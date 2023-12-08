using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public enum ControlType
{
    Personal,
    Mobile
}

public class Settings : MonoBehaviour
{
    [SerializeField] private Dropdown dropdown;
    [SerializeField] private Toggle shadowToggle;
    [SerializeField] private Toggle joisticToggle;

    public static int SceneNumber;
    public static bool isLoad, isJoistick, isShadow;
    public static ControlType _controlType;

    public void SetInitialValues()
    {
        dropdown.AddOptions(QualitySettings.names.ToList());
        dropdown.value = QualitySettings.GetQualityLevel();
        joisticToggle.isOn = isJoistick;
        shadowToggle.isOn = isShadow;
    }

    public void SetJoistick()
    {
        _controlType = joisticToggle.isOn ? ControlType.Mobile : ControlType.Personal;
    }

    public void SetQuality()
    {
        QualitySettings.SetQualityLevel(dropdown.value);
    }

    public void SetShadows()
    {
        isShadow = shadowToggle.isOn;
    }
}
