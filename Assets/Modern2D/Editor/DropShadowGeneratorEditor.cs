using UnityEngine;
using UnityEditor;
using Modern2D;

namespace Modern2DEditor
{
    //  flag necressary for opengl builds because for some reason
    //  Unity includes scripts from editor folder in builds
    #if UNITY_EDITOR

    [CanEditMultipleObjects]
    [CustomEditor(typeof(DropShadowGenerator))]
    public class DropShadowGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("GenerateDropShadow"))
                foreach (DropShadowGenerator generator in targets)
                    generator.GenerateShadow();

            base.OnInspectorGUI();
        }
    }

    #endif

}
