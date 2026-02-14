using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace TheRavine.InventoryElements
{
    public class UIInventoryItem : MonoBehaviour
    {
        public CanvasGroup _canvasGroup;
        public RectTransform _rectTransform;
        [SerializeField] private Image _imageIcon;
        [SerializeField] private TextMeshProUGUI _textAmount;

        public IInventoryItem item { get; private set; }

        public void Refresh(InventorySlot slot)
        {
            if(_imageIcon == null) return;
            if (slot.isEmpty)
            {
                Cleanup();
                return;
            }
            item = slot.item;
            _imageIcon.sprite = item.info.spriteIcon;
            _imageIcon.gameObject.SetActive(true);
            var textAmountEnabled = slot.amount > 1;
            _textAmount.gameObject.SetActive(textAmountEnabled);
            if (textAmountEnabled) _textAmount.text = $"{slot.amount}";
        }

        private void Cleanup()
        {
            _textAmount.gameObject.SetActive(false);
            _imageIcon.gameObject.SetActive(false);
        }
    }
}