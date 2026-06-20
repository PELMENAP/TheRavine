using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MemoryPack;

namespace TheRavine.Extensions
{
    public class GestureRepository
    {
        private const string FileName = "gestures.bin";
        private readonly List<Gesture> gestures = new();
        private static string RepositoryPath =>
            Path.Combine(Application.persistentDataPath, FileName);
        public IReadOnlyList<Gesture> Entries => gestures;
        public event Action<int> OnEntryAdded;
        public event Action<int> OnEntryRemoved;
        public void Load()
        {
            
            gestures?.Clear();

            if (!File.Exists(RepositoryPath))
                return;

            byte[] bytes = File.ReadAllBytes(RepositoryPath);

            var loadedGestures = MemoryPackSerializer.Deserialize<List<Gesture>>(bytes);
            if (loadedGestures is { Count: > 0 })
                gestures.AddRange(loadedGestures);
        }

        public void Save()
        {
            byte[] bytes = MemoryPackSerializer.Serialize(gestures);
            File.WriteAllBytes(RepositoryPath, bytes);
        }

        public void Add(Point[] points, string gestureName)
        {
            gestures.Add(new Gesture(points, gestureName));
            Save();

            OnEntryAdded?.Invoke(gestures.Count - 1);
        }

        public void RemoveAt(int index)
        {
            gestures.RemoveAt(index);
            Save();

            OnEntryRemoved?.Invoke(index);
        }

        public Gesture[] GetGesturesArray()
        {
            return gestures.ToArray();
        }
    }
}