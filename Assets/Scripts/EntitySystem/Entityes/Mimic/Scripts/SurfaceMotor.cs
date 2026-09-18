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
    private Vector3 velocity;

    public Vector3 Position()
    {
        if (transform == null) return Vector3.zero;
        return transform.position;
    }
    public Vector3 Velocity => velocity;

    private const float MinSpeedModifier = 0.05f;
    private const int   SpeedResampleFrames = 4;

    private IEnergySink energySink;

    public void InjectEnergySink(IEnergySink sink) => energySink = sink;

    public void Inject(MapGenerator map) => mapGenerator = map;

    public async UniTask<MoveResult> MoveToAsync(Vector3 target, float speed, float maxDuration,
        float energyCostPerSec, CancellationToken ct)
    {
        if (mapGenerator == null) return MoveResult.None;

        float startTime = Time.time;
        target.y = transform.position.y;

        float speedModifier = 1f;
        int   frame = 0;

        float distance = 0f;
        float pathCost = 0f;
        float energy   = 0f;
        bool  arrived  = false;

        while (!ct.IsCancellationRequested)
        {
            if (this == null) return new MoveResult(distance, pathCost, energy, false);

            if (Vector2.Distance(Flat(transform.position), Flat(target)) <= arriveThreshold)
            {
                arrived = true;
                break;
            }
            if (Time.time - startTime >= maxDuration) break;

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) { arrived = true; break; }
            dir.Normalize();

            if (frame == 0)
                speedModifier = mapGenerator.GetSpeedModifier(
                    transform.position.x,
                    transform.position.z,
                    new float2(dir.x, dir.z));

            frame++;
            if (frame >= SpeedResampleFrames) frame = 0;

            float costModifier = speedModifier < MinSpeedModifier ? MinSpeedModifier : speedModifier;

            velocity = Vector3.Lerp(velocity, dir * (speed * speedModifier),
                velocityLerpCoef * Time.deltaTime);

            Vector3 pos = transform.position;
            float dx = velocity.x * Time.deltaTime;
            float dz = velocity.z * Time.deltaTime;
            pos.x += dx;
            pos.z += dz;
            pos.y = mapGenerator.SampleHeightBilinear(pos.x, pos.z) + heightOffset;

            transform.position = pos;

            float step = math.sqrt(dx * dx + dz * dz);
            distance += step;
            pathCost += step / costModifier;

            if (energySink != null && energyCostPerSec > 0f)
            {
                float spent = energyCostPerSec / costModifier * Time.deltaTime;
                energySink.TryConsume(spent);
                energy += spent;
            }

            await UniTask.Yield(ct);
        }

        if (this != null) Stop();
        return new MoveResult(distance, pathCost, energy, arrived);
    }

    public void Stop() => velocity = Vector3.zero;

    private static Vector2 Flat(Vector3 v) => new(v.x, v.z);
}