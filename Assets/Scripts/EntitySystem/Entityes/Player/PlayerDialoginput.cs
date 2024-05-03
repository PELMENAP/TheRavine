using UnityEngine;
using TMPro;

namespace TheRavine.EntityControl
{
    public class PlayerDialogInput : MonoBehaviour, IDialogSender
    {
        [SerializeField] private TextMeshProUGUI InputWindow;
        [SerializeField] private TMP_InputField playerText;
        [SerializeField] private float dialogDistance;
        public float GetDialogDistance()
        {
            return dialogDistance;
        }
        public Vector3 GetCurrentPosition(){
            return transform.position;
        }

        public void OnDialogSend()
        {
            DialogSystem.Instance.OnSpeechSend(this, InputWindow.text);
            playerText.interactable = false;
        }

        public void OnDialogGetRequire()
        {
            playerText.interactable = true;
            playerText.Select();
            playerText.text = "";
        }
    }
}