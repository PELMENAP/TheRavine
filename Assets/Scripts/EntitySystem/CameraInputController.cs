using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using TheRavine.Events;

namespace TheRavine.EntityControl
{
    public sealed class CameraInputController
    {
        private readonly InputAction _scroll;
        private readonly InputAction _flip;
        private readonly EventBus _bus;
        private readonly CancellationTokenSource _cts = new();
        private float _scale;
        private bool _flipped;
        private float _lastFlipTime = float.NegativeInfinity;

        public float Scale => _scale;
        public bool Flipped => _flipped;

        public CameraInputController(InputAction scroll, InputAction flip, EventBus bus)
        {
            _scroll = scroll;
            _flip   = flip;
            _bus    = bus;
        }

        public void Start() => Run(_cts.Token).Forget();

        public void Stop() => _cts.Cancel();

        private async UniTaskVoid Run(CancellationToken ct)
        {
            while (true)
            {
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
                if (canceled) return;

                Tick();
            }
        }

        private void Tick()
        {
            if (_scroll == null || _flip == null) return;

            float wheel = _scroll.ReadValue<float>();
            if (wheel != 0f)
            {
                var rules = SimulationRules.Active;
                _scale = math.clamp(_scale + math.sign(wheel) * rules.CameraScaleStep, rules.CameraScaleMin, rules.CameraScaleMax);
                Publish();
            }

            if (_flip.WasPressedThisFrame() && Time.unscaledTime - _lastFlipTime >= SimulationRules.Active.CameraFlipDebounce)
            {
                _lastFlipTime = Time.unscaledTime;
                _flipped = !_flipped;
                Publish();
            }
        }

        private void Publish() => _bus.Invoke(null, new CameraPlace { scale = _scale, flip = _flipped });
    }
}
