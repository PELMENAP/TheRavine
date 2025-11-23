using UnityEngine;

public class AimModePresenter : MonoBehaviour
{
    [SerializeField] private GameObject image;
    private bool active = false;
    public void ChangeAimModeActive(){
        active = !active;
        image.SetActive(active);
    }
}
