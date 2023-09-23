using UnityEngine;

namespace Modern2D
{

    [RequireComponent(typeof(SpriteRenderer))]
    public class LightRay : MonoBehaviour
    {
        // You can make a more advanced version of the script that fades the ray
        // based on the distance from the closest point to the camera bounds
        // I will probably add it in the next update

        [Tooltip("Minimal distance to the camera (crossing the distance will start the fade effect)")]
        [SerializeField] float distStartFade = 6;
        [Tooltip("Length of the fading")]
        [SerializeField] float FadeLength = 2;

        Material rayMaterial;

        void Awake() => rayMaterial = GetComponent<SpriteRenderer>().material;

        private void Update()
        {
            Camera cam = Camera.main;
            float dist = Vector2.Distance(cam.transform.position, transform.position);

            if (dist > distStartFade)
                return;
            float t = (distStartFade - dist) / FadeLength;
            rayMaterial.SetFloat("right", Mathf.Lerp(1, 5, t));
        }

    }

}