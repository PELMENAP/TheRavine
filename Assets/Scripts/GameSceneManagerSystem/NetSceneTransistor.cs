using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System;
using System.Net;
using System.Net.Sockets;

using TMPro;
using Cysharp.Threading.Tasks;
public class NetSceneTransistor : MonoBehaviour
{
    [SerializeField] private Button host;
    [SerializeField] private Button server;
    [SerializeField] private Button client;
    [SerializeField] private int sceneIndex;

    private SceneTransistor sceneTransistor;

    private void Awake()
    {
        sceneTransistor = new SceneTransistor();
        
        host.onClick.AddListener(() => LoadSceneAndStart(NetworkManager.Singleton.StartHost));
        server.onClick.AddListener(() => LoadSceneAndStart(NetworkManager.Singleton.StartServer));
        client.onClick.AddListener(() => LoadSceneAndStart(NetworkManager.Singleton.StartClient));
    }

    private void LoadSceneAndStart(Func<bool> startAction)
    {
        LoadSceneAndExecute(sceneIndex, startAction).Forget();
    }

    private async UniTaskVoid LoadSceneAndExecute(int sceneIndex, Func<bool> startAction)
    {
        await sceneTransistor.LoadScene(sceneIndex);

        bool result = startAction.Invoke();
        if (!result)
        {
            Debug.LogError("Failed to start NetworkManager.");
        }
    }
    

    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI currentIPText;
    [SerializeField] private UnityTransport transport;

    private void Start()
    {
        ipInputField.onEndEdit.AddListener(OnEndEditIP);
        ipInputField.text = transport.ConnectionData.Address;

        NetworkManager.Singleton.OnServerStarted += DisplayCurrentIP;

        DisplayLocalIPAddress();
    }

    private void OnEndEditIP(string ip)
    {
        if (IsValidIP(ip))
        {
            transport.ConnectionData.Address = ip;
            Debug.Log($"IP address set to: {ip}");
        }
        else
        {
            Debug.LogError("Invalid IP address entered.");
        }
    }

    private bool IsValidIP(string ip)
    {
        // Проверка формата IP адреса
        System.Net.IPAddress address;
        return System.Net.IPAddress.TryParse(ip, out address);
    }

    private void DisplayCurrentIP()
    {
        string currentIP = transport.ConnectionData.Address;
        currentIPText.text = $"Current IP: {currentIP}";
        Debug.Log($"Current IP address displayed: {currentIP}");
    }

    private void DisplayLocalIPAddress()
    {
        string localIP = GetLocalIPAddress();
        transport.ConnectionData.Address = localIP;
        currentIPText.text = $"Local IP: {localIP}";
        Debug.Log($"Local IP: {localIP}");
        ipInputField.text = "";
    }

    private string GetLocalIPAddress()
    {
        foreach (var networkInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            {
                foreach (var addressInfo in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return addressInfo.Address.ToString();
                    }
                }
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    private void OnDisable() {
        NetworkManager.Singleton.OnServerStarted -= DisplayCurrentIP;
    }
}
