using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using R3;
using TMPro;
using TheRavine.EntityControl;
using Cysharp.Threading.Tasks;

namespace TheRavine.Extensions
{
    public class GestureRecognizer : MonoBehaviour
    {
        [Header("References")]
        public Transform gestureOnScreenPrefab;
        public InputActionReference pointerPosition;
        public InputActionReference pointerContact;
        
        public InputActionReference gestureRecognizeEnable; 
        
        public RectTransform drawAreaRect;
        public GameObject gestureSettings;
        public TextMeshProUGUI messageText;
        public TMP_InputField newGestureNameInput;
        public Button enableGestureSettings;
        public Button recognizeButton; 
        public Button addGestureButton;
        public GestureLibraryView libraryView;

        private readonly GestureRepository repository = new();
        private readonly List<Point> points = new();
        private readonly List<LineRenderer> gestureLines = new();

        private LineRenderer currentLine;
        private int strokeId = -1;
        private bool recognized;
        private bool isPointerDown;
        private bool isInitialized = false;
        private Vector2 pointerPos;
        private Camera mainCamera;

        private void Awake()
        {
            addGestureButton.onClick.AddListener(AddGesture);
            enableGestureSettings.onClick.AddListener(ChangeSettings);
            recognizeButton.onClick.AddListener(RecognizeGesture); 

            gestureSettings.SetActive(false);
        }

        private void OnDestroy()
        {
            addGestureButton.onClick.RemoveListener(AddGesture);
            enableGestureSettings.onClick.RemoveListener(ChangeSettings);
            recognizeButton.onClick.RemoveListener(RecognizeGesture); 
        }
        public void ChangeSettings()
        {
            gestureSettings.SetActive(!gestureSettings.activeSelf);
            drawAreaRect.gameObject.SetActive(gestureSettings.activeSelf);
        }

        private void Start()
        {
            messageText.text = "";
            drawAreaRect.gameObject.SetActive(false);
            repository.Load();
            libraryView.Initialize(repository);

            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                {
                    GetCameraToService(ServiceLocator.Players.GetAllPlayers()).Forget();
                });
        }

        private async UniTaskVoid GetCameraToService(IReadOnlyList<AEntity> list)
        {
            CameraComponent cameraComponent = await WaitUntilComponentReady<CameraComponent>(list[0]);
            mainCamera = cameraComponent.GetCamera();
            isInitialized = true;
        }

        private async UniTask<T> WaitUntilComponentReady<T>(AEntity aEntity) where T : IComponent
        {
            while (!aEntity.HasComponent<T>())
            {
                await UniTask.Yield();
            }
            return aEntity.GetEntityComponent<T>();
        }

        private bool _isPhysicallyDown;
        private bool _isStrokeActive;

        private void Update()
        {
            if(!isInitialized) return;
            
            if (gestureRecognizeEnable.action.WasPressedThisFrame())
            {
                drawAreaRect.gameObject.SetActive(true);
            }

            if (gestureRecognizeEnable.action.IsPressed() || gestureSettings.activeSelf)
            {
                pointerPos = pointerPosition.action.ReadValue<Vector2>();

                bool isPressed = pointerContact.action.IsPressed();
                bool insideArea = drawAreaRect.rect.Contains(drawAreaRect.InverseTransformPoint(pointerPos));

                if (!isPressed)
                {
                    _isPhysicallyDown = false;
                    _isStrokeActive = false;
                }
                else if (insideArea)
                {
                    if (!_isPhysicallyDown)
                    {
                        _isPhysicallyDown = true;
                        _isStrokeActive = true;
                        HandlePointerDown();
                    }

                    if (_isStrokeActive)
                        HandlePointerDrag();
                }
                else
                {
                    if (!_isPhysicallyDown)
                        _isPhysicallyDown = true;
                }
            }

            if (gestureRecognizeEnable.action.WasReleasedThisFrame())
            {
                RecognizeGesture();
                drawAreaRect.gameObject.SetActive(false);
                ClearCurrentGesture();
            }
        }

        private void HandlePointerDown()
        {
            if (recognized)
                ClearCurrentGesture();

            ++strokeId;

            Transform tmpGesture = Instantiate(gestureOnScreenPrefab, transform.position, transform.rotation, this.transform);
            currentLine = tmpGesture.GetComponent<LineRenderer>();
            gestureLines.Add(currentLine);
        }

        private void HandlePointerDrag()
        {
            points.Add(new Point(pointerPos.x, -pointerPos.y, strokeId));

            int vertexCount = currentLine.positionCount + 1;
            currentLine.positionCount = vertexCount;
            currentLine.SetPosition(
                vertexCount - 1,
                mainCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, 10))
            );
        }

        private void ClearCurrentGesture()
        {
            recognized = false;
            strokeId = -1;
            points.Clear();

            foreach (LineRenderer line in gestureLines)
            {
                if (line != null)
                {
                    line.positionCount = 0;
                    Destroy(line.gameObject);
                }
            }

            gestureLines.Clear();
        }

        private void RecognizeGesture()
        {
            if (points.Count == 0)
            {
                messageText.text = "Нарисуйте жест перед распознаванием";
                return;
            }

            recognized = true;

            Gesture candidate = new Gesture(points.ToArray(), "");
            Result gestureResult = PointCloudRecognizerPlus.Classify(candidate, repository.GetGesturesArray());

            string gestureName = gestureResult.GestureClass;

            if (gestureName.StartsWith("~", StringComparison.Ordinal))
            {
                messageText.text = $"Команда: {gestureName} ({gestureResult.Score:F2})";
            }
            else
            {
                messageText.text = $"{gestureName} {gestureResult.Score:F2}";
            }

            GestureCommandBus.Dispatch(gestureName);
        }

        private void AddGesture()
        {
            string gestureName = newGestureNameInput.text;

            if (points.Count == 0 || string.IsNullOrEmpty(gestureName))
            {
                messageText.text = "Нарисуйте жест и введите его название перед добавлением";
                return;
            }

            repository.Add(points.ToArray(), gestureName);
            newGestureNameInput.text = "";
            messageText.text = $"Жест '{gestureName}' успешно добавлен";
        }
    }
}