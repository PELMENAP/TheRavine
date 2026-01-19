public static class RiveExtensions
{
    public static string ToRiveString(this object value)
    {
        return value switch
        {
            int i => i.ToString(),
            Qbit q => $"qbit(α={q.Alpha:F3}, β={q.Beta:F3})",
            null => "null",
            _ => value.ToString()
        };
    }
}