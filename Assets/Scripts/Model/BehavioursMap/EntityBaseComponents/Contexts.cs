using System;
public struct PlayerContext
{
    public string Name;
    public string ProfessionId;
    public float Expertise;
    public float Doubt;
    public string[] KnownFacts;
    
    public static PlayerContext Default => new()
    {
        Name = "unknown",
        ProfessionId = "unknown",
        Expertise = 0f,
        Doubt = 0f,
        KnownFacts = Array.Empty<string>()
    };
}

public struct ItemContext
{
    public string ItemName;
    public string[] ItemTags;
    public string Material;
}