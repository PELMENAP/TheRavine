using System.Collections.Generic;

public class LegPool
{
    private readonly List<LegData> legs;

    public LegPool(int capacity, int resolution)
    {
        legs = new List<LegData>(capacity);
        for (int i = 0; i < capacity; i++)
        {
            LegData leg = new LegData();
            leg.Initialize(resolution);
            legs.Add(leg);
        }
    }

    public LegData Get()
    {
        for (int i = 0; i < legs.Count; i++)
        {
            if (legs[i].state == LegState.Disabled) return legs[i];
        }
        return null;
    }

    public List<LegData> GetAll() => legs;
}