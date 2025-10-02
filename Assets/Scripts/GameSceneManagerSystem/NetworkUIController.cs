using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System;
using System.Collections.Generic;

using TMPro;
using Cysharp.Threading.Tasks;
public class NetworkUIController : MonoBehaviour
{
    [Header("Network Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;

    [Header("IP Configuration")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI currentIPText;
    [SerializeField] private Button refreshIPButton;

    [Header("Settings")]
    [SerializeField] private int targetSceneIndex;
    [SerializeField] private UnityTransport transport;

    private INetworkConnectionManager connectionManager;
    private INetworkTransportConfig transportConfig;
    private SceneLoader sceneLoader;

    private readonly List<Action> eventUnsubscribers = new();

    private void Awake()
    {
        InitializeDependencies();
        SetupUI();
    }

    private void Start()
    {
        InitializeIPConfiguration();
        SubscribeToNetworkEvents();
    }

    private void InitializeDependencies()
    {
        transportConfig = new NetworkTransportConfig(transport);
        sceneLoader = new SceneLoader();
        connectionManager = new NetworkConnectionManager(sceneLoader, targetSceneIndex);
    }

    private void SetupUI()
    {
        hostButton?.onClick.AddListener(() => StartNetworkModeAsync(connectionManager.StartHostAsync).Forget());
        serverButton.onClick.AddListener(() => StartNetworkModeAsync(connectionManager.StartServerAsync).Forget());
        clientButton.onClick.AddListener(() => StartNetworkModeAsync(connectionManager.StartClientAsync).Forget());
        disconnectButton?.onClick.AddListener(connectionManager.Disconnect);
        
        SetButtonsInteractable(true);

        ipInputField.onEndEdit.AddListener(OnIPInputChanged);
        refreshIPButton?.onClick.AddListener(RefreshLocalIP);
    }

    private void InitializeIPConfiguration()
    {
        RefreshLocalIP();
        ipInputField.text = transportConfig.IPAddress;
    }

    private void SubscribeToNetworkEvents()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            eventUnsubscribers.Add(() => NetworkManager.Singleton.OnServerStarted -= OnServerStarted);
            eventUnsubscribers.Add(() => NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected);
            eventUnsubscribers.Add(() => NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected);
        }
    }

    private async UniTaskVoid StartNetworkModeAsync(Func<UniTask<bool>> startAction)
    {
        SetButtonsInteractable(false);

        bool success = await startAction.Invoke();
        
        if (!success)
        {
            Debug.LogError("Failed to start network mode");
            SetButtonsInteractable(true);
        }
    }

    private void OnIPInputChanged(string newIP)
    {
        if (string.IsNullOrWhiteSpace(newIP))
            return;

        if (transportConfig.IsValidIP(newIP))
        {
            transportConfig.IPAddress = newIP;
            UpdateIPDisplay();
            Debug.Log($"IP address updated to: {newIP}");
        }
        else
        {
            Debug.LogWarning($"Invalid IP address: {newIP}");
            ipInputField.text = transportConfig.IPAddress;
        }
    }

    private void RefreshLocalIP()
    {
        try
        {
            string localIP = transportConfig.GetLocalIPAddress();
            transportConfig.IPAddress = localIP;
            UpdateIPDisplay();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to get local IP: {ex.Message}");
        }
    }

    private void UpdateIPDisplay()
    {
        if (currentIPText != null)
        {
            currentIPText.text = $"IP: {transportConfig.IPAddress}";
        }
    }

    private void OnServerStarted()
    {
        Debug.Log("Server started successfully");
        UpdateIPDisplay();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetButtonsInteractable(true);
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        hostButton.interactable = interactable;
        serverButton.interactable = interactable;
        clientButton.interactable = interactable;
        disconnectButton.interactable = !interactable;
    }

    private void OnDestroy()
    {
        foreach (var unsubscriber in eventUnsubscribers)
        {
            try
            {
                unsubscriber?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error unsubscribing from event: {ex.Message}");
            }
        }
        
        eventUnsubscribers.Clear();
    }
}