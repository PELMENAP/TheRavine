using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UIInventoryItem : UIItem, IDropHandler
{
    public UnityAction<PointerEventData> onDrop;
    [SerializeField] private Color Icolor;
    [SerializeField] private Color Tcolor;
    private Color ImainColor;
    private Color TmainColor;
    [SerializeField] private Image _imageIcon;
    [SerializeField] private Text _textAmount;

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

    public void OnDrop(PointerEventData eventData)
    {
        onDrop.Invoke(eventData);
    }

    private void Cleanup()
    {
        _textAmount.gameObject.SetActive(false);
        _imageIcon.gameObject.SetActive(false);
    }

    public void Illumination()
    {
        ImainColor = _imageIcon.color;
        TmainColor = _textAmount.color;
        _imageIcon.color = Icolor;
        _textAmount.color = Tcolor;
        PData.pdata.description.text = item.info.description;
        PData.pdata.infoImage.sprite = item.info.infoSprite;
    }

    public void Fading()
    {
        _imageIcon.color = ImainColor;
        _textAmount.color = TmainColor;
    }

    public void Expose()
    {
        PData.pdata.description.text = item.info.description;
        PData.pdata.infoImage.sprite = item.info.infoSprite;
    }
}
