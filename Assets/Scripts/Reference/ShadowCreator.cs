using UnityEngine;
using Cysharp.Threading.Tasks;

namespace TheRavine.EntityControl
{
    public class ShadowCreator : MonoBehaviour
    {
        [SerializeField] private Color defColor;
        [SerializeField] private Vector3 shadowScale = new Vector3(1f, 1.1f, 1f), shadowRotation = new Vector3(50, 0, 300), shadowPosition = new Vector3(0, 0.1f, 0); 
        public GameObject shadow;
        
        public bool isAlreadyCreated = false;

        private void Start()
        {
            SpawnShadow().Forget();
        }
        private async UniTaskVoid SpawnShadow() 
        {
            await UniTask.Delay(50);

            if(isAlreadyCreated) return;

            shadow = Instantiate(this.gameObject);
            shadow.GetComponent<ShadowCreator>().isAlreadyCreated = true;

            shadow.tag = "Shadow";

            Transform shadowTransform = shadow.transform;
            shadowTransform.SetParent(this.transform);
            shadowTransform.localPosition = shadowPosition;
            shadowTransform.localScale = shadowScale;
            shadowTransform.localEulerAngles = shadowRotation;

            SpriteRenderer spriteRenderer = shadow.GetComponent<SpriteRenderer>();
            spriteRenderer.flipX = true;
            spriteRenderer.color = defColor;
            spriteRenderer.sortingOrder = -10;

            SpriteRenderer[] spriteRenderers = shadow.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].color = defColor;
                spriteRenderers[i].sortingOrder = -10;
            }
        }
    }
}