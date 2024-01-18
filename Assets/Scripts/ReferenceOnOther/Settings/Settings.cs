using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public enum ControlType
{
    Personal,
    Mobile
}

namespace TheRavine.Base
{
    public class Settings : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private Toggle shadowToggle, joisticToggle, particlesToggle, postprocessingToggle;

        public static int SceneNumber;
        public static bool isLoad, isJoistick, isShadow, isParticles, isPostProcessing;
        public static ControlType _controlType;

        public void SetInitialValues()
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(QualitySettings.names.ToList());
            dropdown.value = QualitySettings.GetQualityLevel();
            joisticToggle.isOn = isJoistick;
            shadowToggle.isOn = isShadow;
        }

        public void SetJoistick()
        {
            isJoistick = joisticToggle.isOn;
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

        public void SetParticles()
        {
            isParticles = particlesToggle.isOn;
        }

        public void SetPostProcessing()
        {
            isPostProcessing = postprocessingToggle.isOn;
        }
    }
}