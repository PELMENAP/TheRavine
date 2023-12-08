using UnityEngine;
public interface INALInfo
{
    byte maxcount { get; }
    byte around { get; }
    byte distance { get; }
    byte chance { get; }
    byte delay { get; }
}
