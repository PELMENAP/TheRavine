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

    public Vector3 Position => transform.position;
    public Vector3 Velocity => velocity;

    public void Inject(MapGenerator map) => mapGenerator = map;

    public async UniTask MoveToAsync(Vector3 target, float speed, float maxDuration,
        float energyCostPerSec, CancellationToken ct)
    {
        if (mapGenerator == null) return;

        float startTime = Time.time;
        target.y = transform.position.y;

        while (Vector2.Distance(Flat(transform.position), Flat(target)) > arriveThreshold &&
               Time.time - startTime < maxDuration)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            dir.Normalize();

            velocity = Vector3.Lerp(velocity, dir * speed, velocityLerpCoef * Time.deltaTime);

            Vector3 pos = transform.position;
            pos.x += velocity.x * Time.deltaTime;
            pos.z += velocity.z * Time.deltaTime;

            pos.y = mapGenerator.SampleHeightBilinear(pos.x, pos.z) + heightOffset;

            transform.position = pos;
            await UniTask.Yield(ct);
        }

        Stop();
    }

    public void Stop() => velocity = Vector3.zero;

    private static Vector2 Flat(Vector3 v) => new(v.x, v.z);
}