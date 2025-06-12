using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheRavine.Base
{
    public class InputBindingAdapter
    {
        private readonly Button _button;
        private readonly InputAction _inputAction;
        private readonly Action _callback;

        private readonly Action<InputAction.CallbackContext> _actionHandler;
        private readonly UnityEngine.Events.UnityAction _buttonHandler;

        private InputBindingAdapter(Button button, InputAction inputAction, Action callback)
        {
            _button = button;
            _inputAction = inputAction;
            _callback = callback;

            _buttonHandler = OnButtonClicked;
            _actionHandler = OnInputPerformed;

            _button.onClick.AddListener(_buttonHandler);
            _inputAction.performed += _actionHandler;

            if (!_inputAction.enabled)
                _inputAction.Enable();
        }

        public static InputBindingAdapter Bind(Button button, InputActionReference actionRef, Action callback)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (actionRef == null || actionRef.action == null) throw new ArgumentNullException(nameof(actionRef));
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            return new InputBindingAdapter(button, actionRef.action, callback);
        }

        public void Unbind()
        {
            _button.onClick.RemoveListener(_buttonHandler);
            _inputAction.performed -= _actionHandler;
        }

        private void OnButtonClicked()
        {
            _callback?.Invoke();
        }

        private void OnInputPerformed(InputAction.CallbackContext ctx)
        {
            _callback?.Invoke();
        }
    }
}