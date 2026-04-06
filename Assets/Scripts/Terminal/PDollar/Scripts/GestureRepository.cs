using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheRavine.Extensions
{
    public readonly struct GestureEntry
    {
        public readonly Gesture Gesture;
        public readonly string FilePath;

        public GestureEntry(Gesture gesture, string filePath)
        {
            Gesture = gesture;
            FilePath = filePath;
        }
    }

    public class GestureRepository
    {
        private readonly List<GestureEntry> _entries = new();

        public IReadOnlyList<GestureEntry> Entries => _entries;

        public event Action<int> OnEntryAdded;
        public event Action<int> OnEntryRemoved;

        public void Load()
        {
            string[] filePaths = Directory.GetFiles(Application.persistentDataPath, "*.xml");
            foreach (string filePath in filePaths)
            {
                Gesture gesture = GestureIO.ReadGestureFromFile(filePath);
                _entries.Add(new GestureEntry(gesture, filePath));
            }
        }

        public void Add(Point[] points, string gestureName)
        {
            string filePath = $"{Application.persistentDataPath}/{gestureName}-{DateTime.Now.ToFileTime()}.xml";
            GestureIO.WriteGesture(points, gestureName, filePath);
            _entries.Add(new GestureEntry(new Gesture(points, gestureName), filePath));
            OnEntryAdded?.Invoke(_entries.Count - 1);
        }

        public void RemoveAt(int index)
        {
            string filePath = _entries[index].FilePath;
            if (File.Exists(filePath))
                File.Delete(filePath);
            _entries.RemoveAt(index);
            OnEntryRemoved?.Invoke(index);
        }

        public Gesture[] GetGesturesArray()
        {
            var result = new Gesture[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
                result[i] = _entries[i].Gesture;
            return result;
        }
    }
}