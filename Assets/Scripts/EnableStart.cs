using UnityEngine;

public class EnableStart : MonoBehaviour
{
    public bool enableStart;
    private void Awake()
    {
        if (enableStart)
        {
            if (DataStorage.loadkey)
            {
                this.gameObject.SetActive(false);
            }
        }
        else
        {
            if (DataStorage.normkey)
            {
                this.gameObject.SetActive(false);
            }
        }
    }
}
