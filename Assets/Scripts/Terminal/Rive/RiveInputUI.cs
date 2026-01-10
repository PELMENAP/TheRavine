using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheRavine.Base
{
    public class RiveInputUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text waitingReadersText;
        [SerializeField] private GameObject inputPanel;

        private RiveRuntime _runtime;
        private IRavineLogger _logger;

        public void Initialize(RiveRuntime runtime, IRavineLogger logger)
        {
            _runtime = runtime;
            _logger = logger;

            submitButton.onClick.AddListener(OnSubmitInput);
            
            inputPanel?.SetActive(true);
            UpdateWaitingReadersDisplay();
        }

        private void Update()
        {
            UpdateWaitingReadersDisplay();
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

        private void UpdateWaitingReadersDisplay()
        {
            if (waitingReadersText != null && _runtime != null)
            {
                int count = _runtime.GetWaitingInputReaders();
                waitingReadersText.text = count > 0 
                    ? $"Ожидают ввода: {count} программ(ы)" 
                    : "Нет программ, ожидающих ввода";
            }
        }

        public void SetPanelActive(bool active)
        {
            inputPanel?.SetActive(active);
        }
    }
}