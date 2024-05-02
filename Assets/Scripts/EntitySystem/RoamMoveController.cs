using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Extentions;
using TheRavine.Base;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class RoamMoveController : MonoBehaviour, IMobControllable
    {
        private Transform entityTransform;
        [SerializeField] private Transform pointTarget, pointRandom;
        [SerializeField] private GameObject view;
        [SerializeField] private byte movementSpeed, accuracy, maxTargetDistance;
        [SerializeField] private byte stepMaxDistance, stepSpread, angleDivide;
        [SerializeField] private byte bezierDetail, bezierFactor;
        [SerializeField] private int movementDelay, movementDelaySpread, defaultDelay, colliderDistance;
        private Vector2 target;
        private bool isDelay = false, side = true, isAlife = false, isActive = false;
        private Vector2[] bezierPoints;
        private byte currentPointIndex;
        private Collider2D Ccollider;
        public void SetInitialValues(AEntity entity)
        {
            entityTransform = this.transform;
            Ccollider = this.GetComponent<Collider2D>();
            bezierPoints = new Vector2[bezierDetail + 1];
            isAlife = true;
            UpdateTargetWander();
            UpdateMobControllerCycle().Forget();
        }
        public void SetZeroValues()
        {
        }
        public void EnableComponents()
        {
            isActive = true;
            view.SetActive(true);
            pointTarget.gameObject.SetActive(true);
            pointRandom.gameObject.SetActive(true);
        }
        public void DisableComponents()
        {
            isActive = false;
            view.SetActive(false);
            pointTarget.gameObject.SetActive(false);
            pointRandom.gameObject.SetActive(false);
        }
        public async UniTaskVoid UpdateMobControllerCycle()
        {
            while (!DataStorage.sceneClose)
            {
                if(entityTransform == null) return;
                if (!isActive) await UniTask.Delay(defaultDelay);
                if (Extention.CheckDistance(entityTransform.position, target, accuracy))
                    UpdateTargetWander();
                if (currentPointIndex < bezierPoints.Length && bezierPoints[0] != Vector2.zero)
                {
                    if (Extention.CheckDistance(entityTransform.position, bezierPoints[currentPointIndex], accuracy))
                    {
                        currentPointIndex++;
                        if (currentPointIndex >= bezierPoints.Length)
                        {
                            isDelay = true;
                            await UniTask.Delay(UpdateRandomMove());
                            isDelay = false;
                        }
                    }
                }
                await UniTask.Delay(defaultDelay);
            }
        }
        public Vector2 GetEntityVelocity()
        {
            if (isDelay && !isAlife) return Vector2.zero;
            if (currentPointIndex < bezierPoints.Length && bezierPoints[0] != Vector2.zero)
                return new Vector2((bezierPoints[currentPointIndex].x - entityTransform.position.x) * movementSpeed * Time.deltaTime, (bezierPoints[currentPointIndex].y - entityTransform.position.y) * movementSpeed * Time.deltaTime);
            return Vector2.zero;
        }
        public Transform GetModelTransform() => entityTransform;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isAlife)
                UpdateRandomMove(true);
        }
        private void UpdateTargetWander()
        {
            target = Extention.GetRandomPointAround((Vector2)entityTransform.position, maxTargetDistance);
            pointTarget.position = target;
            UpdateRandomMove();
        }

        private int UpdateRandomMove(bool isBackwards = false)
        {
            Vector2 start = entityTransform.position;
            Vector2 directionToTarget = new Vector2(target.x - start.x, target.y - start.y);
            float angleOffset = RavineRandom.RangeFloat(-Mathf.PI / angleDivide, Mathf.PI / angleDivide);
            Vector2 randomDirection = isBackwards ? Extention.RotateVector(directionToTarget, 2 * angleOffset) : Extention.RotateVector(directionToTarget, angleOffset);
            randomDirection.Normalize();
            Ccollider.offset = new Vector2(randomDirection.x * colliderDistance, randomDirection.y * colliderDistance);
            int distanceStep = RavineRandom.RangeInt(stepMaxDistance - stepSpread, stepMaxDistance + stepSpread);
            Vector2 randomPoint = new Vector2(start.x + randomDirection.x * distanceStep, start.y + randomDirection.y * distanceStep);
            Vector2 distribution = side ? Extention.PerpendicularCounterClockwise(randomPoint - start).normalized : Extention.PerpendicularClockwise(randomPoint - start).normalized;
            Vector2 control = Extention.GetRandomPointAround((start + randomPoint) / 2 + distribution * bezierFactor, bezierFactor / 2);
            Extention.GenerateBezierPoints(start, control, randomPoint, bezierDetail, ref bezierPoints);
            currentPointIndex = 0;
            pointRandom.position = randomPoint;
            side = !side;
            return RavineRandom.RangeInt(movementDelay - movementDelaySpread, movementDelay + movementDelaySpread);
        }

        public void Delete() { }
    }
}