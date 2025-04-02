using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace LitMotion.Extensions
{
    public class ButtonEventTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public UnityEvent<PointerEventData> onPointerDown;
        public UnityEvent<PointerEventData> onPointerUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            onPointerDown.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            onPointerUp.Invoke(eventData);
        }
    }
}