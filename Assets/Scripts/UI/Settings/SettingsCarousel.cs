using UnityEngine;
using UnityEngine.UI;
using LitMotion;

namespace TheRavine.Base
{
    public class SettingsCarousel : MonoBehaviour
    {
        [SerializeField] private RectTransform[] groups;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField, Min(1)] private int visibleCount = 2;
        [SerializeField, Min(0.01f)] private float duration = 0.35f;
        [SerializeField] private Ease ease = Ease.OutCubic;

        private CanvasGroup[] _canvasGroups;
        private float[] _fromX, _toX, _fromAlpha, _toAlpha;
        private float _baseX, _step;
        private int _first;
        private bool _initialized;
        private MotionHandle _motion;

        private int MaxFirst => Mathf.Max(0, groups.Length - visibleCount);

        private void Awake()
        {
            if (groups == null || groups.Length < 2) return;

            int count = groups.Length;
            _canvasGroups = new CanvasGroup[count];
            _fromX = new float[count];
            _toX = new float[count];
            _fromAlpha = new float[count];
            _toAlpha = new float[count];

            for (int i = 0; i < count; i++)
            {
                if (!groups[i].TryGetComponent(out _canvasGroups[i]))
                    _canvasGroups[i] = groups[i].gameObject.AddComponent<CanvasGroup>();
            }

            _baseX = groups[0].anchoredPosition.x;
            _step = groups[1].anchoredPosition.x - _baseX;
            _initialized = true;

            Snap();
        }

        private void OnEnable()
        {
            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            UpdateButtons();
        }

        private void OnDisable()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);

            if (!_initialized) return;
            _motion.TryCancel();
            Snap();
        }

        public void Next() => MoveTo(_first + 1);

        public void Previous() => MoveTo(_first - 1);

        private void MoveTo(int first)
        {
            if (!_initialized) return;

            first = Mathf.Clamp(first, 0, MaxFirst);
            if (first == _first) return;

            _first = first;
            UpdateButtons();

            _motion.TryCancel();
            for (int i = 0; i < groups.Length; i++)
            {
                _fromX[i] = groups[i].anchoredPosition.x;
                _fromAlpha[i] = _canvasGroups[i].alpha;
                _toX[i] = SlotX(i);
                _toAlpha[i] = IsVisible(i) ? 1f : 0f;
                SetInteractable(i, IsVisible(i));
            }

            _motion = LMotion.Create(0f, 1f, duration)
                .WithEase(ease)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, static (t, self) => self.Apply(t));
        }

        private void Apply(float t)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                Vector2 position = groups[i].anchoredPosition;
                position.x = Mathf.LerpUnclamped(_fromX[i], _toX[i], t);
                groups[i].anchoredPosition = position;
                _canvasGroups[i].alpha = Mathf.LerpUnclamped(_fromAlpha[i], _toAlpha[i], t);
            }
        }

        private void Snap()
        {
            for (int i = 0; i < groups.Length; i++)
            {
                Vector2 position = groups[i].anchoredPosition;
                position.x = SlotX(i);
                groups[i].anchoredPosition = position;
                _canvasGroups[i].alpha = IsVisible(i) ? 1f : 0f;
                SetInteractable(i, IsVisible(i));
            }
        }

        private float SlotX(int index) => _baseX + (index - _first) * _step;

        private bool IsVisible(int index) => index >= _first && index < _first + visibleCount;

        private void SetInteractable(int index, bool value)
        {
            _canvasGroups[index].interactable = value;
            _canvasGroups[index].blocksRaycasts = value;
        }

        private void UpdateButtons()
        {
            if (!_initialized) return;
            if (previousButton != null) previousButton.interactable = _first > 0;
            if (nextButton != null) nextButton.interactable = _first < MaxFirst;
        }
    }
}
