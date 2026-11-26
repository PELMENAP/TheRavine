using UnityEngine;
using Cysharp.Threading.Tasks;
using TheRavine.EntityControl;

[RequireComponent(typeof(Renderer))]
public class RippleEffect : MonoBehaviour
{
    public const int TextureSize = 512;
    private const int TextureMask = TextureSize - 1;
    private const float FixedStep = 1f / 30f;
    private const int MaxStepsPerFrame = 2;

    [SerializeField] private ComputeShader simCompute;
    [SerializeField] private Transform followTarget;
    [SerializeField, Min(0.1f)] private float waveSpeedMetersPerSec = 3.2f;
    [SerializeField, Range(0.90f, 0.998f)] private float damping = 0.985f;

    private static readonly int RippleTexId = Shader.PropertyToID("RippleTex");
    private static readonly int StampsId = Shader.PropertyToID("Stamps");
    private static readonly int StampCountId = Shader.PropertyToID("StampCount");
    private static readonly int StateInId = Shader.PropertyToID("StateIn");
    private static readonly int StateOutId = Shader.PropertyToID("StateOut");
    private static readonly int ClearTargetId = Shader.PropertyToID("ClearTarget");
    private static readonly int TexSizeId = Shader.PropertyToID("TexSize");
    private static readonly int TexMaskId = Shader.PropertyToID("TexMask");
    private static readonly int CenterTexelId = Shader.PropertyToID("CenterTexel");
    private static readonly int WaveC2Id = Shader.PropertyToID("WaveC2");
    private static readonly int DampingId = Shader.PropertyToID("Damping");
    private static readonly int ClearXStartId = Shader.PropertyToID("ClearXStart");
    private static readonly int ClearXCountId = Shader.PropertyToID("ClearXCount");
    private static readonly int ClearZStartId = Shader.PropertyToID("ClearZStart");
    private static readonly int ClearZCountId = Shader.PropertyToID("ClearZCount");

    private RenderTexture _stateA;
    private RenderTexture _stateB;
    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;

    private int _kernelClearAll;
    private int _kernelClearStrip;
    private int _kernelStepAndStamp;
    private int _groups;

    private float _accumulator;
    private int _centerTx;
    private int _centerTz;
    private bool _centerInitialized;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        if (simCompute == null)
        {
            enabled = false;
            return;
        }

