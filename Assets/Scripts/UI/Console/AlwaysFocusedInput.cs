using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class AlwaysFocusedInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    void Update()
    {
        inputField.ActivateInputField();
    }

}
