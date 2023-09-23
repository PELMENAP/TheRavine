using UnityEditor;
using UnityEngine;

namespace Modern2D
{

    //  flag necressary for opengl builds because for some reason
    //  Unity includes scripts from editor folder in builds
#if UNITY_EDITOR


    [CanEditMultipleObjects]
    [CustomEditor(typeof(StylizedShadowCaster2D))]
    public class StylizedShadowCaster2DEditor : Editor
    {
        StylizedShadowCaster2D system;

        [System.Obsolete]
#pragma warning disable CS0809 
        public override void OnInspectorGUI()
#pragma warning restore CS0809 
        {
            EditorGUI.BeginChangeCheck();

            base.OnInspectorGUI();
            GUILayout.Space(5); if (GUILayout.Button("Create Shadow"))
                foreach (StylizedShadowCaster2D caster in targets)
                    caster.CreateShadow();
            GUILayout.Space(5); if (GUILayout.Button("Rebuild Shadow"))
                foreach (StylizedShadowCaster2D caster in targets)
                    caster.RebuildShadow();
            GUILayout.Space(5); if (GUILayout.Button("Update Options"))
                foreach (StylizedShadowCaster2D caster in targets)
                    caster.CreateShadow();

            system = (StylizedShadowCaster2D)target;

            GUILayout.Space(10);

            system._pivotOffsetX.value = EditorGUILayout.Slider("pivot offset X ", system._pivotOffsetX.value, -5f, 5f);
            system._pivotOffsetY.value = EditorGUILayout.Slider("pivot offset Y ", system._pivotOffsetY.value, -5f, 5f);

            GUILayout.Space(10);
            system.flipShadowX.value = GUILayout.Toggle(system.flipShadowX.value, "Flip X");

            system.overrideCustomPivot.value = GUILayout.Toggle(system.overrideCustomPivot.value, "Override default pivot source");
            if (system.overrideCustomPivot.value)
            {
                PivotOptions(system);
            }

            GUILayout.Space(10);

            if (system.customShadowLayer.value = GUILayout.Toggle(system.customShadowLayer.value, "Override shadow sorting layer"))
            {
                GUILayout.Space(5);
                GUILayout.Label("shadow sorting layer name : ");
                system.customShadowLayerName.value = GUILayout.TextField(system.customShadowLayerName.value);
            }

            GUILayout.Space(10);

            system.extendedProperties = GUILayout.Toggle(system.extendedProperties, "Per Every Shadow Properties [Warning : GPU Expensive] \n (if possible, keep shadows the same and change values via StylizedLightingSystem2D)");
            system.SetCallbacks();
            if (system.extendedProperties)
            {
                system.SetCallbacks();

                GUILayout.Space(5); system._shadowColor.value = EditorGUILayout.ColorField("Ambient Color", system._shadowColor.value);
                GUILayout.Space(5); system._shadowReflectiveness.value = EditorGUILayout.Slider("Shadow Reflectiveness", system._shadowReflectiveness.value, 0, 1);
                GUILayout.Space(5); system._shadowAlpha.value = EditorGUILayout.Slider("Shadow Alpha", system._shadowAlpha.value, 0, 1);
                GUILayout.Space(5); system._shadowNarrowing.value = EditorGUILayout.Slider("Shadow Narrowing", system._shadowNarrowing.value, 0, 1);
                GUILayout.Space(5); system._shadowFalloff.value = EditorGUILayout.Slider("Shadow Falloff", system._shadowFalloff.value, 0, 15);
            }

            if(EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(system);
            }

        }

        [System.Obsolete]
        private void PivotOptions(StylizedShadowCaster2D system) 
        {
            var a = system.customPivot;
            system.customPivot = (PivotSourceMode)EditorGUILayout.EnumPopup(system.customPivot);
            if (a != system.customPivot) system.overrideCustomPivot.onValueChanged.Invoke();

            if (system.customPivot == PivotSourceMode.custom)
            {
                system.customPivotTransform = EditorGUILayout.ObjectField(system.customPivotTransform,typeof(Transform) ) as Transform;
            }

        }
    }

#endif
}