        _kernelClearAll = simCompute.FindKernel("ClearAll");
        _kernelClearStrip = simCompute.FindKernel("ClearStrip");
        _kernelStepAndStamp = simCompute.FindKernel("StepAndStamp");
        _groups = TextureSize / 8;
    }

    private void Start()
    {
        BindPlayer().Forget();
    }

    private async UniTaskVoid BindPlayer()
    {
        var token = destroyCancellationToken;
        var players = ServiceLocator.Players;

        await UniTask.WaitUntil(() => players.GetAllPlayers().Count > 0, cancellationToken: token);

        AEntity player = players.GetAllPlayers()[0];
        await UniTask.WaitUntil(() => player.HasComponent<TransformComponent>(), cancellationToken: token);

        followTarget = player.GetEntityComponent<TransformComponent>().GetEntityTransform();
    }

    private void OnEnable()
    {
        if (simCompute == null)
        {
            enabled = false;
            return;
        }

        _stateA = CreateStateRT("RippleStateA");
        _stateB = CreateStateRT("RippleStateB");

        simCompute.SetInts(TexSizeId, TextureSize, TextureSize);
        simCompute.SetInts(TexMaskId, TextureMask, TextureMask);

        ClearTexture(_stateA);
        ClearTexture(_stateB);

        _centerInitialized = false;
        _accumulator = 0f;
        ApplyRippleTexture(_stateA);
    }

    private void OnDisable()
    {
        DestroyRT(ref _stateA);
        DestroyRT(ref _stateB);
    }

    private static void DestroyRT(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        if (Application.isPlaying)
            Destroy(rt);
        else
            DestroyImmediate(rt);
        rt = null;
    }

    private void LateUpdate()
    {
        var stampSystem = RippleStampSystem.Instance;
        if (stampSystem == null || simCompute == null) return;

        _accumulator = Mathf.Min(_accumulator + Time.deltaTime, FixedStep * MaxStepsPerFrame);
        if (_accumulator < FixedStep) return;

        stampSystem.RecalculateMetrics(TextureSize);
        float texelSize = stampSystem.WindowSize / TextureSize;

        float courant = (waveSpeedMetersPerSec * FixedStep) / texelSize;
        float waveC2 = Mathf.Min(courant * courant, 0.45f);

        Vector3 targetPos = ResolveFollowPosition();
        int newCenterTx = Mathf.RoundToInt(targetPos.x / texelSize);
        int newCenterTz = Mathf.RoundToInt(targetPos.z / texelSize);

        UpdateWindowAndClearStrip(stampSystem, newCenterTx, newCenterTz, texelSize);

        int pendingStamps = stampSystem.UploadStamps(out ComputeBuffer stampBuf);
        if (stampBuf == null)
        {
            _accumulator = 0f;
            return;
        }

        int steps = 0;
        while (_accumulator >= FixedStep && steps < MaxStepsPerFrame)
        {
            _accumulator -= FixedStep;
            steps++;

            int stampCount = steps == 1 ? pendingStamps : 0;

            simCompute.SetBuffer(_kernelStepAndStamp, StampsId, stampBuf);
            simCompute.SetInt(StampCountId, stampCount);
            simCompute.SetTexture(_kernelStepAndStamp, StateInId, _stateA);
            simCompute.SetTexture(_kernelStepAndStamp, StateOutId, _stateB);
            simCompute.SetInts(CenterTexelId, _centerTx & TextureMask, _centerTz & TextureMask);
            simCompute.SetFloat(WaveC2Id, waveC2);
            simCompute.SetFloat(DampingId, damping);

            simCompute.Dispatch(_kernelStepAndStamp, _groups, _groups, 1);

            (_stateA, _stateB) = (_stateB, _stateA);
        }

        ApplyRippleTexture(_stateA);
    }
    private Vector3 ResolveFollowPosition()
    {
        if (followTarget != null) return followTarget.position;
        var cam = Camera.main;
        return cam != null ? cam.transform.position : transform.position;
    }

    private void UpdateWindowAndClearStrip(RippleStampSystem stampSystem, int newTx, int newTz, float texelSize)
    {
        if (!_centerInitialized)
        {
            _centerTx = newTx;
            _centerTz = newTz;
            _centerInitialized = true;
            stampSystem.SetSnappedWindowCenter(_centerTx * texelSize, _centerTz * texelSize);
            return;
        }

        int dx = newTx - _centerTx;
        int dz = newTz - _centerTz;
        if (dx == 0 && dz == 0) return;

        if (Mathf.Abs(dx) >= TextureSize || Mathf.Abs(dz) >= TextureSize)
        {
            ClearTexture(_stateA);
        }
        else
        {
            const int half = TextureSize / 2;
            int xStart = 0, xCount = 0;
            int zStart = 0, zCount = 0;

            if (dx > 0)
            {
                xStart = (_centerTx + half) & TextureMask;
                xCount = dx;
            }
            else if (dx < 0)
            {
                xStart = (newTx - half) & TextureMask;
                xCount = -dx;
            }

            if (dz > 0)
            {
                zStart = (_centerTz + half) & TextureMask;
                zCount = dz;
            }
            else if (dz < 0)
            {
                zStart = (newTz - half) & TextureMask;
                zCount = -dz;
            }

            simCompute.SetInt(ClearXStartId, xStart);
            simCompute.SetInt(ClearXCountId, xCount);
            simCompute.SetInt(ClearZStartId, zStart);
            simCompute.SetInt(ClearZCountId, zCount);
            simCompute.SetTexture(_kernelClearStrip, ClearTargetId, _stateA);
            simCompute.Dispatch(_kernelClearStrip, _groups, _groups, 1);
        }

        _centerTx = newTx;
        _centerTz = newTz;
        stampSystem.SetSnappedWindowCenter(_centerTx * texelSize, _centerTz * texelSize);
    }

    private void ClearTexture(RenderTexture rt)
    {
        simCompute.SetTexture(_kernelClearAll, ClearTargetId, rt);
        simCompute.Dispatch(_kernelClearAll, _groups, _groups, 1);
    }

    private void ApplyRippleTexture(RenderTexture rt)
    {
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetTexture(RippleTexId, rt);
        _renderer.SetPropertyBlock(_mpb);
    }

    private static RenderTexture CreateStateRT(string rtName)
    {
        var rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf)
        {
            name = rtName,
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        rt.Create();
        return rt;
    }
}