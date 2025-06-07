using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;

namespace TheRavine.Base
{
    public class CircularTransformBuffer : IDisposable
    {
        private readonly int capacity;
        private readonly Queue<Transform[]> incomingGroups = new();
        private TransformAccessArray accessArray;

        private int count;
        private int head;

        public TransformAccessArray AccessArray => accessArray;

        public CircularTransformBuffer(int capacity)
        {
            this.capacity = capacity;
            this.accessArray = new TransformAccessArray(capacity);
            this.count = 0;
            this.head = 0;
        }
        public void SubmitGroup(Transform[] transforms)
        {
            if (transforms is { Length: > 0 })
                incomingGroups.Enqueue(transforms);
        }
        public void Sync()
        {
            while (incomingGroups.Count > 0)
            {
                var group = incomingGroups.Dequeue();

                for (int i = 0; i < group.Length; i++)
                {
                    var tr = group[i];
                    if (tr == null) continue;

                    if (count < capacity)
                    {
                        accessArray.Add(tr);
                        count++;
                    }
                    else
                    {
                        accessArray.RemoveAtSwapBack(head);
                        accessArray.Add(tr);
                        head = (head + 1) % capacity;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (accessArray.isCreated)
                accessArray.Dispose();
        }
    }


    public class CircularLightBuffer : IDisposable
    {
        private readonly int capacity;
        private readonly Queue<(Transform transform, float intensity)> incomingLights = new();
        private Transform[] lightTransforms;
        private float[] lightIntensities;
        
        private int count;
        private int head;
        private bool hasChanges;

        public int Count => count;
        public Transform[] LightTransforms => lightTransforms;
        public float[] LightIntensities => lightIntensities;
        public bool HasChanges => hasChanges;

        public CircularLightBuffer(int capacity)
        {
            this.capacity = capacity;
            this.lightTransforms = new Transform[capacity];
            this.lightIntensities = new float[capacity];
            this.count = 0;
            this.head = 0;
            this.hasChanges = false;
        }

        public void SubmitGroup(Transform transformLight, float lightIntensity)
        {
            if (transformLight != null)
                incomingLights.Enqueue((transformLight, lightIntensity));
        }

        public void Sync()
        {
            hasChanges = incomingLights.Count > 0;
            
            while (incomingLights.Count > 0)
            {
                var (transform, intensity) = incomingLights.Dequeue();
                
                if (transform == null) continue;

                if (count < capacity)
                {
                    lightTransforms[count] = transform;
                    lightIntensities[count] = intensity;
                    count++;
                }
                else
                {
                    lightTransforms[head] = transform;
                    lightIntensities[head] = intensity;
                    head = (head + 1) % capacity;
                }
            }
        }

        public void ClearChangesFlag()
        {
            hasChanges = false;
        }

        public void Dispose()
        {
            lightTransforms = null;
            lightIntensities = null;
        }
    }
}