using Unity.Netcode;
using UnityEngine;

public sealed class CurrencyNetworkComponent : NetworkBehaviour, IComponent
{
    private NetworkVariable<int> _networkCurrency = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private CurrencyComponent _currencyComponent;

    public void Initialize(CurrencyComponent component)
    {
        _currencyComponent = component;
        _networkCurrency.OnValueChanged += OnServerValueChanged;
    }

    private void OnServerValueChanged(int prev, int next)
    {
        _currencyComponent?.ApplyServerValue(CurrencyComponent.AccessToken, next);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestUpdateRpc(int newValue)
    {
        int clamped = Mathf.Max(0, newValue);
        _networkCurrency.Value = clamped;
    }

    public void Dispose()
    {
        _networkCurrency.OnValueChanged -= OnServerValueChanged;
    }
}