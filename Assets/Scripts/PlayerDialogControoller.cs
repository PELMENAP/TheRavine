using System.Collections;
using UnityEngine;
using TMPro;

public class PlayerDialogControoller : MonoBehaviour
{
    public static PlayerDialogControoller instance;
    private TextMeshPro DialogWindow;
    private bool alredyTold = false;
    private void Awake()
    {
        instance = this;
        DialogWindow = this.GetComponent<TextMeshPro>();
    }
    public IEnumerator TypeLine(string speech)
    {
        if (alredyTold)
            yield return new WaitForSeconds(5f);
        alredyTold = true;
        DialogWindow.text = "";
        foreach (char i in speech.ToCharArray())
        {
            DialogWindow.text += i;
            yield return new WaitForSeconds(0.07f);
        }
        yield return new WaitForSeconds(3f);
        DialogWindow.text = "";
        alredyTold = false;
    }
}
