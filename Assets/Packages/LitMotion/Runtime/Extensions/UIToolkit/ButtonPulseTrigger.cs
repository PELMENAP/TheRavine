
using UnityEngine;
using UnityEngine.EventSystems;

namespace LitMotion.Extensions
{
    public class ButtonPulseTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Vector2 pulseStrength;
        [SerializeField] private float duration;
        [SerializeField] private Ease easeCurve;

        private RectTransform _rectTransform;
        private Vector2 _originalSizeDelta;
        private MotionHandle _motionHandle;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _originalSizeDelta = _rectTransform.sizeDelta;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PlayAnimation(_originalSizeDelta - pulseStrength);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PlayAnimation(_originalSizeDelta);
        }

        private void PlayAnimation(Vector2 target)
        {
            if (_motionHandle.IsActive())
                _motionHandle.Cancel();

            _motionHandle = LMotion.Create(_rectTransform.sizeDelta, target, duration)
                .WithEase(Ease.OutBack)
                .BindToSizeDelta(_rectTransform);
        }

        private void OnDisable()
        {
            if (_motionHandle.IsActive())
                _motionHandle.Cancel();

            _rectTransform.sizeDelta = _originalSizeDelta;
        }
    }
}