using Unity.Netcode;

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

    public bool TryServerAdd(int delta)
    {
        if (!IsServer) return false;

        long next = (long)_networkCurrency.Value + delta;
        if (next < 0 || next > int.MaxValue) return false;

        _networkCurrency.Value = (int)next;
        return true;
    }

    public void Dispose()
    {
        _networkCurrency.OnValueChanged -= OnServerValueChanged;
    }
}