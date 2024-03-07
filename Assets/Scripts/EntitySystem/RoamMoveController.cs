using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Extentions;
using TheRavine.EntityControl;

[RequireComponent(typeof(Rigidbody2D))]
public class RoamMoveController : MonoBehaviour, IEntityControllable
{
    [SerializeField] private Transform entityTransform;
    [SerializeField] private Transform pointTarget, pointRandom;
    [SerializeField] private GameObject view;
    [SerializeField] private byte movementSpeed, accuracy, maxTargetDistance;
    [SerializeField] private byte stepMaxDistance, stepSpread, angleDivide;
    [SerializeField] private byte bezierDetail, bezierFactor;
    [SerializeField] private int movementDelay, movementDelaySpread;

    private Vector2 target;
    private bool isDelay, side = true;
    private Vector2[] bezierPoints;
    private byte currentPointIndex;
    // private void Awake()
    // {
    //     bezierPoints = new Vector2[bezierDetail + 1];
    //     UpdateTargetWander();
    // }
    public void SetInitialValues(AEntity entity)
    {
        bezierPoints = new Vector2[bezierDetail + 1];
        UpdateTargetWander();
    }
    public void SetZeroValues()
    {
    }
    public void EnableComponents()
    {
        view.SetActive(true);
        pointTarget.gameObject.SetActive(true);
        pointRandom.gameObject.SetActive(true);
    }
    public void DisableComponents()
    {
        view.SetActive(false);
        pointTarget.gameObject.SetActive(false);
        pointRandom.gameObject.SetActive(false);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        print("triggered");
        UpdateRandomMove(true).Forget();
    }

    // private void Update()
    // {
    //     UpdateMobControllerCycle();
    // }
    public void UpdateMobControllerCycle()
    {
        if (isDelay) return;
        if (Extention.CheckDistance(entityTransform.position, target, accuracy))
            UpdateTargetWander();
        if (currentPointIndex < bezierPoints.Length && bezierPoints[0] != Vector2.zero)
        {
            entityTransform.position = Vector2.MoveTowards(entityTransform.position, bezierPoints[currentPointIndex], movementSpeed * Time.deltaTime);
            if (Extention.CheckDistance(entityTransform.position, bezierPoints[currentPointIndex], accuracy))
            {
                currentPointIndex++;
                if (currentPointIndex >= bezierPoints.Length)
                    UpdateRandomMove().Forget();
            }
        }
    }

    public void UpdateTargetWander()
    {
        target = Extention.GetRandomPointAround((Vector2)entityTransform.position, maxTargetDistance);
        pointTarget.position = target;
        UpdateRandomMove().Forget();
    }

    public async UniTaskVoid UpdateRandomMove(bool isBackwards = false)
    {
        isDelay = true;
        Vector2 start = entityTransform.position;
        Vector2 directionToTarget = target - start;
        float angleOffset = Extention.GetRandomValue(-Mathf.PI / angleDivide, Mathf.PI / angleDivide);
        Vector2 randomDirection = isBackwards ? Extention.RotateVector(directionToTarget, 2 * angleOffset) : Extention.RotateVector(directionToTarget, angleOffset);
        randomDirection.Normalize();
        Vector2 randomPoint = start + randomDirection * RavineRandom.RangeInt(stepMaxDistance - stepSpread, stepMaxDistance + stepSpread);
        Vector2 distribution = side ? Extention.PerpendicularCounterClockwise(randomPoint - start).normalized : Extention.PerpendicularClockwise(randomPoint - start).normalized;
        Vector2 control = Extention.GetRandomPointAround((start + randomPoint) / 2 + distribution * bezierFactor, bezierFactor / 2);

        Extention.GenerateBezierPoints(start, control, randomPoint, bezierDetail, ref bezierPoints);
        currentPointIndex = 0;

        pointRandom.position = randomPoint;
        await UniTask.Delay(10 * RavineRandom.RangeInt(movementDelay - movementDelaySpread, movementDelay + movementDelaySpread));
        side = !side;
        isDelay = false;
    }
}