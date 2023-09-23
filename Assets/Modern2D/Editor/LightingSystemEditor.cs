using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Modern2D
{

    //  flag necressary for opengl builds because for some reason
    //  Unity includes scripts from editor folder in builds
#if UNITY_EDITOR

    [CustomEditor(typeof(LightingSystem))]
    public class LightingSystemEditor : Editor
    {

        [SerializeField] Texture2D myTexture;
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            GUIStyle style = new GUIStyle();
            style.stretchWidth = true;
            style.stretchHeight = true;
            style.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(myTexture, style);
            GUILayout.Space(10);

            GUIStyle header = new GUIStyle();
            header.fontStyle = FontStyle.Bold;
            header.normal.textColor = ColorUtils.HexToRGB("EDAE49");

            GUILayout.Space(20);
            GUILayout.Label("OPTIONS", header, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });

            LightingSystem system = (LightingSystem)target;

            GUILayout.Space(5); system._shadowColor.value = EditorGUILayout.ColorField("Ambient Color", system._shadowColor.value);
            GUILayout.Space(5); system._shadowReflectiveness.value = EditorGUILayout.Slider("Shadow Reflectiveness", system._shadowReflectiveness.value, 0, 1);
            GUILayout.Space(5); system._shadowAlpha.value = EditorGUILayout.Slider("Shadow Alpha", system._shadowAlpha.value, 0, 1);
            GUILayout.Space(5); system._shadowLength.value = EditorGUILayout.Slider("Shadow Length", system._shadowLength.value, 0, 5);
            GUILayout.Space(5); system._shadowNarrowing.value = EditorGUILayout.Slider("Shadow Narrowing", system._shadowNarrowing.value, 0, 1);
            GUILayout.Space(5); system._shadowFalloff.value = EditorGUILayout.Slider("Shadow Falloff", system._shadowFalloff.value, 0, 15);
            GUILayout.Space(5); system._shadowAngle.value = EditorGUILayout.Slider("Shadow Angle", system._shadowAngle.value, 0, 90);
            GUILayout.Space(5); system.ShadowsLayerName = EditorGUILayout.TextField("default shadow sorting layer", system.ShadowsLayerName);

            if (!system._useClosestPointLightForDirection || system.isLightDirectional.value)
            {
                GUILayout.Space(5); system._onlyRenderIn2DLight.value = EditorGUILayout.Toggle("Render In URP 2D Light Only", system._onlyRenderIn2DLight.value);

                if (system._onlyRenderIn2DLight)
                {
                    system._minimumAlphaOutOfLight.value = EditorGUILayout.Slider("Minimum alpha", system._minimumAlphaOutOfLight.value, 0f, 1f);
                    system._2dLightsShadowStrength.value = EditorGUILayout.FloatField("Strength in light", system._2dLightsShadowStrength.value);
                }
            }
            else
            {
                system._onlyRenderIn2DLight.value = true;
            }



            GUILayout.Space(10);
            Color backc; GUI.backgroundColor = GetFieldColor(system.followPlayer, out backc);
            system.followPlayer = (Transform)EditorGUILayout.ObjectField("Camera Transform", system.followPlayer, typeof(Transform), true);
            GUI.backgroundColor = backc;
            
            GUILayout.Space(20);
            GUILayout.Label("BLUR", header, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });

            GUILayout.Space(5);
            if (true)
            {
                system.enableBlur.value = GUILayout.Toggle(system.enableBlur.value, "enable blur");
                system.blurSampleSize.value = EditorGUILayout.IntSlider("sampling area", system.blurSampleSize.value, 1, 32);
                system.blurStrength.value = EditorGUILayout.Slider("blur strength", system.blurStrength.value, 0f, 1f);
                system.blurDirection.value = EditorGUILayout.Vector2Field("blur direction", system.blurDirection.value);
            }


            GUILayout.Space(15);
            header.normal.textColor = ColorUtils.HexToRGB("D1495B");
            GUILayout.Label("LIGHT SOURCE", header, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });

            system.SetCallbacks();
            system.Singleton();

            GUILayout.Space(5);
            GUILayout.Label("Directional -> uses one fake directional light for shadows");
            GUILayout.Label("Point Light Source -> uses one fake point light for shadows");
            GUILayout.Label("Point Light Source with Light 2D -> uses URP 2D spot lights for shadows");
            GUILayout.Space(5);

            if (system.isLightDirectional.value = GUILayout.Toggle(system.isLightDirectional.value, "Directional"))
                DirectionalFields(system);
            else
                SourceFields(system);


            GUILayout.Space(15);
            header.normal.textColor = ColorUtils.HexToRGB("08B2E3");
            GUILayout.Label("SHADOW SPRITE PIVOT", header, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });

            system._useSpritePivotForShadowPivot.value = GUILayout.Toggle(system._useSpritePivotForShadowPivot.value, " Use sprite-pivot as default shadow-pivot ");

            GUILayout.Space(15);
            header.normal.textColor = ColorUtils.HexToRGB("57A773");
            GUILayout.Label("SHADOW SPRITE FLIP-X", header, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });

            system.defaultShadowSprflipx = GUILayout.Toggle(system.defaultShadowSprflipx, "default shadow sprite flip-x");
            system.shadowSprFlip = GUILayout.Toggle(system.shadowSprFlip, "flips shadow sprite horizontaly based on orientation");

            GUILayout.Space(15);

            base.OnInspectorGUI();

            GUILayout.Space(15);

            if (GUILayout.Button("DELETE ALL SHADOWS"))
                foreach (var s in GameObject.FindGameObjectsWithTag("Shadow"))
                    DestroyImmediate(s);

            //this is an example of horribly unreadable code
            //don't try this at home kids
            if (GUILayout.Button("CREATE ALL SHADOWS"))
            {
                foreach (var s in Transform.FindObjectsOfType<StylizedShadowCaster2D>())
                {
                    s.RebuildShadow();
                    system.AddShadow(s.shadowData);
                    system.extendedUpdateThisFrame = true;
                    system.OnShadowSettingsChanged();
                    system.UpdateShadows(Transform.FindObjectsOfType<StylizedShadowCaster2D>().ToDictionary(t => t.transform, t => t.shadowData.shadow));
                }
            }
            if (GUILayout.Button("UPDATE ALL SHADOWS"))
            {
                system.extendedUpdateThisFrame = true;
                system.OnShadowSettingsChanged();
                system.UpdateShadows(Transform.FindObjectsOfType<StylizedShadowCaster2D>().ToDictionary(t => t.transform, t => t.shadowData.shadow));

            }


            SetLayers();

            if(EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(system);
        }

        private void DirectionalFields(LightingSystem system)
        {
            GUILayout.Label("direction:");
            GUILayout.Space(20);
            system.directionalLightAngle.value = GUILayout.HorizontalSlider((system.directionalLightAngle.value), 0, 359);
            GUILayout.Space(20);
        }

        private void SourceFields(LightingSystem system)
        {
            GUILayout.Space(5);
            system._useClosestPointLightForDirection.value = EditorGUILayout.Toggle("Use URP 2D Lights", system._useClosestPointLightForDirection.value);
            GUILayout.Space(5);

            if (!system._useClosestPointLightForDirection.value)
            {
                Color backc; GUI.backgroundColor = GetFieldColor(system.source, out backc);
                system.source = (Transform)EditorGUILayout.ObjectField("Light Source", system.source, typeof(Transform), true);
                GUI.backgroundColor = backc;

            }
            else
            {
                system._minimumAlphaOutOfLight.value = EditorGUILayout.Slider("Minimum alpha", system._minimumAlphaOutOfLight.value, 0f, 1f);
                system._2dLightsShadowStrength.value = EditorGUILayout.FloatField("Strength in light", system._2dLightsShadowStrength.value);
            }

            GUILayout.Space(5);
            system.distMinMax.value = EditorGUILayout.Vector2Field("shadow distance min max", system.distMinMax.value);
            GUILayout.Space(5);
            system.shadowLengthMinMax.value = EditorGUILayout.Vector2Field("shadow length multiplier min max", system.shadowLengthMinMax.value);

            
        }

        public Color GetFieldColor(Component c,out Color bgc) 
        {
            bgc = GUI.backgroundColor;
            if (c == null) return Color.red;
            else return GUI.backgroundColor;
        }

        private void SetLayers()
        {

            if (!Layers.LayerExists("LightingSystem"))
                if (!Layers.CreateLayer("LightingSystem"))
                    Debug.LogError("Not enough space for the LightingSystem layer, LightingSystem shadow detection won't work propetly \nPlease assign the LightingSystem layer or make space for it in your layers list");

            if (!Layers.LayerExists("2DWater"))
                if (!Layers.CreateLayer("2DWater"))
                    Debug.LogError("Not enough space for the Water layer, water won't work propetly \nPlease assign the Water layer or make space for it in your layers list");

            if (!Layers.TagExists("Shadow"))
                if (!Layers.CreateTag("Shadow"))
                    Debug.LogError("Not enough space for the Shadow tag, System won't be able to find shadows \nPlease assign the shadows tag or make space for it in your tags list");
        }

    }

    public class ColorUtils
    {

        public static bool IsNumeric(char c)
        {
            return (c >= '0' && c <= '9');
        }

        public static Color HexToRGB(string hex)
        {
            float r = (((IsNumeric(hex[0]) ? (int)(hex[0] - '0') : (int)(hex[0] - 'A' + 11)) * 16) + (IsNumeric(hex[1]) ? (int)(hex[1] - '0') : (int)(hex[1] - 'A'))) / 255f;
            float g = (((IsNumeric(hex[2]) ? (int)(hex[2] - '0') : (int)(hex[2] - 'A' + 11)) * 16) + (IsNumeric(hex[3]) ? (int)(hex[3] - '0') : (int)(hex[3] - 'A'))) / 255f;
            float b = (((IsNumeric(hex[4]) ? (int)(hex[4] - '0') : (int)(hex[4] - 'A' + 11)) * 16) + (IsNumeric(hex[5]) ? (int)(hex[5] - '0') : (int)(hex[5] - 'A'))) / 255f;
            return new Color(r, g, b);
        }
    }
#endif
}