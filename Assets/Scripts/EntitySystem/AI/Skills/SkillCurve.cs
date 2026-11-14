using System;

[Serializable]
public struct SkillCurve
{
    public float P;
    public float L0;
    public float C;

    public SkillCurve(float p, float l0, float c)
    {
        P = p;
        L0 = l0;
        C = c;
    }

    public static SkillCurve ProfileA => new(0.80f, 6f, 0.12f);
    public static SkillCurve ProfileB => new(0.75f, 8f, 0.12f);
    public static SkillCurve ProfileC => new(0.45f, 4f, 0.22f);
}
