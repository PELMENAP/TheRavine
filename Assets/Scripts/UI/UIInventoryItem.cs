using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace TheRavine.InventoryElements
{
    public class UIInventoryItem : UIItem
    {
        [SerializeField] private Color Icolor;
        [SerializeField] private Color Tcolor;
        private Color ImainColor;
        private Color TmainColor;
        [SerializeField] private Image _imageIcon;
        [SerializeField] private TextMeshProUGUI _textAmount;

        public IInventoryItem item { get; private set; }

        public void Refresh(IInventorySlot slot)
        {
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
            if (textAmountEnabled)
                _textAmount.text = $"x{slot.amount.ToString()}";
        }

        private void Cleanup()
        {
            _textAmount.gameObject.SetActive(false);
            _imageIcon.gameObject.SetActive(false);
        }
    }
}