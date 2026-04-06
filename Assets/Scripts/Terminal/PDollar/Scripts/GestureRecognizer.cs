using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace TheRavine.Extensions
{
    public class GestureRecognizer : MonoBehaviour
    {
        [Header("References")]
        public Transform gestureOnScreenPrefab;
        public InputActionReference pointerPosition;
        public InputActionReference pointerContact;
        public RectTransform drawAreaRect;
        public TextMeshProUGUI messageText;
        public TMP_InputField newGestureNameInput;
        public Button recognizeButton;
        public Button addGestureButton;
        public GestureLibraryView libraryView;

        private readonly GestureRepository _repository = new();
        private readonly List<Point> _points = new();
        private readonly List<LineRenderer> _gestureLines = new();

        private LineRenderer _currentLine;
        private int _strokeId = -1;
        private bool _recognized;
        private bool _isPointerDown;
        private Vector2 _pointerPos;
        private Camera _mainCamera;

        private void OnEnable()
        {
            pointerPosition.action.Enable();
            pointerContact.action.Enable();

            recognizeButton.onClick.AddListener(RecognizeGesture);
            addGestureButton.onClick.AddListener(AddGesture);

            _mainCamera = Camera.main;
        }

        private void OnDestroy()
        {
            pointerPosition.action.Disable();
            pointerContact.action.Disable();

            recognizeButton.onClick.RemoveListener(RecognizeGesture);
            addGestureButton.onClick.RemoveListener(AddGesture);
        }

        private void Start()
        {
            messageText.text = "";
            _repository.Load();
            libraryView.Initialize(_repository);
        }

        private void Update()
        {
            _pointerPos = pointerPosition.action.ReadValue<Vector2>();

            bool isPressed = pointerContact.action.IsPressed();
            bool insideArea = drawAreaRect.rect.Contains(drawAreaRect.InverseTransformPoint(_pointerPos));

            if (!isPressed)
            {
                _isPointerDown = false;
                return;
            }

            if (!insideArea)
            {
                _isPointerDown = true;
                return;
            }

            if (!_isPointerDown)
            {
                _isPointerDown = true;
                HandlePointerDown();
            }

            HandlePointerDrag();
        }

        private void HandlePointerDown()
        {
            if (_recognized)
                ClearCurrentGesture();

            ++_strokeId;

            Transform tmpGesture = Instantiate(gestureOnScreenPrefab, transform.position, transform.rotation);
            _currentLine = tmpGesture.GetComponent<LineRenderer>();
            _gestureLines.Add(_currentLine);
        }

        private void HandlePointerDrag()
        {
            _points.Add(new Point(_pointerPos.x, -_pointerPos.y, _strokeId));

            int vertexCount = _currentLine.positionCount + 1;
            _currentLine.positionCount = vertexCount;
            _currentLine.SetPosition(
                vertexCount - 1,
                _mainCamera.ScreenToWorldPoint(new Vector3(_pointerPos.x, _pointerPos.y, 10))
            );
        }

        private void ClearCurrentGesture()
        {
            _recognized = false;
            _strokeId = -1;
            _points.Clear();

            foreach (LineRenderer line in _gestureLines)
            {
                line.positionCount = 0;
                Destroy(line.gameObject);
            }

            _gestureLines.Clear();
        }

        private void RecognizeGesture()
        {
            if (_points.Count == 0)
            {
                messageText.text = "Нарисуйте жест перед распознаванием";
                return;
            }

            _recognized = true;

            Gesture candidate = new Gesture(_points.ToArray(), "");
            Result gestureResult = PointCloudRecognizerPlus.Classify(candidate, _repository.GetGesturesArray());

            string gestureName = gestureResult.GestureClass;

            if (gestureName.StartsWith("~", StringComparison.Ordinal))
            {
                messageText.text = $"Команда: {gestureName} ({gestureResult.Score:F2})";
                GestureCommandBus.Dispatch(gestureName);
                return;
            }

            messageText.text = $"{gestureName} {gestureResult.Score:F2}";
        }

        private void AddGesture()
        {
            string gestureName = newGestureNameInput.text;

            if (_points.Count == 0 || string.IsNullOrEmpty(gestureName))
            {
                messageText.text = "Нарисуйте жест и введите его название перед добавлением";
                return;
            }

            _repository.Add(_points.ToArray(), gestureName);
            newGestureNameInput.text = "";
            messageText.text = $"Жест '{gestureName}' успешно добавлен";
        }
    }
}