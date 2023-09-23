using UnityEngine.UI;
using UnityEngine;

public class LikeAFader : MonoBehaviour
{
    [SerializeField] private Image fader;
    [SerializeField] private Image other;
    void Update()
    {

        other.color = new Color(other.color.r, other.color.g, other.color.b, fader.color.a);
    }
}
