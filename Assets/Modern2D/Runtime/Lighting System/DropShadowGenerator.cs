using UnityEngine;

namespace Modern2D
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class DropShadowGenerator : MonoBehaviour
    {
        public static readonly string shadowLayer = "Shadows";
        SpriteRenderer sr;

        public void GenerateShadow()
        {
            this.sr = GetComponent<SpriteRenderer>();
            GameObject shadow = new GameObject(name + " drop shadow");
            SpriteRenderer sr = shadow.AddComponent<SpriteRenderer>();
            if (!ownMaterialInstance)
                sr.material = LightingSystem.system.dropShadowDefaultMaterial;
            if (ownMaterialInstance)
            {
                sr.sharedMaterial = new Material(LightingSystem.system.dropShadowDefaultMaterial);
                sr.sharedMaterial.name = LightingSystem.system.dropShadowDefaultMaterial.name + gameObject.name;
            }
            sr.sprite = this.sr.sprite;
            sr.sortingLayerName = shadowLayer;
            sr.transform.parent = transform;
            sr.transform.localPosition = Vector3.zero;
        }

        [Tooltip("changing values in the shader won't change the values of other drop shadows, but costs preformance")]
        public bool ownMaterialInstance = false;
    }

}