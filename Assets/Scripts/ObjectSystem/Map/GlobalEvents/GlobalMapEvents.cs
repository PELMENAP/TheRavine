using System;
using UnityEngine;
using TheRavine.Base;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class GlobalMapEvents : IDisposable
    {
        private IMapEvent[] _events = new IMapEvent[4];
        private int _count;
        private Camera _camera;
        private bool _disposed;

        public MapEventRules Rules { get; }
        public IMapSeason Season { get; set; }
        public FireEvent Fire { get; }

        public GlobalMapEvents(MapGenerator map, ObjectInfoRegistry registry, RandomTickService ticks,
            MapEventRules rules, FireViewSettings fireView)
        {
            Rules = rules;
            Fire = new FireEvent(this, map, registry, rules.fire);
            ticks.Register(Fire);
            Add(Fire);
            if (fireView != null && fireView.enabled) Fire.AttachView(FireView.TryCreate(Fire, map, fireView));
        }

        public void Add(IMapEvent mapEvent)
        {
            if (mapEvent == null || _disposed) return;
            if (_count == _events.Length) Array.Resize(ref _events, _count * 2);
            _events[_count++] = mapEvent;
        }

        public float SeasonValue(double time) => Season != null ? Season.Value(time) : 0f;
        public bool IsWinter(double time) => Season != null && Season.IsWinter(time);

        public void Tick()
        {
            if (_disposed) return;

            if (WorldAuthority.IsAuthority)
            {
                double now = SimulationClock.TimeD;
                float dt = SimulationClock.DeltaTime;
                for (int i = 0; i < _count; i++) _events[i].Step(now, dt);
            }

            if (_camera == null) _camera = Camera.main;
            for (int i = 0; i < _count; i++) _events[i].Render(_camera);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _count; i++) _events[i].Dispose();
            Array.Clear(_events, 0, _count);
            _count = 0;
            Season = null;
            _camera = null;
        }
    }
}
