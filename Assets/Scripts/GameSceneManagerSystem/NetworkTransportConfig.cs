using Unity.Netcode.Transports.UTP;
using System;
using System.Net.Sockets;

public interface INetworkTransportConfig
{
    string IPAddress { get; set; }
    bool IsValidIP(string ip);
    string GetLocalIPAddress();
}

// Конфигурация транспорта
public class NetworkTransportConfig : INetworkTransportConfig
{
    private readonly UnityTransport transport;

    public NetworkTransportConfig(UnityTransport transport)
    {
        this.transport = transport;
    }

    public string IPAddress
    {
        get => transport.ConnectionData.Address;
        set => transport.ConnectionData.Address = value;
    }

    public bool IsValidIP(string ip)
    {
        return System.Net.IPAddress.TryParse(ip, out _);
    }

    public string GetLocalIPAddress()
    {
        foreach (var networkInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            foreach (var addressInfo in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork && 
                    !System.Net.IPAddress.IsLoopback(addressInfo.Address))
                {
                    return addressInfo.Address.ToString();
                }
            }
        }
        return "127.0.0.1";
    }
}