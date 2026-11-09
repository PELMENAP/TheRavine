using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using TheRavine.Events;
using TheRavine.ViewControl;

namespace TheRavine.EntityControl
{
    public sealed class CameraInputController
    {
        private readonly InputAction _scroll;
        private readonly InputAction _rotateRight;
        private readonly InputAction _rotateLeft;
        private readonly EventBus _bus;
        private readonly CancellationTokenSource _cts = new();
        private float _scale;
        private ViewFacing _facing;
        private bool _wheelCaptured;
        private int _wheelStep;
        private float _lastRotateTime = float.NegativeInfinity;

        public float Scale => _scale;
        public ViewFacing Facing => _facing;

        public bool WheelCaptured
        {
            get => _wheelCaptured;
            set
            {
                if (_wheelCaptured == value) return;
                _wheelCaptured = value;
                _wheelStep = 0;
            }
        }

        public int ConsumeWheelStep()
        {
            int step = _wheelStep;
            _wheelStep = 0;
            return step;
        }

        public CameraInputController(InputAction scroll, InputAction rotateRight, InputAction rotateLeft, EventBus bus)
        {
            _scroll      = scroll;
            _rotateRight = rotateRight;
            _rotateLeft  = rotateLeft;
            _bus         = bus;
            _facing      = ViewOrientation.Facing;
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
            if (_scroll != null)
            {
                float wheel = _scroll.ReadValue<float>();
                if (wheel != 0f && _wheelCaptured)
                {
                    _wheelStep += (int)math.sign(wheel);
                }
                else if (wheel != 0f)
                {
                    var rules = SimulationRules.Active;
                    _scale = math.clamp(_scale + math.sign(wheel) * rules.CameraScaleStep, rules.CameraScaleMin, rules.CameraScaleMax);
                    Publish();
                }
            }

            TryRotate(_rotateRight, 1);
            TryRotate(_rotateLeft, -1);
        }

        private void TryRotate(InputAction action, int quarterTurns)
        {
            if (action == null || !action.WasPressedThisFrame()) return;

            float now = Time.unscaledTime;
            if (now - _lastRotateTime < SimulationRules.Active.CameraFlipDebounce) return;

            _lastRotateTime = now;
            _facing = _facing.Rotate(quarterTurns);
            Publish();
        }

        private void Publish() => _bus.Invoke(null, new CameraPlace { scale = _scale, facing = _facing });
    }
}
