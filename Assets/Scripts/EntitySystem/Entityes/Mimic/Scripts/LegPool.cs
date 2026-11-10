using System.Collections.Generic;

public class LegPool
{
    private readonly List<LegData> legs;

    public LegPool(int capacity)
    {
        legs = new List<LegData>(capacity);
        for (int i = 0; i < capacity; i++) legs.Add(new LegData());
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
