using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class EnableAllSpriteShadows : EditorWindow
{
    [MenuItem("Tools/Enable All Sprite Cast Shadows")]
    public static void EnableShadows()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

        int count = 0;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.shadowCastingMode != ShadowCastingMode.On)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.staticShadowCaster = true;
                count++;
            }
        }

        Debug.Log($"Enabled shadow casting on {count} Sprite Renderers.");

        if (count > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
    }
}