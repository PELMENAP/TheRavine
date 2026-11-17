public static class RiveExtensions
{
    public static string ToRiveString(this object value)
    {
        return value switch
        {
            int i => i.ToString(),
            null => "null",
            _ => value.ToString()
        };
    }
}