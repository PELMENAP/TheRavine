using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonAudioController : MonoBehaviour
{
    private Button gameButton;
    [SerializeField] private UISoundType soundType = UISoundType.Click;
    private void OnEnable()
    {
        gameButton ??= GetComponent<Button>();
        gameButton.onClick.AddListener(OnClickAudio);
    }

    private void OnClickAudio()
    {
        UISoundSystem.Instance.Play(soundType);
    }

    private void OnDisable()
    {
        gameButton?.onClick.RemoveListener(OnClickAudio);
    }
}
