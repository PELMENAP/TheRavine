using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxEffect : MonoBehaviour
{
    [SerializeField] private float value;
    private void Start() {
        this.GetComponent<SpriteRenderer>().material.SetFloat("_Addition", value);
    }
}
