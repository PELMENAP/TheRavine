using System;
using System.Collections.Generic;
using System.IO;
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

		private List<Gesture> trainingSet = new List<Gesture>();
		private List<Point> points = new List<Point>();
		private int strokeId = -1;

		private Vector2 virtualKeyPosition = Vector2.zero;
		private Rect drawArea;

		private List<LineRenderer> gestureLinesRenderer = new List<LineRenderer>();
		private LineRenderer currentGestureLineRenderer;

		private bool recognized;

		private void Awake()
		{
			pointerPosition.action.Enable();
			pointerContact.action.Enable();
			
			recognizeButton.onClick.AddListener(RecognizeGesture);
			addGestureButton.onClick.AddListener(AddGesture);
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
			drawArea = new Rect(0, 0, Screen.width - Screen.width / 3, Screen.height);
			
			messageText.text = "";

			LoadGestures();
		}

		private void LoadGestures()
		{
			TextAsset[] gesturesXml = Resources.LoadAll<TextAsset>("GestureSet/");
			foreach (TextAsset gestureXml in gesturesXml)
			{
				trainingSet.Add(GestureIO.ReadGestureFromXML(gestureXml.text));
			}

			string[] filePaths = Directory.GetFiles(Application.persistentDataPath, "*.xml");
			foreach (string filePath in filePaths)
			{
				trainingSet.Add(GestureIO.ReadGestureFromFile(filePath));
			}
		}

		private void Update()
		{
			virtualKeyPosition = pointerPosition.action.ReadValue<Vector2>();
			
			if (drawAreaRect.rect.Contains(drawAreaRect.InverseTransformPoint(virtualKeyPosition)))
			{
				if (pointerContact.action.WasPressedThisFrame())
				{
					HandlePointerDown();
				}
				if (pointerContact.action.IsPressed())
				{
					HandlePointerDrag();
				}
			}
		}

		private void HandlePointerDown()
		{
			if (recognized)
			{
				ClearCurrentGesture();
			}

			++strokeId;

			Transform tmpGesture = Instantiate(gestureOnScreenPrefab, transform.position, transform.rotation);
			currentGestureLineRenderer = tmpGesture.GetComponent<LineRenderer>();
			gestureLinesRenderer.Add(currentGestureLineRenderer);
		}

		private void HandlePointerDrag()
		{
			points.Add(new Point(virtualKeyPosition.x, -virtualKeyPosition.y, strokeId));
			
			int vertexCount = currentGestureLineRenderer.positionCount + 1;
			currentGestureLineRenderer.positionCount = vertexCount;
			currentGestureLineRenderer.SetPosition(
				vertexCount - 1, 
				Camera.main.ScreenToWorldPoint(new Vector3(virtualKeyPosition.x, virtualKeyPosition.y, 10))
			);
		}

		private void ClearCurrentGesture()
		{
			recognized = false;
			strokeId = -1;
			points.Clear();

			foreach (LineRenderer lineRenderer in gestureLinesRenderer)
			{
				lineRenderer.positionCount = 0;
				Destroy(lineRenderer.gameObject);
			}

			gestureLinesRenderer.Clear();
		}

		private void RecognizeGesture()
		{
			if (points.Count == 0)
			{
				messageText.text = "Нарисуйте жест перед распознаванием";
				return;
			}
			
			recognized = true;

			Gesture candidate = new Gesture(points.ToArray(), "", true);
			Result gestureResult = PointCloudRecognizerPlus.Classify(candidate, trainingSet.ToArray());

			messageText.text = $"{gestureResult.GestureClass} {gestureResult.Score}";
		}

		private void AddGesture()
		{
			string newGestureName = newGestureNameInput.text;
			
			if (points.Count > 0 && !string.IsNullOrEmpty(newGestureName))
			{
				string fileName = $"{Application.persistentDataPath}/{newGestureName}-{DateTime.Now.ToFileTime()}.xml";

				GestureIO.WriteGesture(points.ToArray(), newGestureName, fileName);
				trainingSet.Add(new Gesture(points.ToArray(), newGestureName));

				newGestureNameInput.text = "";
				messageText.text = $"Жест '{newGestureName}' успешно добавлен";
			}
			else
			{
				messageText.text = "Нарисуйте жест и введите его название перед добавлением";
			}
		}
	}
}