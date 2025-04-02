using UnityEngine;
using UnityEngine.UI;

using TMPro;
using LitMotion;
using LitMotion.Extensions;

public class UICasinoSlots : MonoBehaviour
{
    [SerializeField] private Image[] helpImages;
    [SerializeField] private TextMeshProUGUI[] helpText;
    public void FillTheHelp(CasinoSlotPref currentPref, int factor = 1)
    {
        for(byte i = 0; i < 10; i++)
        {
            helpImages[i].sprite = currentPref.slotCells[i].sprite;
            helpText[i].text = $"x2 - {currentPref.slotCells[i].costs[0] * factor} \n x3 - {currentPref.slotCells[i].costs[1] * factor} \n x4 - {currentPref.slotCells[i].costs[2] * factor} \n x5 - {currentPref.slotCells[i].costs[3] * factor}";
        }

        helpImages[10].sprite = currentPref.slotCells[10].sprite;
        helpText[10].text = $"x2 - {currentPref.slotCells[10].costs[0]} bonus games \n x3 - {currentPref.slotCells[10].costs[1]} bonus games \n x4 - {currentPref.slotCells[10].costs[2]} bonus games \n x5 - {currentPref.slotCells[10].costs[3]} bonus games";
        
        helpImages[11].sprite = currentPref.slotCells[11].sprite;
        helpText[11].text = "can sabstitute others  \n x5 - " + 1000 * factor;
    }
    [SerializeField] private ButtonEventTrigger[] buttonTrigger;
    [SerializeField] private HoverEventTrigger[] hoverTrigger;
    [SerializeField] private Image[] fillImage;
    public void StartUI(CasinoSlotPref currentPref)
    {
        FillTheHelp(currentPref);

        var buttonTransform = (RectTransform)buttonTrigger[0].transform;
        var buttonSize = buttonTransform.sizeDelta;
        buttonTrigger[0].onPointerDown.AddListener(_ =>
        {
            LMotion.Create(buttonSize, buttonSize - new Vector2(10f, 10f), 0.08f)
                .BindToSizeDelta(buttonTransform);
        });
        buttonTrigger[0].onPointerUp.AddListener(_ =>
        {
            LMotion.Create(buttonSize - new Vector2(10f, 10f), buttonSize, 0.08f)
                .BindToSizeDelta(buttonTransform);
        });

        hoverTrigger[0].onPointerEnter.AddListener(_ =>
        {
            fillImage[0].fillOrigin = 0;
            LMotion.Create(0f, 1f, 0.1f)
                .BindToFillAmount(fillImage[0]);
        });
        hoverTrigger[0].onPointerExit.AddListener(_ =>
        {
            fillImage[0].fillOrigin = 1;
            LMotion.Create(1f, 0f, 0.1f)
                .BindToFillAmount(fillImage[0]);
        });

        buttonTransform = (RectTransform)buttonTrigger[1].transform;
        buttonSize = buttonTransform.sizeDelta;
        buttonTrigger[1].onPointerDown.AddListener(_ =>
        {
            LMotion.Create(buttonSize, buttonSize - new Vector2(10f, 10f), 0.08f)
                .BindToSizeDelta(buttonTransform);
        });
        buttonTrigger[1].onPointerUp.AddListener(_ =>
        {
            LMotion.Create(buttonSize - new Vector2(10f, 10f), buttonSize, 0.08f)
                .BindToSizeDelta(buttonTransform);
        });

        hoverTrigger[1].onPointerEnter.AddListener(_ =>
        {
            fillImage[1].fillOrigin = 0;
            LMotion.Create(0f, 1f, 0.1f)
                .BindToFillAmount(fillImage[1]);
        });
        hoverTrigger[1].onPointerExit.AddListener(_ =>
        {
            fillImage[1].fillOrigin = 1;
            LMotion.Create(1f, 0f, 0.1f)
                .BindToFillAmount(fillImage[1]);
        });

        buttonTransform = (RectTransform)buttonTrigger[2].transform;
        buttonSize = buttonTransform.sizeDelta;
        buttonTrigger[2].onPointerDown.AddListener(_ =>
        {
            LMotion.Create(buttonSize, buttonSize - new Vector2(20f, 20f), 0.08f)
                .BindToSizeDelta(buttonTransform);
        });
        buttonTrigger[2].onPointerUp.AddListener(_ =>
        {
            LMotion.Create(buttonSize - new Vector2(20f, 20f), buttonSize, 0.08f)
                .BindToSizeDelta(buttonTransform);
        });

        hoverTrigger[2].onPointerEnter.AddListener(_ =>
        {
            LMotion.Create(buttonSize, buttonSize + new Vector2(20f, 20f), 0.08f)
                .BindToSizeDelta(buttonTransform);
        });
        hoverTrigger[2].onPointerExit.AddListener(_ =>
        {
            LMotion.Create(buttonSize + new Vector2(20f, 20f), buttonSize, 0.08f)
                .BindToSizeDelta(buttonTransform);
        });

        var buttonTransform1 = (RectTransform)buttonTrigger[3].transform;
        buttonSize = buttonTransform1.sizeDelta;
        buttonTrigger[3].onPointerDown.AddListener(_ =>
        {
            LMotion.Create(buttonSize, buttonSize - new Vector2(20f, 20f), 0.08f)
                .BindToSizeDelta(buttonTransform1);
        });
        buttonTrigger[3].onPointerUp.AddListener(_ =>
        {
            LMotion.Create(buttonSize - new Vector2(20f, 20f), buttonSize, 0.08f)
                .BindToSizeDelta(buttonTransform1);
        });

        hoverTrigger[3].onPointerEnter.AddListener(_ =>
        {
            LMotion.Create(buttonSize, buttonSize + new Vector2(20f, 20f), 0.08f)
                .BindToSizeDelta(buttonTransform1);
        });
        hoverTrigger[3].onPointerExit.AddListener(_ =>
        {
            LMotion.Create(buttonSize + new Vector2(20f, 20f), buttonSize, 0.08f)
                .BindToSizeDelta(buttonTransform1);
        });
    }
}
