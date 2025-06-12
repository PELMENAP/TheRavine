using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using R3;

using TheRavine.Base;
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
    private readonly ReactiveProperty<MenuSection> currentSection = new(MenuSection.Menu);
    private readonly ReactiveProperty<bool> isLoading = new(true);
    
    private SceneTransistor transistor;
    private Dictionary<MenuSection, MenuSectionData> sectionLookup;
    private CompositeDisposable disposables = new();

    private void Awake()
    {
        InitializeComponents();
        SetupSectionSystem();
        StartInitialization();
    }

    private void InitializeComponents()
    {
        transistor = new SceneTransistor();
        sectionLookup = new Dictionary<MenuSection, MenuSectionData>();
        foreach (var section in menuSections)
        {
            sectionLookup[section.sectionType] = section;
        }
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

    private async void StartInitialization()
    {
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        
        FaderOnTransit.instance.FadeOut(() => { isLoading.Value = false; SwitchToSection(MenuSection.Menu) ;});
    }
    public void SwitchToSection(MenuSection section)
    {
        if (isLoading.CurrentValue) return;
        
        currentSection.Value = section;
    }
    public async void StartGame()
    {
        if (isLoading.CurrentValue) return;
        
        DataStorage.cycleCount = 0;
        await LoadGameScene();
    }

    public async void LoadGame()
    {
        if (isLoading.CurrentValue) return;
        await LoadGameScene();
    }

    public async void LoadTestScene()
    {
        if (isLoading.CurrentValue) return;
        await LoadGameScene();
    }

    private async UniTask LoadGameScene()
    {
        isLoading.Value = true;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        
        try
        {
            await transistor.LoadScene(gameSceneIndex);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ошибка загрузки сцены: {ex.Message}");
            isLoading.Value = false;
        }
    }

    public void AddCameraToStack(Camera cameraToAdd)
    {
        if (cameraToAdd != null && cameraData != null)
        {
            cameraData.cameraStack.Add(cameraToAdd);
        }
    }

    public void QuitGame()
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