using System;

[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class FrameFieldAttribute : Attribute
{
    public readonly string FrameName;

    public FrameFieldAttribute(string frameName = null)
    {
        FrameName = frameName;
    }
}
