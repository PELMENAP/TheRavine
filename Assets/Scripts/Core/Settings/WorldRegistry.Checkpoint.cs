using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public partial class WorldRegistry
    {
        private readonly SemaphoreSlim checkpointGate = new(1, 1);
        private Func<PopulationSnapshot> populationProvider;
        internal Func<CancellationToken, UniTask> BeforeSave;

        public void RegisterPopulation(Func<PopulationSnapshot> provider)
        {
            if (!HasLoadedWorld || populationProvider != null) throw new InvalidOperationException("Population owner unavailable");
            populationProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public bool OwnsPopulation(Func<PopulationSnapshot> provider) => !disposed && populationProvider == provider;

        public void ReleasePopulation(Func<PopulationSnapshot> provider)
        {
            if (populationProvider == provider) populationProvider = null;
        }

        public async UniTask<bool> SaveCurrentWorldAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread();
            if (disposed || !HasLoadedWorld) return false;
            var owner = currentState;
            var provider = populationProvider;
            string world = currentWorldId.Value;
            try { await checkpointGate.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { return false; }
            try
            {
                await UniTask.SwitchToMainThread();
                if (!OwnsCheckpoint()) return false;
                var snapshot = provider?.Invoke();
                if (snapshot != null)
                {
                    snapshot.Seed = owner.seed;
                    snapshot.WorldCreated = currentConfig.createdTime;
                    snapshot.Validate(owner.seed, currentConfig.createdTime);
                    checkPopulation?.Invoke(snapshot);
                }
                if (BeforeSave != null) await BeforeSave(cancellationToken);
                await UniTask.SwitchToMainThread();
                if (!OwnsCheckpoint()) return false;
                owner.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                long capturedRevision = revision;
                var config = currentConfig.Clone();
                await storage.SaveFullAsync(world, owner.Clone(), config);
                await UniTask.SwitchToMainThread();
                if (!OwnsCheckpoint()) return false;
                if (snapshot != null)
                {
                    PopulationRepository.Save(world, snapshot, owner.seed, config.createdTime);
                    LoadedPopulation = snapshot;
                }
                hasUnsavedChanges = capturedRevision != revision;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
            catch (Exception ex)
            {
                logger.LogError($"[WorldRegistry] Checkpoint failed: {ex.Message}");
                return false;
            }
            finally
            {
                checkpointGate.Release();
            }

            bool OwnsCheckpoint() => !disposed && !cancellationToken.IsCancellationRequested &&
                ReferenceEquals(currentState, owner) && currentWorldId.Value == world && populationProvider == provider;
        }
    }
}
