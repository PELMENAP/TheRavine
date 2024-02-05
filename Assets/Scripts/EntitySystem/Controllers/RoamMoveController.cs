using UnityEngine;
using System;
using System.Collections;
using System.Threading.Tasks;
public class RoamMoveController : MonoBehaviour
{
    public event Action<string, float> setValueToAnimator;
    public event Action missionComplete, setRandomPointComplete, setRandomPointStart;
    public Vector3 randomD;
    private const float pi = 3.14f;
    [SerializeField] private Rigidbody2D RB;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform ET, pointT, pointR; // EntityTranform
    [SerializeField] private Vector3 groupTarget, target, randomEP; // targetDirection, randomDirection, randomEndPosition
    [SerializeField] private float movementSpeed, accuracy, lostPath, lostPathValue, maxTarget;
    [SerializeField] private int stepValue, spreadOfStep, angleDivide, movementDelay;
    [SerializeField] private bool isGroup;
    private bool delay;

    #region [MONO]
    private float GRMV(float n) => UnityEngine.Random.Range(-n, n); // GetRandomModuleValue
    private int GRVR(int n, int spread) => UnityEngine.Random.Range(n - spread, n + spread); // GetRandomValueOnRange
    private Vector3 GRPA(Vector3 vector, float n) => new Vector3(vector.x + GRMV(n), vector.y + GRMV(n)); //GetRandomPointInArea
    private Vector3 GRV(Vector3 vector, float angle) => new Vector3(vector.x * Mathf.Cos(angle) - vector.y * Mathf.Sin(angle), vector.x * Mathf.Sin(angle) + vector.y * Mathf.Cos(angle)); //GetRotateVector
    private bool CheckMeet(Vector3 first, Vector3 second, float value) => Vector2.Distance(first, second) < value;

    public void AssingMethod(Transform _ET, Rigidbody2D _RB)
    {
        ET = _ET;
        RB = _RB;
    }

    #endregion
    public void UpdateBehaviour()
    {
        if (delay)
            return;
        if (CheckMeet(ET.position, randomEP, accuracy))
            UpdateRandomMove();
        else if (lostPath < 0)
            UpdateRandomMove();
        if (CheckMeet(ET.position, target, accuracy))
            missionComplete?.Invoke();
        RB.velocity = randomD * movementSpeed;
        lostPath -= movementSpeed * lostPathValue;
    }

    public void UpdateTargetWander()
    {
        target = isGroup ? groupTarget : GRPA(ET.position, maxTarget);
        pointT.position = target;
        UpdateRandomMove();
    }

    public async void UpdateRandomMove(bool back = false)
    {
        delay = true;
        setRandomPointStart?.Invoke();
        setValueToAnimator?.Invoke("Speed", 0);
        RB.velocity = Vector3.zero;
        if (!back)
            randomD = GRV(target - ET.position, GRMV(pi / angleDivide));
        else
            randomD = GRV(target - ET.position, 2 * GRMV(pi / angleDivide));
        randomD.Normalize();
        randomEP = randomD * GRVR(stepValue, spreadOfStep) + ET.position;
        pointR.position = randomEP;
        lostPath = Vector2.Distance(ET.position, randomEP);
        await Task.Delay(100 * movementDelay);
        setRandomPointComplete?.Invoke();
        await Task.Delay(10 * movementDelay);
        setValueToAnimator?.Invoke("Horizontal", randomD.x);
        setValueToAnimator?.Invoke("Vertical", randomD.y);
        setValueToAnimator?.Invoke("Speed", 1);
        delay = false;
    }
}