using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
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

    public async UniTask MoveToAsync(Vector3 target, float speed, float maxDuration,
        float energyCostPerSec, CancellationToken ct)
    {
        if (mapGenerator == null) return;

        float startTime = Time.time;
        target.y = transform.position.y;

        float speedModifier = 1f;
        int   frame = 0;

        while (!ct.IsCancellationRequested)
        {
            if (this == null) return;
            if (Vector2.Distance(Flat(transform.position), Flat(target)) <= arriveThreshold) break;
            if (Time.time - startTime >= maxDuration) break;

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) break;
            dir.Normalize();

            if (frame == 0)
                speedModifier = mapGenerator.GetSpeedModifier(
                    transform.position.x,
                    transform.position.z,
                    new Unity.Mathematics.float2(dir.x, dir.z));

            frame++;
            if (frame >= SpeedResampleFrames) frame = 0;

            float costModifier = speedModifier < MinSpeedModifier ? MinSpeedModifier : speedModifier;

            velocity = Vector3.Lerp(velocity, dir * (speed * speedModifier),
                velocityLerpCoef * Time.deltaTime);

            Vector3 pos = transform.position;
            pos.x += velocity.x * Time.deltaTime;
            pos.z += velocity.z * Time.deltaTime;
            pos.y = mapGenerator.SampleHeightBilinear(pos.x, pos.z) + heightOffset;

            transform.position = pos;

            if (energySink != null && energyCostPerSec > 0f)
                energySink.TryConsume(energyCostPerSec / costModifier * Time.deltaTime);

            await UniTask.Yield(ct);
        }

        if (this != null) Stop();
    }

    public void Stop() => velocity = Vector3.zero;

    private static Vector2 Flat(Vector3 v) => new(v.x, v.z);
}