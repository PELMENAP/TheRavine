using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheRavine.Extensions
{
    public class GestureEntryItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Button _deleteButton;

        private int _index;
        private Action<int> _onDelete;

        public void Initialize(string gestureName, int index, Action<int> onDelete)
        {
            _nameText.text = gestureName;
            _index = index;
            _onDelete = onDelete;
            _deleteButton.onClick.AddListener(HandleDelete);
        }

        public void UpdateIndex(int newIndex)
        {
            _index = newIndex;
        }

        private void OnDestroy()
        {
            _deleteButton.onClick.RemoveListener(HandleDelete);
        }

        private void HandleDelete()
        {
            _onDelete?.Invoke(_index);
        }
    }
}