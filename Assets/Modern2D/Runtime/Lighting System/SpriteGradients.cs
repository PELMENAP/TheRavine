using UnityEngine;
using System;

namespace Modern2D
{

    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class SpriteGradients : MonoBehaviour
    {


        [Header("Please set a material that inherits from the \n2DStylizedLit Shader")]
        [SerializeField] SpriteRenderer sr;

        Material _material;
        Material matInstance
        {
            get
            {
                if (Stylized2DLitMaterial == null)
                {
                    Debug.Log("Stylized2DLitMaterial hasn't been assigned on object : " + this.name);
                    return null;
                }

                if (_material == null)
                    _material = new Material(Stylized2DLitMaterial);
                sr.material = _material;
                return _material;
            }
            set { _material = value; }
        }

        void Awake() => sr = (sr == null ? GetComponent<SpriteRenderer>() : sr);

        void Start() => VariableChanged();

        [SerializeField] Material Stylized2DLitMaterial;
        public Color gradientTop = Color.white;
        public Color gradientRight = Color.white;
        public Color gradientLeft = Color.white;
        public Color gradientBottom = Color.white;
        [Range(0, 1f)] public float FalloffRight = 1;
        [Range(0, 1f)] public float FalloffLeft = 1;
        [Range(0, 1f)] public float FalloffBottom = 1;
        [Range(0, 1f)] public float FalloffTop = 1;
        [Range(0, 1f)] public float BotDarken = 0;
        [Range(0, 1f)] public float BotTransparency = 1;
        [Range(0, 1f)] public float smoothness = 1;

        private void OnValidate()
        {
            sr = (sr == null ? GetComponent<SpriteRenderer>() : sr);
            VariableChanged();
        }


        void VariableChanged()
        {
            if (matInstance == null)
                return;

            matInstance.SetColor("_gradientTop", gradientTop);
            matInstance.SetColor("_gradientRight", gradientRight);
            matInstance.SetColor("_gradientLeft", gradientLeft);
            matInstance.SetColor("_gradientBottom", gradientBottom);

            matInstance.SetFloat("_FalloffRight", FalloffRight);
            matInstance.SetFloat("_FalloffLeft", FalloffLeft);
            matInstance.SetFloat("_FalloffBottom", FalloffBottom);
            matInstance.SetFloat("_FalloffTop", FalloffTop);
            matInstance.SetFloat("_BotDarken", BotDarken);
            matInstance.SetFloat("_BotTransparency", BotTransparency);
            matInstance.SetFloat("_smoothness", smoothness);
        }
    }


}