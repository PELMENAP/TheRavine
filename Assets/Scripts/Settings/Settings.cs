using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class Settings : MonoBehaviour
{
    [SerializeField] private Dropdown dropdown;
    [SerializeField] private Toggle shadowToggle;
    [SerializeField] private Toggle joisticToggle;

    public static int SceneNumber;
    public static bool isLoad, isJoistick, isShadow;

    public void SetInitialValues()
    {
        dropdown.AddOptions(QualitySettings.names.ToList());
        dropdown.value = QualitySettings.GetQualityLevel();
        joisticToggle.isOn = isJoistick;
        shadowToggle.isOn = isShadow;
    }

    public void SetJoistick(){
        isJoistick = joisticToggle.isOn;
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
