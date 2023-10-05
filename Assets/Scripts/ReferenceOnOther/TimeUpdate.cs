using UnityEngine;

public class TimeUpdate : MonoBehaviour
{
    public static float globalDeltaTime;
    public static float globalTime;
    public static int globalTimeInt;

    private void FixedUpdate()
    {
        GlobalTimeUpdate();
    }

    private void GlobalTimeUpdate()
    {
        globalDeltaTime = Time.deltaTime;
        globalTime += globalDeltaTime;
        globalTimeInt = (int)globalTime;
    }
}
