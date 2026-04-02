using TheRavine.Extensions;

public struct SafeInt
{
    private int _encoded;
    private int _salt;

    public SafeInt(int value)
    {
        _salt = RavineRandom.RangeInt(int.MinValue / 4, int.MaxValue / 4);
        _encoded = value ^ _salt;
    }

    public static implicit operator int(SafeInt s) => s._encoded ^ s._salt;
    public static implicit operator SafeInt(int v) => new SafeInt(v);

    public override string ToString() => ((int)this).ToString();
}