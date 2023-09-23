#if UNITY_EDITOR

using Unity.Mathematics;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;


namespace Water2D
{
    [CustomEditor(typeof(CloudShadowsSystem))]
    public class CloudShadowsSystemEditor : Editor
    {
        AnimBool ab1 = new AnimBool();
        AnimBool ab2 = new AnimBool();
        AnimBool ab3 = new AnimBool();
        bool b1;
        bool b2;
        bool b3;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            CloudShadowsSystem system = (CloudShadowsSystem)target;

            //enum types don't work with Cryo well ? 
            int c1 = (int)system.cloudType.value;
            system.cloudType = (CloudType)EditorGUILayout.EnumPopup("type of clouds", system.cloudType);
            if (c1 != (int)system.cloudType.value) system.SetShaderVariables();

            EditorGUILayout.Space(10);

            using (new LayoutUtils.FoldoutScope(false, ab1, out b1, "Camera Settings", true))
            {
                if (b1)
                {
                    system.overrideMainCamera.value = EditorGUILayout.Toggle("overrideMainCamera", system.overrideMainCamera.value);
                    system.overrideCam.value = (Camera)EditorGUILayout.ObjectField(system.overrideCam.value, typeof(Camera), true);
                }
            }

            using (new LayoutUtils.FoldoutScope(false, ab2, out b2, "Visual Settings", true))
            {
                if (b2)
                {
                    system.tiling.value = EditorGUILayout.Vector2Field("texture tiling", system.tiling.value);
                    system.scale.value = EditorGUILayout.FloatField("scale", system.scale.value);

                    float2x2 l = system.fbsMatrix.value;
                    float4 v = EditorGUILayout.Vector4Field("brownian transformation matrix", new Vector4(l.c0.x, l.c0.y, l.c1.x, l.c1.y));
                    system.fbsMatrix.value = new float2x2(v.x, v.z, v.y, v.w);
                    if (new Vector4(l.c0.x, l.c0.y, l.c1.x, l.c1.y) != (Vector4)v) system.SetShaderVariables();

                    GUILayout.Space(10);
                    system.alpha.value = EditorGUILayout.Slider("alpha", system.alpha.value, 0, 2);
                    system.maxAlpha.value = EditorGUILayout.Slider("out max alpha", system.maxAlpha.value, 0, 1);

                    GUILayout.Space(10);
                    system.cloudsColor.value = EditorGUILayout.ColorField("shadows color", system.cloudsColor.value);

                    GUILayout.Space(10);
                    system.sunDirection.value = EditorGUILayout.Vector3Field("sun direction for shading", system.sunDirection.value);
                    system.smoothStep.value = EditorGUILayout.Toggle("alpha smoothstep", system.smoothStep.value);
                }
            }

            using (new LayoutUtils.FoldoutScope(false, ab3, out b3, "Speed Settings", true))
            {
                if (b3)
                {
                    system.scrollSpeed.value = EditorGUILayout.FloatField("texture scrolling-with-displacement factor", system.scrollSpeed.value);

                    system.speed1.value = EditorGUILayout.FloatField("speed of first cloud layer", system.speed1.value);
                    system.speed2.value = EditorGUILayout.FloatField("speed of second cloud layer", system.speed2.value);
                    GUILayout.Space(10);
                    system.cloudsDirection1.value = EditorGUILayout.Vector2Field("float direction of first cloud layer", system.cloudsDirection1.value);
                    system.cloudsDirection2.value = EditorGUILayout.Vector2Field("float direction of second cloud layer", system.cloudsDirection2.value);
                }
            }
        }
    }

}

#endif