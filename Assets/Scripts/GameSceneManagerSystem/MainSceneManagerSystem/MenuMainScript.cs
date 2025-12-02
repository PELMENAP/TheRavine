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
        public Button[] activationButton;

        public MenuSectionData(MenuSection type, GameObject obj, Button[] btn)
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
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button testStartGameButton;

        private readonly ReactiveProperty<MenuSection> currentSection = new(MenuSection.Menu);
        private readonly ReactiveProperty<bool> isLoading = new(true);
        private readonly CompositeDisposable disposables = new();

        private readonly Dictionary<MenuSection, MenuSectionData> sectionLookup = new();

        private SceneLaunchService sceneLaunch;

        private void Awake()
        {
            InitializeSceneLauncher();
            InitializeComponents();
            SetupSectionSystem();
            StartInitialization();
        }

        private void InitializeSceneLauncher()
        {
            sceneLaunch = new SceneLaunchService(
                gameSceneIndex,
                testSceneIndex,
                new SceneLoader(),
                cameraData,
                FaderOnTransit.Instance.GetFaderCamera(),
                isLoading
            );

            ServiceLocator.Services.Register(sceneLaunch);
        }

        private void InitializeComponents()
        {
            foreach (var section in menuSections)
                sectionLookup[section.sectionType] = section;

            testStartGameButton?.onClick.AddListener(() =>
            {
                if (sceneLaunch.CanLaunch)
                    sceneLaunch.LaunchTest().Forget();
            });

            quitGameButton.onClick.AddListener(QuitGame);
        }

        private void SetupSectionSystem()
        {
            currentSection
                .Subscribe(OnSectionChanged)
                .AddTo(disposables);

            foreach (var section in menuSections)
            {
                if (section.activationButton == null) continue;

                var type = section.sectionType;

                for(int i = 0; i < section.activationButton.Length; i++)
                {   
                    section.activationButton[i].onClick.AddListener(() =>
                    {
                        if (!isLoading.Value)
                            SwitchToSection(type);
                    });
                }
            }
        }

        private void OnSectionChanged(MenuSection newSection)
        {
            foreach (var section in menuSections)
                section.sectionObject?.SetActive(false);

            if (sectionLookup.TryGetValue(newSection, out var d))
            {
                d.sectionObject?.SetActive(true);
                d.sectionObject?.transform.SetAsLastSibling();
            }
        }

        private void StartInitialization()
        {
            AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
            FaderOnTransit.Instance.FadeOut(() =>
            {
                isLoading.Value = false;
                SwitchToSection(MenuSection.Menu);
            });

            SwitchToSection(MenuSection.Menu);
        }

        private void SwitchToSection(MenuSection s)
        {
            if (!isLoading.Value)
                currentSection.Value = s;
        }

        private void AddCameraToStack(Camera cam)
        {
            if (cam != null && cameraData != null)
                cameraData.cameraStack.Add(cam);
        }

        private void QuitGame()
        {
            if (isLoading.Value) return;

    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
        }

        private void OnDestroy()
        {
            disposables.Dispose();
            currentSection.Dispose();
            isLoading.Dispose();
        }
    }

}