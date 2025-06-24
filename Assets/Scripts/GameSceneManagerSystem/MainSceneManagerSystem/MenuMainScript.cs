using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.Base
{
    public enum MenuSection
    {
        Menu,
        Settings,
        Multiplayer,
        Loading
    }
    [Serializable]
    public struct MenuSectionData
    {
        public MenuSection sectionType;
        public GameObject sectionObject;
        public Button activationButton;

        public MenuSectionData(MenuSection type, GameObject obj, Button btn)
        {
            sectionType = type;
            sectionObject = obj;
            activationButton = btn;
        }
    }

    public class MenuMainScript : MonoBehaviour
    {
        [Header("Menu Sections")]
        [SerializeField] private MenuSectionData[] menuSections;

        [Header("Camera")]
        [SerializeField] private UniversalAdditionalCameraData cameraData;

        [Header("Scene Loading")]
        [SerializeField] private int gameSceneIndex = 2;
        [SerializeField] private int testSceneIndex = 2;
        [Header("Buttons")]
        [SerializeField] private Button fastStartGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button testStartGameButton;
        private readonly ReactiveProperty<MenuSection> currentSection = new(MenuSection.Menu);
        private readonly ReactiveProperty<bool> isLoading = new(true);
        private readonly CompositeDisposable disposables = new();
        private readonly SceneTransistor transistor = new();
        private readonly Dictionary<MenuSection, MenuSectionData> sectionLookup = new();

        private void Awake()
        {
            InitializeComponents();
            SetupSectionSystem();
            StartInitialization();
        }

        private void InitializeComponents()
        {
            foreach (var section in menuSections)
            {
                sectionLookup[section.sectionType] = section;
            }

            fastStartGameButton.onClick.AddListener(StartGame);
            fastStartGameButton.onClick.AddListener(QuitGame);
            testStartGameButton.onClick.AddListener(LoadTestScene);
        }

        private void SetupSectionSystem()
        {
            currentSection
                .Subscribe(OnSectionChanged)
                .AddTo(disposables);

            foreach (var section in menuSections)
            {
                if (section.activationButton != null)
                {
                    var sectionType = section.sectionType;
                    section.activationButton.onClick.AddListener(() =>
                    {
                        if (!isLoading.CurrentValue)
                            SwitchToSection(sectionType);
                    });
                }
            }
        }

        private void OnSectionChanged(MenuSection newSection)
        {
            foreach (var section in menuSections)
            {
                if (section.sectionObject != null)
                {
                    section.sectionObject.SetActive(false);
                }
            }
            if (sectionLookup.TryGetValue(newSection, out var activeSection) &&
                activeSection.sectionObject != null)
            {
                activeSection.sectionObject.SetActive(true);
                activeSection.sectionObject.transform.SetAsLastSibling();
            }
        }

        private void StartInitialization()
        {
            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());

            FaderOnTransit.instance.FadeOut(() => { isLoading.Value = false; SwitchToSection(MenuSection.Menu); });
        }
        private void SwitchToSection(MenuSection section)
        {
            if (isLoading.CurrentValue) return;

            currentSection.Value = section;
        }
        private async void StartGame()
        {
            if (isLoading.CurrentValue) return;

            await LoadGameScene(gameSceneIndex);
        }
        private async void LoadTestScene()
        {
            if (isLoading.CurrentValue) return;
            await LoadGameScene(testSceneIndex);
        }

        private async UniTask LoadGameScene(int SceneIndex)
        {
            isLoading.Value = true;
            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());

            try
            {
                await transistor.LoadScene(SceneIndex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка загрузки сцены: {ex.Message}");
                isLoading.Value = false;
            }
        }

        private void AddCameraToStack(Camera cameraToAdd)
        {
            if (cameraToAdd != null && cameraData != null)
            {
                cameraData.cameraStack.Add(cameraToAdd);
            }
        }

        private void QuitGame()
        {
            if (isLoading.CurrentValue) return;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            disposables?.Dispose();
            currentSection?.Dispose();
            isLoading?.Dispose();
        }
    }
}