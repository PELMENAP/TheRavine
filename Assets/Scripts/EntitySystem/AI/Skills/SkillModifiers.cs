public struct SkillModifiers
{
    public float Speed;
    public float RunSpeed;
    public float MoveEnergyCost;
    public float Attack;
    public float Cooldown;
    public float GroupBonus;
    public float DamageTaken;
    public float Health;
    public float RestHeal;
    public float EnergyCapacity;
    public float EatSpeed;
    public float Detection;
    public float Carry;
    public float StoreEfficiency;
    public float Feed;
    public float ReproduceCost;

    public static SkillModifiers Neutral => new()
    {
        Speed = 1f, RunSpeed = 1f, MoveEnergyCost = 1f, Attack = 1f, Cooldown = 1f, GroupBonus = 1f, DamageTaken = 1f,
        Health = 1f, RestHeal = 1f, EnergyCapacity = 1f, EatSpeed = 1f, Detection = 1f, Carry = 1f,
        StoreEfficiency = 1f, Feed = 1f, ReproduceCost = 1f,
    };

    public static SkillModifiers From(SkillGraphStore store, int index, SkillGraphTopology topology, SkillAtlas atlas)
    {
        var m = Neutral;
        if ((uint)index >= (uint)store.Capacity) return m;
        var curves = atlas.NativeCurves;
        var channels = atlas.NativeChannels;
        var dividers = atlas.NativeDividers;
        var levels = store.Levels;
        int baseIndex = index * store.SkillCount;
        ulong usable = topology.ComponentOf(store.Root(index)) & ~topology.PrunedMask;
        for (int v = 0; v < store.SkillCount; v++)
        {
            var channel = (SkillChannel)channels[v];
            if (channel == SkillChannel.None || ((usable >> v) & 1ul) == 0ul) continue;
            m.Multiply(channel, SkillGraphMath.Effect(levels[baseIndex + v], curves[v], dividers[v] != 0));
        }
        return m;
    }

    public void Multiply(SkillChannel channel, float factor)
    {
        switch (channel)
        {
            case SkillChannel.Speed: Speed *= factor; break;
            case SkillChannel.RunSpeed: RunSpeed *= factor; break;
            case SkillChannel.MoveEnergyCost: MoveEnergyCost *= factor; break;
            case SkillChannel.Attack: Attack *= factor; break;
            case SkillChannel.Cooldown: Cooldown *= factor; break;
            case SkillChannel.GroupBonus: GroupBonus *= factor; break;
            case SkillChannel.DamageTaken: DamageTaken *= factor; break;
            case SkillChannel.Health: Health *= factor; break;
            case SkillChannel.RestHeal: RestHeal *= factor; break;
            case SkillChannel.EnergyCapacity: EnergyCapacity *= factor; break;
            case SkillChannel.EatSpeed: EatSpeed *= factor; break;
            case SkillChannel.Detection: Detection *= factor; break;
            case SkillChannel.Carry: Carry *= factor; break;
            case SkillChannel.StoreEfficiency: StoreEfficiency *= factor; break;
            case SkillChannel.Feed: Feed *= factor; break;
            case SkillChannel.ReproduceCost: ReproduceCost *= factor; break;
        }
    }
}
