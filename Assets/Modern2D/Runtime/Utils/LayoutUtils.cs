using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

using UnityEditor.AnimatedValues;
using UnityEditor;

namespace Water2D
{
    public static class LayoutUtils
    {
    #if UNITY_EDITOR
        //from BennyKok github, extended by me
        public readonly struct FoldoutScope : IDisposable
        {
            private readonly bool wasIndent;
            private readonly bool fancy;

            public FoldoutScope(bool fancy, AnimBool value, out bool shouldDraw, string label, bool indent = true, SerializedProperty toggle = null)
            {
                this.fancy = fancy;
                value.target = Foldout(value.target, label, toggle);
                if (fancy) shouldDraw = EditorGUILayout.BeginFadeGroup(value.faded);
                else { EditorGUILayout.BeginVertical(); shouldDraw = value.target; }

                if (shouldDraw && indent)
                {
                    Indent();
                    wasIndent = true;
                }
                else
                {
                    wasIndent = false;
                }
            }

            public void Dispose()
            {
                if (wasIndent)
                    EndIndent();
                if (fancy) EditorGUILayout.EndFadeGroup();
                else EditorGUILayout.EndVertical();
            }
        }

        //from BennyKok github, extended by me
        public static void HorizontalLine(float height = 1, float width = -1, Vector2 margin = new Vector2())
        {
            GUILayout.Space(margin.x);

            var rect = EditorGUILayout.GetControlRect(false, height);
            if (width > -1)
            {
                var centerX = rect.width / 2;
                rect.width = width;
                rect.x += centerX - width / 2;
            }

            Color color = EditorStyles.label.active.textColor;
            color.a = 0.5f;
            EditorGUI.DrawRect(rect, color);

            GUILayout.Space(margin.y);
        }

        //from BennyKok github, extended by me
        public static bool Foldout(bool value, string label, SerializedProperty toggle = null)
        {
            bool _value;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            if (toggle != null && !toggle.boolValue)
            {
                EditorGUI.BeginDisabledGroup(true);
                _value = EditorGUILayout.Toggle(value, EditorStyles.foldout);
                EditorGUI.EndDisabledGroup();

                _value = false;
            }
            else
            {
                _value = EditorGUILayout.Toggle(value, EditorStyles.foldout);
            }

            if (toggle != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(toggle, GUIContent.none, GUILayout.Width(20));
                if (EditorGUI.EndChangeCheck() && toggle.boolValue)
                    _value = true;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            var rect = GUILayoutUtility.GetLastRect();
            rect.x += 20;
            rect.width -= 20;

            if (toggle != null && !toggle.boolValue)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
            }
            return _value;
        }

        public static void Indent()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            EditorGUILayout.BeginVertical();
        }

        public static void EndIndent()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    
    

    public static void StartVB(Color bg)
        {
            Rect rect = GUILayoutUtility.GetRect(1, 1);
            Rect vrect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x - 13, rect.y - 1, rect.width + 17, vrect.height + 9), bg);
        }

        public static void EndVB() { EditorGUILayout.EndVertical(); }

        public static Vector2 SliderV2(Vector2 vec, string x, string y, float l, float r) 
        {
            float diplx = vec.x;
            float diply = vec.y;
            diplx = EditorGUILayout.Slider(x, diplx, l, r);
            diply = EditorGUILayout.Slider(y, diply, l, r);
            vec = new Vector2(diplx, diply);
            return vec;
        }

        public static List<int> LayerList(this List<int> l)
        {
            var list = l;
            int newCount = Mathf.Max(0, EditorGUILayout.IntField("layers to render size", list.Count));
            while (newCount < list.Count)
                list.RemoveAt(list.Count - 1);
            while (newCount > list.Count)
                list.Add(0);

            for (int i = 0; i < list.Count; i++)
            {
                list[i] = EditorGUILayout.IntField("layer index : ", list[i]);
            }

            return list;
        }

        public static GUIStyle Header(int fontsize = 18, string color = "EDAE49", Font f = null)
        {

            GUIStyle header = new GUIStyle();
            if(f != null) header.font = f;
            header.fontSize = fontsize;
            header.fontStyle = FontStyle.Bold;
            header.alignment = TextAnchor.MiddleCenter;
            header.normal.textColor = ColorUtils.HexToRGB(color);
            return header;
        }

        public static GUIStyle Header1() 
        {

            GUIStyle header = new GUIStyle();
            header.fontSize = 26;
            header.fontStyle = FontStyle.Bold;
            header.alignment = TextAnchor.MiddleCenter;
            header.normal.textColor = ColorUtils.HexToRGB("EDAE49");
            return header;
        }

        public static GUIStyle Header2()
        {
            GUIStyle header = Header1();
            header.fontSize = 24;
            return header;
        }

        public static GUIStyle Header3()
        {
            GUIStyle header = Header1();
            header.fontSize = 22;
            return header;
        }

        public static GUIStyle Header4()
        {
            GUIStyle header = Header1();
            header.fontSize = 20;
            return header;
        }
    }
    #endif
}

#endif