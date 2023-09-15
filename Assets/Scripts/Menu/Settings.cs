using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class Settings : MonoBehaviour
{
    [SerializeField] private Dropdown dropdown;
    [SerializeField] private Toggle toggle;
    private void Awake()
    {
        dropdown.AddOptions(QualitySettings.names.ToList());
        dropdown.value = QualitySettings.GetQualityLevel();
        toggle.isOn = DayCycle.shadow;
    }

    public void SetQuality()
    {
        QualitySettings.SetQualityLevel(dropdown.value);
    }
    public void SetShadows()
    {
        DayCycle.shadow = toggle.isOn;
    }
}
