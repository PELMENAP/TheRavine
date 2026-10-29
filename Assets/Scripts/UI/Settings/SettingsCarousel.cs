using System;
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
        private Action _onComplete;

        private bool CanScroll => _initialized && groups.Length > visibleCount;

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
            _onComplete = Snap;
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

        public void Next() => MoveBy(1);

        public void Previous() => MoveBy(-1);

        private void MoveBy(int direction)
        {
            if (!CanScroll) return;

            int count = groups.Length;
            int oldFirst = _first;
            _first = Wrap(_first + direction, count);

            _motion.TryCancel();
            for (int i = 0; i < count; i++)
            {
                int oldOffset = Wrap(i - oldFirst, count);
                int newOffset = Wrap(i - _first, count);

                float fromX = groups[i].anchoredPosition.x;
                float fromAlpha = _canvasGroups[i].alpha;
                int targetOffset = newOffset;

                if (direction > 0)
                {
                    if (oldOffset == 0)
                        targetOffset = -1;
                    else if (newOffset == visibleCount - 1 && oldOffset >= visibleCount && fromAlpha <= 0.01f)
                        fromX = OffsetX(visibleCount);
                }
                else if (newOffset == 0 && oldOffset >= visibleCount && fromAlpha <= 0.01f)
                {
                    fromX = OffsetX(-1);
                }

                bool visible = newOffset < visibleCount;
                _fromX[i] = fromX;
                _fromAlpha[i] = fromAlpha;
                _toX[i] = OffsetX(targetOffset);
                _toAlpha[i] = visible ? 1f : 0f;
                SetInteractable(i, visible);
            }

            _motion = LMotion.Create(0f, 1f, duration)
                .WithEase(ease)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(_onComplete)
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
            int count = groups.Length;
            for (int i = 0; i < count; i++)
            {
                int offset = Wrap(i - _first, count);
                bool visible = offset < visibleCount;

                Vector2 position = groups[i].anchoredPosition;
                position.x = OffsetX(offset);
                groups[i].anchoredPosition = position;
                _canvasGroups[i].alpha = visible ? 1f : 0f;
                SetInteractable(i, visible);
            }
        }

        private static int Wrap(int value, int count) => (value % count + count) % count;

        private float OffsetX(int offset) => _baseX + offset * _step;

        private void SetInteractable(int index, bool value)
        {
            _canvasGroups[index].interactable = value;
            _canvasGroups[index].blocksRaycasts = value;
        }

        private void UpdateButtons()
        {
            bool canScroll = CanScroll;
            if (previousButton != null) previousButton.interactable = canScroll;
            if (nextButton != null) nextButton.interactable = canScroll;
        }
    }
}
