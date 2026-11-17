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
        public const long MaxFileBytes = 1L << 20;
        public const int MaxGestures = 256;
        public const int MaxNameLength = 64;
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

            if (!AtomicFile.TryReadAllBytes(RepositoryPath, MaxFileBytes, out byte[] bytes))
            {
                RavineLog.Warning($"[GestureRepository] {FileName} is larger than {MaxFileBytes} bytes, file ignored");
                return;
            }

            List<Gesture> loadedGestures;
            try
            {
                loadedGestures = MemoryPackSerializer.Deserialize<List<Gesture>>(bytes);
            }
            catch (Exception e)
            {
                RavineLog.Warning($"[GestureRepository] {FileName} is corrupted, file ignored: {e.Message}");
                return;
            }

            if (loadedGestures == null) return;

            int skipped = 0;
            for (int i = 0; i < loadedGestures.Count; i++)
            {
                if (gestures.Count >= MaxGestures || !IsValid(loadedGestures[i]))
                {
                    skipped++;
                    continue;
                }
                gestures.Add(loadedGestures[i]);
            }

            if (skipped > 0)
                RavineLog.Warning($"[GestureRepository] {skipped} gestures were skipped as invalid or over the limit of {MaxGestures}");
        }

        private static bool IsValid(Gesture gesture)
        {
            if (gesture == null || gesture.Points == null || gesture.Theta == null) return false;
            if (gesture.Points.Length != Gesture.SAMPLING_RESOLUTION || gesture.Theta.Length != gesture.Points.Length) return false;
            if (gesture.Name != null && gesture.Name.Length > MaxNameLength) return false;

            for (int i = 0; i < gesture.Points.Length; i++)
            {
                if (!float.IsFinite(gesture.Points[i].X) || !float.IsFinite(gesture.Points[i].Y) || !float.IsFinite(gesture.Theta[i]))
                    return false;
            }
            return true;
        }

        public void Save()
        {
            byte[] bytes = MemoryPackSerializer.Serialize(gestures);

            try
            {
                AtomicFile.WriteAllBytes(RepositoryPath, bytes);
            }
            catch (IOException e)
            {
                RavineLog.Error($"[GestureRepository] Save failed: {e.Message}");
            }
        }

        public void Add(Point[] points, string gestureName)
        {
            if (gestures.Count >= MaxGestures)
            {
                RavineLog.Warning($"[GestureRepository] Gesture limit {MaxGestures} reached");
                return;
            }

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