using System;
using System.Collections.Generic;
using Unity.Collections;

public static class GeneticAudioProfileCache
{
    private readonly struct Key : IEquatable<Key>
    {
        private readonly string seed;
        private readonly int harmonicsCount;

        public Key(string seed, int harmonicsCount)
        {
            this.seed = seed;
            this.harmonicsCount = harmonicsCount;
        }

        public bool Equals(Key other) => seed == other.seed && harmonicsCount == other.harmonicsCount;
        public override bool Equals(object obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(seed, harmonicsCount);
    }

    private static readonly Dictionary<Key, AgentGeneticAudioProfile> cache = new(64);
    private static readonly LinkedList<Key> lru = new();
    private static readonly Dictionary<Key, LinkedListNode<Key>> lruNodes = new(64);
    private static int capacity = 128;
    private static readonly object gate = new();

    public static void Configure(int newCapacity) => capacity = Math.Max(1, newCapacity);

    public static AgentGeneticAudioProfile Resolve(string seed, int harmonicsCount, float duration)
    {
        var key = new Key(seed, harmonicsCount);

        lock (gate)
        {
            if (cache.TryGetValue(key, out var hit))
            {
                Touch(key);
                return hit;
            }

            if (cache.Count >= capacity)
                EvictOldest();

            var profile = new AgentGeneticAudioProfile(seed, harmonicsCount, duration);
            cache[key] = profile;
            lruNodes[key] = lru.AddLast(key);
            return profile;
        }
    }

    private static void Touch(Key key)
    {
        var node = lruNodes[key];
        lru.Remove(node);
        lruNodes[key] = lru.AddLast(key);
    }

    private static void EvictOldest()
    {
        var oldest = lru.First;
        if (oldest == null) return;

        lru.RemoveFirst();
        lruNodes.Remove(oldest.Value);

        if (cache.TryGetValue(oldest.Value, out var profile))
            profile.Dispose();

        cache.Remove(oldest.Value);
    }
}