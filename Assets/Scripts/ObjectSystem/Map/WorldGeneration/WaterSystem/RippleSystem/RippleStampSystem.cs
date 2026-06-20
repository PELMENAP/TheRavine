using UnityEngine;
using System.Collections.Generic;
public class RippleStampSystem : MonoBehaviour
{
    public static RippleStampSystem Instance { get; private set; }

    [SerializeField] private ComputeShader stampCompute;
    private ComputeBuffer stampBuffer;
    private const int MaxStamps = 64;
    private StampData[] stampArray = new StampData[MaxStamps];
    private readonly struct StampRequest
    {
        public readonly Vector2 UVCenter;
        public readonly float Radius;
        public readonly float Strength;
        public StampRequest(Vector2 uv, float r, float s) { UVCenter = uv; Radius = r; Strength = s; }
    }

    private readonly List<StampRequest> pending = new(16);

    struct StampData { public Vector2 center; public float invRadius; public float strength; }
    private int kernelId;

    private void Awake()
    {
        Instance = this;
        stampBuffer = new ComputeBuffer(MaxStamps, 16); // float2+float+float = 16 bytes
        kernelId = stampCompute.FindKernel("StampKernel");
    }
    private const float WaterSize = 375f;
    private readonly float waterSizeInv = 1f / WaterSize;
    private static readonly int RippleOffsetID = Shader.PropertyToID("RippleOffset");

    private static Vector2 Frac(Vector2 v) =>
        v - new Vector2(Mathf.Floor(v.x), Mathf.Floor(v.y));

    public void SetWaterPosition(float _waterX, float _waterZ)
    {
        Vector2 corner = new(_waterX - WaterSize * 0.5f, _waterZ - WaterSize * 0.5f);
        Vector2 offset = Frac(corner * waterSizeInv);
        Shader.SetGlobalVector(RippleOffsetID, new Vector4(offset.x, offset.y, 0f, 0f));
    }

    public void Stamp(Vector3 worldPos, float radius, float strength)
    {
        Vector2 uv = Frac(new Vector2(worldPos.x, worldPos.z) * waterSizeInv);
        float uvRadius = radius * waterSizeInv;
        pending.Add(new StampRequest(uv, 1f / uvRadius, strength));
    }

    public void FlushToRT(RenderTexture target)
    {
        if (pending.Count == 0) return;
        int count = Mathf.Min(pending.Count, MaxStamps);
        for (int i = 0; i < count; i++)
            stampArray[i] = new StampData { center = pending[i].UVCenter, invRadius = pending[i].Radius, strength = pending[i].Strength };

        stampBuffer.SetData(stampArray, 0, 0, count);
        stampCompute.SetBuffer(kernelId, "Stamps", stampBuffer);
        
        stampCompute.SetInt("StampCount", count);
        stampCompute.SetTexture(kernelId, "Target", target);
        stampCompute.SetInts("TexSize", target.width, target.height);
        stampCompute.Dispatch(kernelId, Mathf.CeilToInt(target.width / 8f), Mathf.CeilToInt(target.height / 8f), 1);
        pending.Clear();
    }

    private void OnDisable() => stampBuffer?.Release();
}