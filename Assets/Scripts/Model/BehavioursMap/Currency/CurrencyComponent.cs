using R3;

public sealed class CurrencyComponent : IComponent
{
    private SafeInt _safe;
    private readonly ReactiveProperty<int> _amount;

    public ReadOnlyReactiveProperty<int> Amount { get; }

    public CurrencyComponent(int initialAmount = 0)
    {
        _safe = initialAmount;
        _amount = new ReactiveProperty<int>(initialAmount);
        Amount = _amount.ToReadOnlyReactiveProperty();
    }

    public static readonly object AccessToken = new();

    public void ApplyServerValue(object token, int confirmedValue)
    {
        if (token != AccessToken) return;
        if (confirmedValue < 0) return;
        _safe = confirmedValue;
        _amount.Value = confirmedValue;
    }

    public int GetRaw() => (int)_safe;

    public void Dispose() => _amount.Dispose();
}