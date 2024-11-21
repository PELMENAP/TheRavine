using UnityEngine;
using TheRavine.Extensions;

public class FakeDistortion : MonoBehaviour
{
    public int distortionFactor;
    private void Start() {
        this.transform.localPosition += (Vector3)Extension.GetRandomPointAround((Vector2)this.transform.localPosition, distortionFactor);
    }
}
