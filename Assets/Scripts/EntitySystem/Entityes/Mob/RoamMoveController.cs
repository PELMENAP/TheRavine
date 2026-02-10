using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Extensions;
using TheRavine.Base;
using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class RoamMoveController : MonoBehaviour
    {
        private Transform entityTransform;
        [SerializeField] private Transform pointTarget, pointRandom;
        [SerializeField] private GameObject view;
        [SerializeField] private int movementSpeed, accuracy, maxTargetDistance;
        [SerializeField] private int stepMaxDistance, stepSpread, angleDivide;
        [SerializeField] private int bezierDetail, bezierFactor;
        [SerializeField] private int movementDelay, movementDelaySpread, defaultDelay, colliderDistance;
        private Vector2 target;
        private bool isDelay = false, side = true, isActive = false;
        private Vector2[] bezierPoints;
        private int currentPointIndex;
        private Collider2D Ccollider;
        private AEntity entity;
        private MovementComponent movementComponent;
        public void SetInitialValues(AEntity entity, IRavineLogger logger)
        {
            entityTransform = this.transform;
            Ccollider = this.GetComponent<Collider2D>();
            bezierPoints = new Vector2[bezierDetail + 1];
            this.entity = entity;
            movementComponent = entity.GetEntityComponent<MovementComponent>();

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
            while (entity.IsActive.Value)
            {
                if(entityTransform == null) return;
                if (!isActive) await UniTask.Delay(defaultDelay);
                if (Extension.CheckDistance(entityTransform.position, target, accuracy))
                    UpdateTargetWander();
                if (currentPointIndex < bezierPoints.Length && bezierPoints[0] != Vector2.zero)
                {
                    if (Extension.CheckDistance(entityTransform.position, bezierPoints[currentPointIndex], accuracy))
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

                if (currentPointIndex < bezierPoints.Length && bezierPoints[0] != Vector2.zero)
                    movementComponent.SetVelocity(new Vector2((bezierPoints[currentPointIndex].x - entityTransform.position.x) * movementSpeed * Time.deltaTime, (bezierPoints[currentPointIndex].y - entityTransform.position.y) * movementSpeed * Time.deltaTime));
                else movementComponent.SetVelocity(Vector2.zero);


                await UniTask.Delay(defaultDelay);
            }
        }
        public Transform GetModelTransform() => entityTransform;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (entity.IsActive.Value)
                UpdateRandomMove(true);
        }
        private void UpdateTargetWander()
        {
            target = Extension.GetRandomPointAround((Vector2)entityTransform.position, maxTargetDistance);
            pointTarget.position = target;
            UpdateRandomMove();
        }

        private int UpdateRandomMove(bool isBackwards = false)
        {
            Vector2 start = entityTransform.position;
            Vector2 directionToTarget = new Vector2(target.x - start.x, target.y - start.y);
            float angleOffset = RavineRandom.RangeFloat(-Mathf.PI / angleDivide, Mathf.PI / angleDivide);
            Vector2 randomDirection = isBackwards ? Extension.RotateVector(directionToTarget, 2 * angleOffset) : Extension.RotateVector(directionToTarget, angleOffset);
            randomDirection.Normalize();
            Ccollider.offset = new Vector2(randomDirection.x * colliderDistance, randomDirection.y * colliderDistance);
            int distanceStep = RavineRandom.RangeInt(stepMaxDistance - stepSpread, stepMaxDistance + stepSpread);
            Vector2 randomPoint = new Vector2(start.x + randomDirection.x * distanceStep, start.y + randomDirection.y * distanceStep);
            Vector2 distribution = side ? Extension.PerpendicularCounterClockwise(randomPoint - start).normalized : Extension.PerpendicularClockwise(randomPoint - start).normalized;
            Vector2 control = Extension.GetRandomPointAround((start + randomPoint) / 2 + distribution * bezierFactor, bezierFactor / 2);
            Extension.GenerateBezierPoints(start, control, randomPoint, bezierDetail, ref bezierPoints);
            currentPointIndex = 0;
            pointRandom.position = randomPoint;
            side = !side;
            return RavineRandom.RangeInt(movementDelay - movementDelaySpread, movementDelay + movementDelaySpread);
        }

        public void Delete() { }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}