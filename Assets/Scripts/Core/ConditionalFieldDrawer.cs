#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;


[CustomPropertyDrawer(typeof(ConditionalFieldAttribute))]
public class ConditionalFieldDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
            return;

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
            return 0f;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        var conditionalAttribute = attribute as ConditionalFieldAttribute;
        var targetObject = property.serializedObject.targetObject;
        var conditionField = targetObject.GetType().GetField(conditionalAttribute.ConditionFieldName, 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (conditionField == null)
            return true;

        var currentValue = conditionField.GetValue(targetObject);
        bool matches = Equals(currentValue, conditionalAttribute.ExpectedValue);

        return conditionalAttribute.Inverse ? !matches : matches;
    }
}
#endif