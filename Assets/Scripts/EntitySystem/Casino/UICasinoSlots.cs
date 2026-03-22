using UnityEngine;
using UnityEngine.UI;

using TMPro;

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
}
