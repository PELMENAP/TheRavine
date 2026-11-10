using Unity.Mathematics;

public sealed class WarmthField
{
    private readonly float[] _heat;
    private readonly int     _mask;
    private readonly float   _invCell;
    private float _scale = 1f;

    private const float RenormalizeBelow = 1e-12f;

    public float Total { get; private set; }

    public WarmthField(float cellSize, int buckets)
    {
        int n = math.ceilpow2(math.max(buckets, 1));
        _heat    = new float[n];
        _mask    = n - 1;
        _invCell = 1f / math.max(cellSize, 0.01f);
    }

    public int Index(float2 position)
        => EntitySpatialGrid.HashCell((int)math.floor(position.x * _invCell), (int)math.floor(position.y * _invCell), _mask);

    public float Read(int index) => _heat[index] * _scale;

    public void Add(int index, float amount)
    {
        _heat[index] += amount / _scale;
        Total        += amount;
    }

    public float Decay(float factor)
    {
        float lost = Total * (1f - factor);
        Total  *= factor;
        _scale *= factor;
        if (_scale < RenormalizeBelow) Renormalize();
        return lost;
    }

    private void Renormalize()
    {
        float total = 0f;
        for (int i = 0; i < _heat.Length; i++)
        {
            float h = _heat[i];
            if (h == 0f) continue;
            h *= _scale;
            total += h;
            _heat[i] = h;
        }
        Total  = total;
        _scale = 1f;
    }
}
