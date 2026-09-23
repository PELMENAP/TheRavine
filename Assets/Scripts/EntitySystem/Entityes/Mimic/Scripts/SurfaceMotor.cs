using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Mathematics;
using TheRavine.Generator;

public class SurfaceMotor : MonoBehaviour, IEntityMotor, IVelocitySource
{
    [SerializeField] private float heightOffset = 8.8f;
    [SerializeField] private float velocityLerpCoef = 4f;
    [SerializeField] private float arriveThreshold = 0.1f;

    private MapGenerator mapGenerator;

    public Vector3 Position()
    {
        if (transform == null) return Vector3.zero;
        return transform.position;
    }
    private const float MinSpeedModifier = 0.05f;
    private const int   SpeedResampleFrames = 4;

    private IEnergySink energySink;

    public void InjectEnergySink(IEnergySink sink) => energySink = sink;

    public void Inject(MapGenerator map) => mapGenerator = map;

    private float2 _velocity;

    public Vector3 Velocity => new(_velocity.x, 0f, _velocity.y);

    public async UniTask<MoveResult> MoveToAsync(Vector3 target, float speed, float maxDuration,
        float energyCostPerSec, CancellationToken ct)
    {
        if (mapGenerator == null) return MoveResult.None;

        var    tr      = transform;
        float2 goal    = new(target.x, target.z);
        float  arrive2 = arriveThreshold * arriveThreshold;

        double start = SimulationClock.TimeD;
        double prev  = start;

        float speedModifier = 1f;
        int   frame = 0;

        float distance = 0f;
        float pathCost = 0f;
        float energy   = 0f;
        bool  arrived  = false;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (this == null) return new MoveResult(distance, pathCost, energy, false);

                Vector3 pos = tr.position;
                float2  to  = goal - new float2(pos.x, pos.z);
                float   d2  = math.lengthsq(to);
                if (d2 <= arrive2 || d2 < 1e-6f) { arrived = true; break; }

                double now = SimulationClock.TimeD;
                if (now - start >= maxDuration) break;

                float dt = (float)(now - prev);
                prev = now;

                if (dt > 0f)
                {
                    float2 dir = to * math.rsqrt(d2);

                    if (frame == 0)
                        speedModifier = mapGenerator.GetSpeedModifier(pos.x, pos.z, dir);
                    if (++frame >= SpeedResampleFrames) frame = 0;

                    float costModifier = math.max(speedModifier, MinSpeedModifier);

                    _velocity = math.lerp(_velocity, dir * (speed * speedModifier),
                        math.saturate(velocityLerpCoef * dt));

                    float2 step = _velocity * dt;
                    pos.x += step.x;
                    pos.z += step.y;
                    pos.y = mapGenerator.SampleHeightBilinear(pos.x, pos.z) + heightOffset;
                    tr.position = pos;

                    float len = math.length(step);
                    distance += len;
                    pathCost += len / costModifier;

                    if (energySink != null && energyCostPerSec > 0f)
                    {
                        float spent = energyCostPerSec / costModifier * dt;
                        energySink.TryConsume(spent);
                        energy += spent;
                    }
                }

                await UniTask.Yield(ct);
            }
        }
        finally
        {
            if (this != null) Stop();
        }

        return new MoveResult(distance, pathCost, energy, arrived);
    }

    public void Stop() => _velocity = float2.zero;

    private static Vector2 Flat(Vector3 v) => new(v.x, v.z);
}