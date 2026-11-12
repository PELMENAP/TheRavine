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
        private static readonly int SeasonId         = Shader.PropertyToID("_Season");
        private static readonly int WinterApproachId = Shader.PropertyToID("_WinterApproach");
        private const double SeasonPublishInterval = 1d;

        private Camera _camera;
        private double _nextSeasonPublish;
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

            PublishSeason();

            if (_camera == null) _camera = Camera.main;
            for (int i = 0; i < _count; i++) _events[i].Render(_camera);
        }

        private void PublishSeason()
        {
            if (Season == null) return;

            double now = SimulationClock.TimeD;
            if (now < _nextSeasonPublish) return;
            _nextSeasonPublish = now + SeasonPublishInterval;

            Shader.SetGlobalFloat(SeasonId, Season.Value(now));
            Shader.SetGlobalFloat(WinterApproachId, Season.WinterApproach(now));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _count; i++) _events[i].Dispose();
            Array.Clear(_events, 0, _count);
            _count = 0;
            Season = null;
            Shader.SetGlobalFloat(SeasonId, 0f);
            Shader.SetGlobalFloat(WinterApproachId, 0f);
            _camera = null;
        }
    }
}
