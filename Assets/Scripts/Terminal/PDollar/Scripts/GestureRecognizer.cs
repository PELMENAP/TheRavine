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

        public InputActionReference gestureAreaEnable;
        public InputActionReference gestureRecognizeEnable;

        public RectTransform drawAreaRect;
        public GameObject gestureSettings;
        public TextMeshProUGUI messageText;
        public TMP_InputField newGestureNameInput;
        public Button enableGestureSettings;
        public Button addGestureButton;
        public GestureLibraryView libraryView;

        private readonly GestureRepository repository = new();
        private readonly List<Point> points = new();
        private List<LineRenderer> currentGestureLines = new();
        private readonly Stack<List<LineRenderer>> gestureHistory = new();

        private LineRenderer currentLine;
        private int strokeId = -1;
        private bool isInitialized;
        private bool isPhysicallyDown;
        private bool isStrokeActive;
        private Vector2 pointerPos;
        private Camera mainCamera;

        private void Awake()
        {
            addGestureButton.onClick.AddListener(AddGesture);
            enableGestureSettings.onClick.AddListener(ChangeSettings);

            gestureAreaEnable.action.performed += ToggleDrawArea;
            gestureRecognizeEnable.action.performed += RecognizeGesture;

            gestureSettings.SetActive(false);
        }

        private void OnDestroy()
        {
            addGestureButton.onClick.RemoveListener(AddGesture);
            enableGestureSettings.onClick.RemoveListener(ChangeSettings);

            gestureAreaEnable.action.performed -= ToggleDrawArea;
            gestureRecognizeEnable.action.performed -= RecognizeGesture;
        }

        public void ChangeSettings()
        {
            if (!isInitialized) return;

            bool turningOn = !gestureSettings.activeSelf;
            gestureSettings.SetActive(turningOn);
            drawAreaRect.gameObject.SetActive(turningOn);

            if (!turningOn)
                ArchiveCurrentGesture();
        }

        private void Start()
        {
            messageText.text = "";
            drawAreaRect.gameObject.SetActive(false);
            repository.Load();
            libraryView.Initialize(repository);

            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ => GetCameraToService(ServiceLocator.Players.GetAllPlayers()).Forget());
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
                await UniTask.Yield();

            return aEntity.GetEntityComponent<T>();
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (drawAreaRect.gameObject.activeSelf)
                HandleDrawingInput();
        }

        private void ToggleDrawArea(InputAction.CallbackContext callbackContext)
        {
            bool turningOn = !drawAreaRect.gameObject.activeSelf;
            drawAreaRect.gameObject.SetActive(turningOn);

            if (!turningOn)
                ArchiveCurrentGesture();
        }

        private void HandleDrawingInput()
        {
            pointerPos = pointerPosition.action.ReadValue<Vector2>();

            bool isPressed = pointerContact.action.IsPressed();
            bool insideArea = drawAreaRect.rect.Contains(drawAreaRect.InverseTransformPoint(pointerPos));

            if (!isPressed)
            {
                isPhysicallyDown = false;
                isStrokeActive = false;
                return;
            }

            if (!insideArea)
            {
                isPhysicallyDown = true;
                return;
            }

            if (!isPhysicallyDown)
            {
                isPhysicallyDown = true;
                isStrokeActive = true;
                HandlePointerDown();
            }

            if (isStrokeActive)
                HandlePointerDrag();
        }

        private void HandlePointerDown()
        {
            ++strokeId;

            Transform tmpGesture = Instantiate(gestureOnScreenPrefab, transform.position, transform.rotation, transform);
            currentLine = tmpGesture.GetComponent<LineRenderer>();
            currentGestureLines.Add(currentLine);
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

        private void ArchiveCurrentGesture()
        {
            if (currentGestureLines.Count == 0) return;

            gestureHistory.Push(new List<LineRenderer>(currentGestureLines));

            currentGestureLines.Clear();
            points.Clear();
            strokeId = -1;
        }

        private void DestroyCurrentGesture()
        {
            foreach (LineRenderer line in currentGestureLines)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }

            currentGestureLines.Clear();
            points.Clear();
            strokeId = -1;
        }

        private void DestroyArchivedGesture()
        {
            currentGestureLines = gestureHistory.Pop();
            foreach (LineRenderer line in currentGestureLines)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }

            currentGestureLines.Clear();
        }

        private void RecognizeGesture(InputAction.CallbackContext callbackContext)
        {
            if (!drawAreaRect.gameObject.activeSelf) return;

            if (points.Count == 0)
            {
                if (gestureHistory.Count > 0)
                {
                    DestroyArchivedGesture();
                }
                messageText.text = "Нарисуйте жест перед распознаванием";
                return;
            }

            Gesture candidate = new(points.ToArray(), "");
            Result gestureResult = PointCloudRecognizerPlus.Classify(candidate, repository.GetGesturesArray());

            string gestureName = gestureResult.GestureClass;

            messageText.text = gestureName.StartsWith("~", StringComparison.Ordinal)
                ? $"Команда: {gestureName} ({gestureResult.Score:F2})"
                : $"{gestureName} {gestureResult.Score:F2}";

            GestureCommandBus.Dispatch(gestureName);

            DestroyCurrentGesture();
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