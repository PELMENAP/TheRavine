using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ConditionalFieldAttribute : PropertyAttribute
{
    public string ConditionFieldName { get; }
    public object ExpectedValue { get; }
    public bool Inverse { get; }

    public ConditionalFieldAttribute(string conditionFieldName, object expectedValue, bool inverse = false)
    {
        ConditionFieldName = conditionFieldName;
        ExpectedValue = expectedValue;
        Inverse = inverse;
    }
}