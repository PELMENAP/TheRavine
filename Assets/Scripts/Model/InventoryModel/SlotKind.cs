using System;

public enum SlotKind : byte
{
    Bag,
    Hotbar,
    Head,
    Chest,
    Legs,
    Boots,
    Artifact,
    Craft,
    CraftResult
}

[Flags]
public enum SlotMask : ushort
{
    None = 0,
    Bag = 1 << 0,
    Hotbar = 1 << 1,
    Head = 1 << 2,
    Chest = 1 << 3,
    Legs = 1 << 4,
    Boots = 1 << 5,
    Artifact = 1 << 6,
    Craft = 1 << 7,
    CraftResult = 1 << 8
}

public static class SlotKindExtensions
{
    public static SlotMask ToMask(this SlotKind kind) => (SlotMask)(1 << (int)kind);
    public static bool IsStorage(this SlotKind kind) => kind == SlotKind.Bag || kind == SlotKind.Hotbar;
    public static bool IsEquipment(this SlotKind kind) => kind >= SlotKind.Head && kind <= SlotKind.Artifact;
}
