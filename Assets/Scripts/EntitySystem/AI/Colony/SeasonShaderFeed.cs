using UnityEngine;

public static class SeasonShaderFeed
{
    private static readonly int SeasonId         = Shader.PropertyToID("_Season");
    private static readonly int WinterApproachId = Shader.PropertyToID("_WinterApproach");

    public static void Publish(float time)
    {
        Shader.SetGlobalFloat(SeasonId, SeasonClock.Value(time));
        Shader.SetGlobalFloat(WinterApproachId, SeasonClock.WinterApproach(time));
    }

    public static void Clear()
    {
        Shader.SetGlobalFloat(SeasonId, 0f);
        Shader.SetGlobalFloat(WinterApproachId, 0f);
    }
}
