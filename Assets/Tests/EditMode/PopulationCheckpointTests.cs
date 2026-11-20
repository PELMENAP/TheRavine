using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TheRavine.Base;
using UnityEngine;
using UnityEngine.TestTools;

public class PopulationCheckpointTests
{
    private readonly string world = Guid.NewGuid().ToString("N");

    private static PopulationSnapshot Example()
    {
        var genes = GeneticParameters.Default;
        return new PopulationSnapshot
        {
            Seed = 22, WorldCreated = 33, Captured = 44, LastEntityId = 90, LastColonyId = 80,
            Colonies = new[] { new PopulationSnapshot.Colony
            {
                Id = 65, Leader = 7, X = -80, Z = 400, Hunger = 0.25f, Storage = 230,
                Arm = (byte)ColonyArm.SpeechMuted, LearningRate = 0.1f, GradientNorm = 2, Entropy = 0.2f, ElectionRemaining = 31
            } },
            Agents = new[] { new PopulationSnapshot.Agent
            {
                Id = 7, ColonyId = 65, Parent = 3, Age = 130, Health = 8, Energy = 12, Stomach = 5, Carrying = 2,
                Position = new Vector3(-20, 31, 42), Caste = (byte)Caste.Scout, Fixed = true, AttackRemaining = 0.75, ReviewRemaining = 3.25,
                Genes = GeneticParameters.AsReadOnlySpan(in genes).ToArray()
            } }
        };
    }

    [TearDown]
    public void Clean()
    {
        string directory = WorldRegistry.GetWorldDirectory(world);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void MissingPopulatedAndEmptyAreDistinct()
    {
        var s = Example();
        Assert.IsNull(PopulationRepository.Load(world, s.Seed, s.WorldCreated));
        PopulationRepository.Save(world, s, s.Seed, s.WorldCreated);
        var loaded = PopulationRepository.Load(world, s.Seed, s.WorldCreated);
        Assert.AreEqual(s.Colonies, loaded.Colonies);
        var agent = loaded.Agents[0];
        CollectionAssert.AreEqual(s.Agents[0].Genes, agent.Genes);
        agent.Genes = s.Agents[0].Genes;
        Assert.AreEqual(s.Agents[0], agent);
        Assert.AreEqual(s.LastEntityId, loaded.LastEntityId);
        Assert.AreEqual(s.LastColonyId, loaded.LastColonyId);
        Assert.Throws<InvalidDataException>(() => PopulationRepository.Load(world, s.Seed + 1, s.WorldCreated));
        s.Agents = Array.Empty<PopulationSnapshot.Agent>();
        PopulationRepository.Save(world, s, s.Seed, s.WorldCreated);
        Assert.IsEmpty(PopulationRepository.Load(world, s.Seed, s.WorldCreated).Agents);
    }

    [TestCase("duplicate")]
    [TestCase("orphan")]
    [TestCase("nan")]
    [TestCase("highwater")]
    public void InvalidReplacementPreservesPreviousCheckpoint(string fault)
    {
        var s = Example();
        PopulationRepository.Save(world, s, s.Seed, s.WorldCreated);
        if (fault == "duplicate") s.Agents = new[] { s.Agents[0], s.Agents[0] };
        if (fault == "orphan") s.Agents[0].ColonyId++;
        if (fault == "nan") s.Agents[0].Genes[2] = float.NaN;
        if (fault == "highwater") s.LastEntityId = 4;
        Assert.Throws<InvalidDataException>(() => PopulationRepository.Save(world, s, s.Seed, s.WorldCreated));
        Assert.AreEqual(7, PopulationRepository.Load(world, s.Seed, s.WorldCreated).Agents[0].Id);
    }

    [TestCase(4, 2)]
    [TestCase(36, -1)]
    [TestCase(40, int.MaxValue)]
    [TestCase(40, 0)]
    public void RefusesCorruptHeaderBeforeReadingRecords(int offset, int value)
    {
        var s = Example();
        using var stream = new MemoryStream();
        PopulationRepository.Write(stream, s);
        stream.Position = offset;
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        writer.Write(value);
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => PopulationRepository.Read(stream, s.Seed, s.WorldCreated));
    }

    [Test]
    public void ChecksSceneCapacityAndGeneRangesBeforeRegistration()
    {
        var s = Example();
        EntityManager.ValidateRuntimePopulation(s, 1, 0);
        Assert.Throws<InvalidDataException>(() => EntityManager.ValidateRuntimePopulation(s, 0, 1));
        s.Agents[0].Genes[0] = float.MaxValue;
        Assert.Throws<InvalidDataException>(() => EntityManager.ValidateRuntimePopulation(s, 1, 0));
    }

