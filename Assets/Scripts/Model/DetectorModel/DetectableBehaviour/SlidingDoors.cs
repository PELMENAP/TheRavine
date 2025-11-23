using UnityEngine;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(DetectableObject))]
public class SlidingDoors : MonoBehaviour
{
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private float maxDist = 3f;
    [SerializeField] private float speed = 2f;

    private Vector3 leftDoorClosedPos;
    private Vector3 rightDoorClosedPos;
    private Vector3 leftDoorOpenPos;
    private Vector3 rightDoorOpenPos;
    private bool isMoving = false, quiteStop = false;

    private IDetectableObject _idetectableobject;

    private void Awake()
    {
        _idetectableobject = this.GetComponent<DetectableObject>();
        _idetectableobject.OnGameObjectDetectedEvent += OnDetectedEvent;
        leftDoorClosedPos = leftDoor.localPosition;
        rightDoorClosedPos = rightDoor.localPosition;
        leftDoorOpenPos = leftDoorClosedPos + new Vector3(-maxDist, 0, 0);
        rightDoorOpenPos = rightDoorClosedPos + new Vector3(maxDist, 0, 0);
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player"))
        {
            if(!isMoving)
                MoveDoors(leftDoorOpenPos, rightDoorOpenPos).Forget();
            else
                quiteStop = true;
        }
    }
    private async UniTaskVoid MoveDoors(Vector3 leftTargetPos, Vector3 rightTargetPos, bool isClosing = false)
    {
        isMoving = true;
        while (Vector3.Distance(leftDoor.localPosition, leftTargetPos) > 0.1f || Vector3.Distance(rightDoor.localPosition, rightTargetPos) > 0.1f)
        {
            if(quiteStop)
            {
                await UniTask.Delay(500);
                quiteStop = false;
            }
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftTargetPos, Time.deltaTime * speed);
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightTargetPos, Time.deltaTime * speed);
            await UniTask.Yield();
        }
        leftDoor.localPosition = leftTargetPos;
        rightDoor.localPosition = rightTargetPos;
        if(!isClosing) MoveDoors(leftDoorClosedPos, rightDoorClosedPos, true).Forget();
        else isMoving = false;
    }

    private void OnDestroy()
    {
        _idetectableobject.OnGameObjectDetectedEvent -= OnDetectedEvent;
    }
}