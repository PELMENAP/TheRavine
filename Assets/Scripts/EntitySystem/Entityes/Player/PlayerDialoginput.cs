using UnityEngine;
using TMPro;

namespace TheRavine.EntityControl
{
    public class PlayerDialogInput : MonoBehaviour, IDialogListener
    {
        [SerializeField] private TextMeshProUGUI InputWindow;
        [SerializeField] private TMP_InputField playerText;
        [SerializeField] private TextMeshProUGUI heardLabel;
        [SerializeField] private SpeechLexicon lexicon;
        [SerializeField] private PlayerController controller;

        private void OnEnable() => DialogSystem.Instance.AddDialogListener(this);
        private void OnDisable() => DialogSystem.Instance.RemoveDialogListener(this);

        public Vector3 GetCurrentPosition(){
            return transform.position;
        }

        public void OnSpeechGet(Vector3 position, string label)
        {
            if (heardLabel != null) heardLabel.text = label;
        }

        public void OnDialogSend()
        {
            var lex = lexicon != null ? lexicon : SpeechLexicon.Default;
            int len = lex.Encode(InputWindow.text, out ushort tokens);
            if (len > 0 && ResolveController() is PlayerController owner) owner.RequestSpeak(tokens, (byte)len);
            OnDialogGetRequire();
        }

        private PlayerController ResolveController()
        {
            if (controller != null && controller.IsOwner) return controller;
            controller = GetComponentInParent<PlayerController>();
            if (controller != null && controller.IsOwner) return controller;

            foreach (var candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (candidate.IsOwner) return controller = candidate;
            return null;
        }

        public void OnDialogGetRequire()
        {
            playerText.interactable = true;
            playerText.Select();
            playerText.text = "";
        }
    }
}
