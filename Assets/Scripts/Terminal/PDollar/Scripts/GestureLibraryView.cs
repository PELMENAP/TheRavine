using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheRavine.Extensions
{
    public class GestureLibraryView : MonoBehaviour
    {

        [Header("Scroll View")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private GestureEntryItem _itemPrefab;

        private GestureRepository _repository;
        private readonly List<GestureEntryItem> _items = new();

        public void Initialize(GestureRepository repository)
        {
            _repository = repository;
            _repository.OnEntryAdded += HandleEntryAdded;
            _repository.OnEntryRemoved += HandleEntryRemoved;

            Rebuild();
        }

        private void OnDestroy()
        {
            if (_repository == null) return;

            _repository.OnEntryAdded -= HandleEntryAdded;
            _repository.OnEntryRemoved -= HandleEntryRemoved;
        }

        private void Rebuild()
        {
            foreach (GestureEntryItem item in _items)
                Destroy(item.gameObject);
            _items.Clear();

            IReadOnlyList<GestureEntry> entries = _repository.Entries;
            for (int i = 0; i < entries.Count; i++)
                SpawnItem(entries[i].Gesture.Name, i);
        }

        private void SpawnItem(string gestureName, int index)
        {
            GestureEntryItem item = Instantiate(_itemPrefab, _content);
            item.Initialize(gestureName, index, OnDeleteClicked);
            _items.Add(item);
        }

        private void HandleEntryAdded(int index)
        {
            SpawnItem(_repository.Entries[index].Gesture.Name, index);
        }

        private void HandleEntryRemoved(int index)
        {
            Destroy(_items[index].gameObject);
            _items.RemoveAt(index);

            for (int i = index; i < _items.Count; i++)
                _items[i].UpdateIndex(i);
        }

        private void OnDeleteClicked(int index)
        {
            _repository.RemoveAt(index);
        }
    }
}