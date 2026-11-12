using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using TheRavine.Base;
using TheRavine.Events;
using TheRavine.ViewControl;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace TheRavine.EntityControl
{
    public sealed class CameraInputController
    {
        private readonly InputAction _scroll;
        private readonly EventBus _bus;
        private readonly GlobalSettingsController _settings;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<RaycastResult> _hits = new(8);
        private PointerEventData _pointer;

        private float _scale;
        private float _yaw;
        private float _pitch;
        private bool _wheelCaptured;
        private int _wheelStep;

        private ViewDragGesture _gesture;
        private int _touchId;

        public float Scale => _scale;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public bool IsDragging => _gesture.IsDragging;

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

        public CameraInputController(InputAction scroll, EventBus bus, GlobalSettingsController settings)
        {
            _scroll = scroll;
            _bus = bus;
            _settings = settings;
            _yaw = ViewOrientation.YawDegrees;
            _pitch = SimulationRules.Active.CameraPitchInitial;
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
            TickWheel();

            if (_settings != null && _settings.GetCurrent().controlType == ControlType.Mobile)
                TickTouch();
            else
                TickMouse();
        }

        private void TickWheel()
        {
            if (_scroll == null) return;

            float wheel = _scroll.ReadValue<float>();
            if (wheel == 0f) return;

            if (_wheelCaptured)
            {
                _wheelStep += (int)math.sign(wheel);
                return;
            }

            var rules = SimulationRules.Active;
            _scale = math.clamp(_scale + math.sign(wheel) * rules.CameraScaleStep, rules.CameraScaleMin, rules.CameraScaleMax);
            Publish();
        }

        private void TickMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            var button = mouse.leftButton;

            if (button.wasPressedThisFrame && !_gesture.IsTracking)
                Begin(position, 0);

            if (!_gesture.IsTracking) return;

            if (button.wasReleasedThisFrame || !button.isPressed)
                End(false);
            else
                Move(position);
        }

        private void TickTouch()
        {
            if (!EnhancedTouchSupport.enabled) return;

            var touches = Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];

                if (!_gesture.IsTracking)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        Begin(touch.screenPosition, touch.touchId);
                        break;
                    }
                    continue;
                }

                if (touch.touchId != _touchId) continue;

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    End(touch.phase == TouchPhase.Canceled);
                else
                    Move(touch.screenPosition);
                return;
            }

            if (_gesture.IsTracking && !HasTouch(touches, _touchId)) End(true);
        }

        private static bool HasTouch(ReadOnlyArray<Touch> touches, int id)
        {
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].touchId == id) return true;
            return false;
        }

        private void Begin(Vector2 position, int touchId)
        {
            _touchId = touchId;
            _gesture.Begin(position, new float2(Screen.width, Screen.height), IsOverUi(position));
        }

        private void Move(Vector2 position)
        {
            var rules = SimulationRules.Active;
            if (!_gesture.Move(position, rules.CameraDragThresholdPixels, out float2 delta)) return;

            float yaw = _yaw;
            float pitch = _pitch;
            delta *= new float2(rules.CameraInvertDragYaw ? -1f : 1f, rules.CameraInvertDragPitch ? -1f : 1f);
            ViewDragGesture.Rotate(ref yaw, ref pitch, delta, _gesture.Side,
                rules.CameraYawDegreesPerSide, rules.CameraPitchDegreesPerSide,
                rules.CameraPitchMin, rules.CameraPitchMax);

            if (yaw == _yaw && pitch == _pitch) return;

            _yaw = yaw;
            _pitch = pitch;
            Publish();
        }

        private void End(bool canceled)
        {
            if (_gesture.End(canceled))
                _bus.Invoke(null, new ViewClick());
        }

        private bool IsOverUi(Vector2 position)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            _pointer ??= new PointerEventData(eventSystem);
            _pointer.position = position;
            _hits.Clear();
            eventSystem.RaycastAll(_pointer, _hits);
            return _hits.Count > 0;
        }

        private void Publish() => _bus.Invoke(null, new CameraPlace { scale = _scale, yaw = _yaw, pitch = _pitch });
    }
}
