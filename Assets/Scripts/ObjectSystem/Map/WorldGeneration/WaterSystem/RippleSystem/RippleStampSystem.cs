using UnityEngine;
public class RippleStampSystem : MonoBehaviour
{
    public static RippleStampSystem Instance { get; private set; }

    [SerializeField] private Shader _stampShader;
    [SerializeField] private Collider waterCollider;
    private Material _stampMat;
    private float waterSize = 1f / 375f;

    private float waterX, waterZ;
    public void SetWaterPosition(float _waterX, float _waterZ)
    {
        waterX = _waterX - 375f / 2;
        waterZ = _waterZ - 375f / 2;
    }

    private readonly struct StampRequest
    {
        public readonly Vector2 UVCenter;
        public readonly float Radius;
        public readonly float Strength;
        public StampRequest(Vector2 uv, float r, float s) { UVCenter = uv; Radius = r; Strength = s; }
    }

    private readonly System.Collections.Generic.List<StampRequest> _pending = new(16);

    private void Awake()
    {
        Instance = this;
        _stampMat = new Material(_stampShader);
    }
    public void Stamp(Vector3 worldPos, float radius, float strength)
    {
        Vector2 uv = new(
            (worldPos.x - waterX) * waterSize,
            (worldPos.z - waterZ) * waterSize);
        float uvRadius = radius * waterSize;

        _pending.Add(new StampRequest(uv, uvRadius, strength));
    }

    public void FlushToRT(RenderTexture target)
    {
        if (_pending.Count == 0) return;
        var prev = RenderTexture.active;
        RenderTexture.active = target;
        foreach (var req in _pending)
        {
            _stampMat.SetVector("_StampCenter", req.UVCenter);
            _stampMat.SetFloat("_StampRadius", req.Radius);
            _stampMat.SetFloat("_Strength", req.Strength);
            Graphics.Blit(null, target, _stampMat);
        }
        _pending.Clear();
        RenderTexture.active = prev;
    }
}