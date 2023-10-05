using UnityEngine;

public class FPS : MonoBehaviour {
    public static float fps;
    void OnGUI()
    {
        fps = 1.0f / Time.deltaTime;
        GUILayout.Label("FPS: " + (int)fps);
    }
}