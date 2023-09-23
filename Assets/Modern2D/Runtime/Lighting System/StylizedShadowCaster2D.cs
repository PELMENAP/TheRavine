using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modern2D
{

    //  shadow type that is used by stylized lighting 2D system
    //  stylized lighting 2D system needs to be setup beforehand
    //  it also needs an "Shadows" sprite sorting layer and "Shadow" tag
    //  for detailed tutorial please read the whole setup section in my documentation

    [ExecuteAlways]
    [Serializable]
    public class StylizedShadowCaster2D : MonoBehaviour
    {
        [SerializeField] private ShadowData _shadowData;

        [SerializeField] [HideInInspector] public Cryo<bool> flipShadowX;

        [Tooltip("Color that's applied to shadow color calculation and other shaders")]
        [SerializeField] [HideInInspector] public Cryo<Color> _shadowColor;

        [Tooltip("special abstract property of the shadow that's responsible for the illusion of shadow reflecting shadowcaster")]
        [SerializeField] [HideInInspector] public Cryo<float> _shadowReflectiveness;

        [Tooltip("Alpha of shadow color that's applied to shadow color calculation and other shaders")]
        [SerializeField] [HideInInspector] public Cryo<float> _shadowAlpha;

        [Tooltip("Shadow Narrowing of the drop shadow in shadowcasters")]
        [SerializeField] [HideInInspector] public Cryo<float> _shadowNarrowing;

        [Tooltip("Shadow Falloff of the drop shadow in shadowcasters")]
        [SerializeField] [HideInInspector] public Cryo<float> _shadowFalloff;

        [SerializeField] [HideInInspector] public Cryo<float> _pivotOffsetX;
        [SerializeField] [HideInInspector] public Cryo<float> _pivotOffsetY;

        [SerializeField] [HideInInspector] public Cryo<bool> customShadowLayer;

        [SerializeField] [HideInInspector] public Cryo<string> customShadowLayerName;

        [SerializeField] [HideInInspector] public bool extendedProperties;

        [SerializeField] [HideInInspector] private MaterialPropertyBlock _propBlock;

        [SerializeField] [HideInInspector] public Cryo<bool> overrideCustomPivot;

        [SerializeField] [HideInInspector] public PivotSourceMode customPivot;

        [SerializeField] [HideInInspector] public Transform customPivotTransform;

        // bug fix for version 2.0.1v 
        // after changing the parent class from scriptable object to none,
        // error "is missing class attribute 'Shadow Data, Extenstion of Native class' appears"
        // in order to fix it, all shadows need to be rebuilded one time

        [SerializeField][HideInInspector] private bool rebuildShadowsBF_2_0_1_v = true;

        // end 

        public void SetCallbacks()
        {
            _shadowColor.onValueChanged = SetPropBlock;
            _shadowReflectiveness.onValueChanged = SetPropBlock;
            _shadowAlpha.onValueChanged = SetPropBlock;
            _shadowNarrowing.onValueChanged = SetPropBlock;
            _shadowFalloff.onValueChanged = SetPropBlock;
            overrideCustomPivot.onValueChanged = PivotOptionsChanged;
            flipShadowX.onValueChanged = PivotOptionsChanged;
            _pivotOffsetY.onValueChanged = PivotOptionsChanged;
            _pivotOffsetX.onValueChanged = PivotOptionsChanged;
        }

        public void PivotOptionsChanged() 
        {
     
            if(overrideCustomPivot.value == false) customPivot = LightingSystem.system._useSpritePivotForShadowPivot.value == true ? PivotSourceMode.sprite : PivotSourceMode.auto;
            RebuildShadow();
        }

        public void Start()
        {
            if (extendedProperties) SetPropBlock();
            if (IsFromSpriteSheet(shadowData.shadow.shadowSr.sprite)) SetSpriteSheetData();
            CreateShadow();

            // bug fix for version 2.0.1v 
            if(shadowData != null && rebuildShadowsBF_2_0_1_v)
            {
                rebuildShadowsBF_2_0_1_v = false;
                _shadowData = CreateShadowData();
            }
        }

        private void OnValidate()
        {
            if (extendedProperties) SetPropBlock();
        }

        public static bool IsFromSpriteSheet(Sprite s)
        {
            if (s.textureRect.xMax - s.textureRect.xMin >= (s.texture.width - 2) && s.textureRect.yMax - s.textureRect.yMin >= (s.texture.height - 2)) return false;

            return true;
        }

        public void SetSpriteSheetData()
        {
        
            _propBlock = new MaterialPropertyBlock();

            if (shadowData.shadow.shadowSr == null || shadowData.shadow.shadowSr.sprite == null)
            {
                Debug.LogError("Can't change properties : create a shadow first!");
                return;
            }

            shadowData.shadow.shadowSr.GetPropertyBlock(_propBlock);

            float texW = shadowData.shadow.shadowSr.sprite.texture.width;
            float texH = shadowData.shadow.shadowSr.sprite.texture.height;

            _propBlock.SetInt("_fromSS",1);
            _propBlock.SetFloat("_minX",(shadowData.shadow.shadowSr.sprite.rect.xMin / texW) );
            _propBlock.SetFloat("_maxX",(shadowData.shadow.shadowSr.sprite.rect.xMax / texW));
            _propBlock.SetFloat("_minY",(shadowData.shadow.shadowSr.sprite.rect.yMin / texH));
            _propBlock.SetFloat("_maxY",(shadowData.shadow.shadowSr.sprite.rect.yMax / texH));
            shadowData.shadow.shadowSr.SetPropertyBlock(_propBlock);
        }

        public void SetPropBlock()
        {
            if (!extendedProperties) return;
            _propBlock = new MaterialPropertyBlock();

            if (shadowData.shadow.shadowSr == null || shadowData.shadow.shadowSr.sprite == null)
            {
                Debug.LogError("Can't change properties : create a shadow first!");
                return;
            }

            shadowData.shadow.shadowSr.GetPropertyBlock(_propBlock);
            
            _propBlock.SetColor("_shadowBaseColor", _shadowColor.value);
            _propBlock.SetFloat("_shadowBaseAlpha", _shadowAlpha.value);
            _propBlock.SetFloat("_shadowReflectiveness", _shadowReflectiveness.value);
            _propBlock.SetFloat("_shadowNarrowing", _shadowNarrowing.value);
            _propBlock.SetFloat("_shadowFalloff", _shadowFalloff.value);

            shadowData.shadow.shadowSr.SetPropertyBlock(_propBlock);
        }


        /// <summary>
        /// if shadow wasn't created, automatically creates one
        /// </summary>
        public ShadowData shadowData
        {
            get { if (_shadowData == null || _shadowData.shadow.shadowPivot == null) _shadowData = CreateShadowData(); return _shadowData; }
            set { _shadowData = value; }

        }

        /// <summary>
        /// creates shadow or returns existent one
        /// </summary>
        public void CreateShadow()
        {
            if(CanCreateShadow()) LightingSystem.system.AddShadow(shadowData);
        }

        /// <summary>
        /// if shadow exists, it destroys it and creates a new one
        /// </summary>
        public void RebuildShadow()
        {
            if (shadowData!= null && shadowData.shadow.shadowPivot!=null) _shadowData = CreateShadowData(shadowData.shadow.shadowPivot, shadowData.shadow.shadow);
            if (CanCreateShadow()) LightingSystem.system.AddShadow(shadowData);
        }

        /// <summary>
        /// checks if object have a shadow, omitting shadowData getter
        /// </summary>
        /// <returns></returns>
        public bool HasShadow() => _shadowData == null;

        public bool CanCreateShadow() 
        {
            if (GetComponent<SpriteRenderer>()==null) return false;
            if (GetComponent<SpriteRenderer>().sprite == null) return false;
            return true;
        }

        //needed if you use a prefarb in the scene, as prefarbs can't save scriptable objects (dirty fix)
        private void CleanOldShadow() 
        {
            foreach(var child in GetComponentsInChildren<Transform>())
            {
                if (child.tag == "Shadow")
                {   
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private bool ShadowPivotExist(out Transform pivot)
        {
            pivot = null;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).tag == "Shadow")
                { 
                    pivot = transform.GetChild(i);
                    return true;
                }
            return false;
        }

        private ShadowData CreateShadowData(Transform pivot = null, Transform casterT = null)
        {

            if(!CanCreateShadow()) return null;
            ShadowData data = new ShadowData();


            StylizedShadowCaster caster = new StylizedShadowCaster(transform, null, null, null, Vector2.zero, customPivot,customPivotTransform, this, flipShadowX.value, customShadowLayer, customShadowLayerName);

            GameObject parent = caster.shadowCaster.gameObject;
            GameObject shadowGO;

            Transform repivot;
            bool shadowPivotExist = ShadowPivotExist(out repivot);
            
            if(pivot!=null)
            {
                caster.shadowPivot = pivot;
                shadowGO = casterT.gameObject;
            }
            else if (shadowPivotExist)
            {
                shadowGO = repivot.GetChild(0).gameObject;
                caster.shadowPivot = repivot;
            }
            else
            {

                shadowGO = new GameObject(parent.name + " : shadow");
                CreatePivot(ref caster);

            }

            caster.shadowPivot.parent = parent.transform;
            caster.shadow = shadowGO.transform;
            caster.shadow.parent = caster.shadowPivot.transform;
            shadowGO.transform.localRotation = Quaternion.identity;
            shadowGO.transform.localPosition = Vector3.zero;

            caster.shadowSr = (shadowGO.GetComponent<SpriteRenderer>() == null ? shadowGO.AddComponent<SpriteRenderer>() : shadowGO.GetComponent<SpriteRenderer>());

            caster.shadowSr.sortingLayerName = !customShadowLayer.value ? (LightingSystem.system!=null? LightingSystem.system.ShadowsLayerName : "default" ) : customShadowLayerName.value;
            caster.shadowSr.sortingOrder = 1;

            caster.shadowSr.material = LightingSystem.system.shadowsMaterial;
            caster.shadowCasterSr = parent.GetComponent<SpriteRenderer>();
            caster.shadowSr.sprite = caster.shadowCasterSr.sprite;
            caster.pivotOffset.x = _pivotOffsetX.value;
            caster.pivotOffset.y = _pivotOffsetY.value;

            if (LightingSystem.system.shadowSprFlip)
            {
                if (Mathf.Abs(caster.shadowPivot.eulerAngles.z % 360) < 90 || Mathf.Abs(caster.shadowPivot.eulerAngles.z % 360) > 270) caster.shadowSr.flipX = LightingSystem.system.defaultShadowSprflipx;
                else caster.shadowSr.flipX = !LightingSystem.system.defaultShadowSprflipx;
            }
            else caster.shadowSr.flipX = LightingSystem.system.defaultShadowSprflipx;

            caster.shadow.localScale = Vector3.one;

            data.shadow = caster;

            return data;
        }

        /// <summary>
        /// creates or reuses already created pivot
        /// </summary>
        /// <param name="caster"></param>
        private void CreatePivot(ref StylizedShadowCaster caster)
        {
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).tag == "Shadow")
                {
                    caster.shadowPivot = transform.GetChild(i);
                    return;
                }

            GameObject shadowPivot = new GameObject(caster.shadowCaster.name + " : shadowPivot");
            caster.shadowPivot = shadowPivot.transform;
            caster.shadowPivot.tag = "Shadow";

            CircleCollider2D c = caster.shadowPivot.gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 0.3f;

            caster.shadowPivot.parent = caster.shadowCaster;
            caster.shadowPivot.localScale = Vector3.one;
        }
    }


    public static class TransformExtensionMethods
    {
        public static void SetGlobalScale(this Transform transform, Vector3 globalScale)
        {
            Transform parentBefore = transform.parent;
            transform.parent = null;
            transform.localScale = globalScale;
            transform.parent = parentBefore;
        }
    }

    


    /// <summary>
    /// Source of the shadow pivot point : 
    /// auto -> auto generation with algorithm (default)
    /// sprite -> takes sprite source pivot as shadow pivot
    /// custom -> you can set the pivot yourself via setting Transform Point in Unity Inspector
    /// </summary>
    public enum PivotSourceMode
    {
        auto,
        sprite,
        custom
    }

}
