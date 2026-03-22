
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace LitMotion.Extensions
{
    public class ButtonPulseTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public UnityEvent<PointerEventData> onPointerDown;
        public UnityEvent<PointerEventData> onPointerUp;

        [SerializeField] private Vector2 pulseStrength;
        [SerializeField] private float duration;

        private void OnEnable()
        {
            RectTransform buttonRTransform = (RectTransform)transform;
            onPointerDown.AddListener(_ =>
            {
                LMotion.Create(buttonRTransform.sizeDelta, buttonRTransform.sizeDelta - pulseStrength, duration)
                    .BindToSizeDelta(buttonRTransform);
            });
            onPointerUp.AddListener(_ =>
            {
                LMotion.Create(buttonRTransform.sizeDelta - pulseStrength, buttonRTransform.sizeDelta, duration)
                    .BindToSizeDelta(buttonRTransform);
            });
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            onPointerDown.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            onPointerUp.Invoke(eventData);
        }

        private void OnDisable()
        {
            onPointerDown.RemoveAllListeners();
            onPointerUp.RemoveAllListeners();
        }
    }
}