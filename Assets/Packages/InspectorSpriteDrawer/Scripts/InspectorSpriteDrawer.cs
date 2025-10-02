#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace DarkHex.InspectorSpriteDrawer
{
    [CustomPropertyDrawer(typeof(Sprite))]
    public class InspectorSpriteDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect objectFieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.objectReferenceValue = EditorGUI.ObjectField(objectFieldRect, label, property.objectReferenceValue, typeof(Sprite), false);

            if (property.objectReferenceValue != null)
            {
                Sprite sprite = property.objectReferenceValue as Sprite;
                if (sprite != null)
                {
                    Texture2D tex = sprite.texture;
                    Rect spriteRect = sprite.textureRect;
                    Rect uv = new Rect(
                        spriteRect.x / tex.width,
                        spriteRect.y / tex.height,
                        spriteRect.width / tex.width,
                        spriteRect.height / tex.height
                    );
                    Rect previewRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, 100, 100);
                    GUI.DrawTextureWithTexCoords(previewRect, tex, uv, true);
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.objectReferenceValue != null)
            {
                height += 2 + 100;
            }
            return height;
        }
    }
}
#endif