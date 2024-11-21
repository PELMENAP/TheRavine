using UnityEngine;
using UnityEngine.UI;

public class CraftPresenter : MonoBehaviour
{
    [SerializeField] private Image progress;
    [SerializeField] private GameObject craftButton;

    public void CraftPossible(bool isActive)
    {
        if(craftButton == null) return;
        craftButton.SetActive(isActive);
    }

    public bool FillProgressBar(float amount){
        if(progress == null) return false;
        if(progress.fillAmount >= 1)
        {
            progress.fillAmount = 0;    
            return false;
        }
        progress.fillAmount += amount;
        return true;
    }
}
