using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheRavine.Base
{
    public class RiveInputUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button submitButton;

        private RiveRuntime _runtime;
        private RavineLogger _logger;

        public void Initialize(RiveRuntime runtime, RavineLogger logger)
        {
            _runtime = runtime;
            _logger = logger;

            submitButton.onClick.AddListener(OnSubmitInput);
        }


        private void OnSubmitInput()
        {
            if (string.IsNullOrWhiteSpace(inputField.text))
            {
                _logger.LogWarning("Введите число");
                return;
            }

            if (!int.TryParse(inputField.text, out int value))
            {
                _logger.LogWarning("Неверный формат числа");
                return;
            }

            _runtime.PushInput(value);
            
            inputField.text = "";
            inputField.ActivateInputField();
        }
    }
}