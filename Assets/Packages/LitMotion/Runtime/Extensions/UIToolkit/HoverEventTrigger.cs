using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace LitMotion.Extensions
{
    public class HoverFillTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public UnityEvent<PointerEventData> onPointerEnter;
        public UnityEvent<PointerEventData> onPointerExit;

        [SerializeField] private Image fillImage;
        [SerializeField] private float fillEdge = 1f;
        [SerializeField] private float duration;

        private void OnEnable()
        {
            onPointerEnter.AddListener(_ =>
            {
                fillImage.fillOrigin = 0;
                LMotion.Create(0f, fillEdge, duration)
                    .BindToFillAmount(fillImage);
            });
            onPointerExit.AddListener(_ =>
            {
                fillImage.fillOrigin = 1;
                LMotion.Create(fillEdge, 0f, duration)
                    .BindToFillAmount(fillImage);
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            onPointerEnter.Invoke(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            onPointerExit.Invoke(eventData);
        }

        private void OnDisable()
        {
            onPointerEnter.RemoveAllListeners();
            onPointerExit.RemoveAllListeners();
        }
    }
}