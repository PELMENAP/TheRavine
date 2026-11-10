using UnityEngine;

public sealed class MimicLegDriver : MonoBehaviour
{
    private void LateUpdate() => MimicLegSystem.Run();
}