    [UnityTest]
    public IEnumerator OrderedCheckpointsAndSwitchKeepTheOwningWorld() => UniTask.ToCoroutine(async () =>
    {
        var storage = new MemoryStorage();
        using var registry = new WorldRegistry(storage, new RavineLogger(null, "population-checkpoint-test.log"));
        const string other = "population-test-other";
        Assert.IsTrue(await registry.CreateWorldAsync(world));
        Assert.IsTrue(await registry.CreateWorldAsync(other));
        Assert.IsTrue(await registry.LoadWorldAsync(world));
        int capture = 0;
        registry.RegisterPopulation(() =>
        {
            var s = Example();
            s.Agents[0].Energy = ++capture;
            return s;
        });
        registry.UpdateState(s => { s.currency = 45; s.codex = new CodexSave { version = 1 }; });
        storage.PauseNextWrite();
        var first = registry.SaveCurrentWorldAsync();
        var second = registry.SaveCurrentWorldAsync();
        var change = registry.LoadWorldAsync(other);
        Assert.AreEqual(1, capture);
        storage.Resume();
        Assert.IsTrue(await first);
        Assert.IsTrue(await second);
        Assert.IsTrue(await change);
        Assert.IsNull(registry.LoadedPopulation);
        Assert.IsTrue(await registry.LoadWorldAsync(world));
        Assert.AreEqual(3, registry.LoadedPopulation.Agents[0].Energy);
        Assert.AreEqual(45, registry.GetCurrentState().currency);
        Assert.AreEqual(1, registry.GetCurrentState().codex.version);
    });

    [UnityTest]
    public IEnumerator PeriodicSaveCapturesAndDisposedOwnerCannotPublish() => UniTask.ToCoroutine(async () =>
    {
        var storage = new MemoryStorage();
        var logger = new RavineLogger(null, "population-periodic-test.log");
        using var registry = new WorldRegistry(storage, logger);
        using var periodic = new AutosaveCoordinator(registry, logger);
        Assert.IsTrue(await registry.CreateWorldAsync(world));
        Assert.IsTrue(await registry.LoadWorldAsync(world));
        int captures = 0;
        Func<PopulationSnapshot> capture = () =>
        {
            var s = Example();
            s.Agents[0].Energy = ++captures;
            return s;
        };
        registry.RegisterPopulation(capture);
        periodic.SetInterval(1);
        periodic.Start();
        double deadline = Time.realtimeSinceStartupAsDouble + 8;
        while (registry.LoadedPopulation == null && Time.realtimeSinceStartupAsDouble < deadline)
            await UniTask.Yield();
        periodic.Stop();
        Assert.IsNotNull(registry.LoadedPopulation);
        Assert.AreEqual(1, captures);
        var config = registry.GetCurrentConfig();
        var state = registry.GetCurrentState();
        storage.PauseNextWrite();
        var save = registry.SaveCurrentWorldAsync();
        Assert.AreEqual(2, captures);
        registry.ReleasePopulation(capture);
        storage.Resume();
        Assert.IsFalse(await save);
        var existing = PopulationRepository.Load(world, state.seed, config.createdTime);
        Assert.AreEqual(1, existing.Agents[0].Energy);
    });

    [UnityTest]
    public IEnumerator CorruptCheckpointFailsWorldLoadAndKeepsBytes() => UniTask.ToCoroutine(async () =>
    {
        using var registry = new WorldRegistry(new MemoryStorage(), new RavineLogger(null, "population-corrupt-test.log"));
        Assert.IsTrue(await registry.CreateWorldAsync(world));
        string directory = WorldRegistry.GetWorldDirectory(world);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "population.bin");
        var corrupt = new byte[] { 1, 2, 3 };
        File.WriteAllBytes(path, corrupt);
        LogAssert.Expect(LogType.Error, "[ERROR] [WorldRegistry] World load failed: Population: invalid file size");
        Assert.IsFalse(await registry.LoadWorldAsync(world));
        Assert.IsFalse(registry.HasLoadedWorld);
        CollectionAssert.AreEqual(corrupt, File.ReadAllBytes(path));
    });

    private sealed class MemoryStorage : IAsyncPersistentStorage
    {
        private readonly Dictionary<string, object> data = new();
        private UniTaskCompletionSource gate;
        private bool pause;
        public void PauseNextWrite() { gate = new UniTaskCompletionSource(); pause = true; }
        public void Resume() => gate.TrySetResult();

        public async UniTask SaveAsync<T>(string key, T value, CancellationToken ct = default)
        {
            if (pause) { pause = false; await gate.Task; }
            data[key] = value;
        }

        public UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
            => UniTask.FromResult(data.TryGetValue(key, out var value) ? (T)value : default);
        public UniTask<bool> ExistsAsync(string key, CancellationToken ct = default) => UniTask.FromResult(data.ContainsKey(key));
        public UniTask DeleteAsync(string key, CancellationToken ct = default)
        {
            data.Remove(key);
            return UniTask.CompletedTask;
        }
    }
}
