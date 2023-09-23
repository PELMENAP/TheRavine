using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using System.Linq;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering.Universal;
using System;


namespace Modern2D
{

    [ExecuteAlways]
	[AddComponentMenu("Light/2D Stylized Lighting")]
	public class LightingSystem : MonoBehaviour
	{

        #region Start/Awake

        void Start()
        {
            SetCallbacks();
			SetCollidersCallbacks();
			SetWaterTexture();
            OnShadowSettingsChanged();
        }


        private void Awake()
		{
			Singleton();
			SetPivotCorrectionsDictionaries();
			OnShadowSettingsChanged();
        }


		[SerializeField] public static LightingSystem system;

        void OnEnable() => Singleton();
        void OnDisable()  => Singleton();
        public void Singleton()
		{
			if (system == null || system.gameObject == this.gameObject)
				system = this;
			else if (system.gameObject != this.gameObject)
				if (Application.isPlaying)
					Destroy(this);
		}

        #endregion

        #region PivotCorrections

        public static List<DictPair<T,T1>> GetNonNullValues<T,T1>( List<DictPair<T, T1>> items) where T : class
        {
            return items.Where(a => a.key != null).Select(a => a).ToList();
        }

        public void SetPivotCorrectionsDictionaries()
        {
			pivotCorrections = GetNonNullValues(pivotCorrections);
            spritePivotCorrections = pivotCorrections.ToDictionary(t =>  t.key, t => t.val);
            spritePivotCorrections = PivotGroupAddToDict(spritePivotCorrections, pivotCorrectionsGroups);

            pivotCorrectionsSpriteSheet = GetNonNullValues(pivotCorrectionsSpriteSheet);
            spritePivotCorrectionsSpriteSheet = pivotCorrectionsSpriteSheet.ToDictionary(t => t.key, t => t.val);
			spritePivotCorrectionsSpriteSheet = SSPivotGroupAddToDict(spritePivotCorrectionsSpriteSheet, pivotCorrectionsSpriteSheetGroups);
        }

        public Dictionary<Texture, Vector2> PivotGroupAddToDict(Dictionary<Texture, Vector2> dict , List<DictPair<List<Texture>, Vector2>> d)
		{
			foreach(var dictpar in d)
			{
				foreach(var t in dictpar.key) dict.Add(t, dictpar.val);
			}
			return dict;

        }

        public Dictionary<Sprite, Vector2> SSPivotGroupAddToDict(Dictionary<Sprite, Vector2> dict, List<DictPair<List<Sprite>, Vector2>> d)
        {
            foreach (var dictpar in d)
            {
                foreach (var t in dictpar.key) dict.Add(t, dictpar.val);
            }
			return dict;
        }

        public List<DictPair<Texture, Vector2>> pivotCorrections = new List<DictPair<Texture, Vector2>>();
        public List<DictPair<List<Texture>, Vector2>> pivotCorrectionsGroups = new List<DictPair<List<Texture>, Vector2>>();

        [SerializeField] [HideInInspector] Dictionary<Texture, Vector2> _spritePivotCorrections;

		/// <summary>
		/// used for correcting spriteNewPivots pivot positions by manually cached Vector2 value
		/// </summary>
		public Dictionary<Texture, Vector2> spritePivotCorrections
		{
			get
			{
				if (_spritePivotCorrections == null)
					_spritePivotCorrections = new Dictionary<Texture, Vector2>();
				return _spritePivotCorrections;
			}
			set { _spritePivotCorrections = value; }
		}

		public List<DictPair<Sprite, Vector2>> pivotCorrectionsSpriteSheet = new List<DictPair<Sprite, Vector2>>();
		public List<DictPair<List<Sprite>, Vector2>> pivotCorrectionsSpriteSheetGroups = new List<DictPair<List<Sprite>, Vector2>>();
		
		[SerializeField] [HideInInspector] Dictionary<Sprite, Vector2> _spritePivotCorrectionsSpriteSheet;

		/// <summary>
		/// used for correcting spriteNewPivots pivot positions by manually cached Vector2 value
		/// </summary>
		public Dictionary<Sprite, Vector2> spritePivotCorrectionsSpriteSheet
		{
			get
			{
				if (_spritePivotCorrectionsSpriteSheet == null)
					_spritePivotCorrectionsSpriteSheet = new Dictionary<Sprite, Vector2>();
				return _spritePivotCorrectionsSpriteSheet;
			}
			set { _spritePivotCorrectionsSpriteSheet = value; }
		}

		//used for corrected pivot points
		Dictionary<Texture, Vector2> _spriteNewPivots;

		/// <summary>
		/// used for placing shadows at the bottom of sprites
		/// </summary>
		Dictionary<Texture, Vector2> spriteNewPivots
		{
			get
			{
				if (_spriteNewPivots == null)
					_spriteNewPivots = new Dictionary<Texture, Vector2>();
				return _spriteNewPivots;
			}
			set { _spriteNewPivots = value; }
		}

		//used for corrected pivot points
		Dictionary<Sprite, Vector2> _spriteNewPivotsSpriteSheets;

		/// <summary>
		/// used for placing shadows at the bottom of sprites
		/// </summary>
		Dictionary<Sprite, Vector2> spriteNewPivotsSpriteSheets
		{
			get
			{
				if (_spriteNewPivotsSpriteSheets == null)
					_spriteNewPivotsSpriteSheets = new Dictionary<Sprite, Vector2>();
				return _spriteNewPivotsSpriteSheets;
			}
			set { _spriteNewPivotsSpriteSheets = value; }
		}

		Dictionary<Transform, StylizedShadowCaster> _shadows;
		Dictionary<Transform, StylizedShadowCaster> shadows
		{
			get
			{
				if (_shadows == null)
					_shadows = new Dictionary<Transform, StylizedShadowCaster>();
				return _shadows;
			}
			set { _shadows = value; }
		}

		#endregion

		#region variables

		[Header("Light Source Settings")]

		[Tooltip("decides if light comes from one direction(true), or from a certain source(false)")]
		[HideInInspector] public Cryo<bool> isLightDirectional = new Cryo<bool>(true);
		[Tooltip("if light is Directional, it comes from this angle")]
		[HideInInspector] public Cryo<float> directionalLightAngle = new Cryo<float>(60f);
		[Tooltip("if light is from a source, it comes from this source")]
        [HideInInspector] public Transform source;

        [HideInInspector] public Cryo<bool> enableBlur = new Cryo<bool>(false);

        [HideInInspector] public Cryo<int> blurSampleSize = new Cryo<int>(4);

        [HideInInspector] public Cryo<float> blurStrength = new Cryo<float>(1);

        [HideInInspector] public Cryo<Vector2> blurDirection = new Cryo<Vector2>(new Vector2(1,1));

        [Header("Global Shadow Settings")]

		[Tooltip("Change it to disable and enable shadows")]
		[HideInInspector] public static Cryo<bool> showShadows = new Cryo<bool>(true);

		[Tooltip("Color that's applied to shadow color calculation and other shaders")]
		[HideInInspector] public Cryo<Color> _shadowColor = new Cryo<Color>(Color.black);

		[Tooltip("special abstract property of the shadow that's responsible for the illusion of shadow reflecting shadowcaster")]
		[HideInInspector] public Cryo<float> _shadowReflectiveness = new Cryo<float>(0.117f);

		[Tooltip("Alpha of shadow color that's applied to shadow color calculation and other shaders")]
		[HideInInspector] public Cryo<float> _shadowAlpha = new Cryo<float>(0.547f);

		[Tooltip("Angle of the drop shadow in shadowcasters")]
		[HideInInspector] public Cryo<float> _shadowAngle = new Cryo<float>(42f);

		[Tooltip("Shadow Length of the drop shadow in shadowcasters")]
		[HideInInspector] public Cryo<float> _shadowLength = new Cryo<float>(1f);

		[Tooltip("Shadow Narrowing of the drop shadow in shadowcasters")]
		[HideInInspector] public Cryo<float> _shadowNarrowing = new Cryo<float>(0.6f);

		[Tooltip("Shadow Falloff of the drop shadow in shadowcasters")]
		[HideInInspector] public Cryo<float> _shadowFalloff = new Cryo<float>(5.5f);

        [Tooltip("Shows shadow if it's in the 2d light")]
        [HideInInspector] public Cryo<bool> _onlyRenderIn2DLight = new Cryo<bool>(false);

        [Tooltip("2d light shadow strength multiplayer")]
        [HideInInspector] public Cryo<float> _2dLightsShadowStrength = new Cryo<float>(0.2f);

        [Tooltip("2d light shadow strength multiplayer")]
        [HideInInspector] public Cryo<float> _minimumAlphaOutOfLight = new Cryo<float>(0.2f);

        [Tooltip("Uses point lights for shadow projection")]
		[HideInInspector] public Cryo<bool> _useClosestPointLightForDirection = new Cryo<bool>(false);

        [Header("DEPENDENCIES")]

		[Tooltip("Material that determines how the shadows are rendered")]
        [SerializeField] [HideInInspector] private Material _shadowsMaterial;
        [SerializeField] [HideInInspector] private Material _dropShadowDefaultMaterial;

        [SerializeField] public Material shadowsMaterial
        {
            get { if (_shadowsMaterial == null) _shadowsMaterial = (Material)Resources.Load("Materials/shadow materials/StylizedShadows2DMat"); return _shadowsMaterial; }
            set { _shadowsMaterial = value; }
        }

        [SerializeField] public Material dropShadowDefaultMaterial
		{ 
			get { if (_dropShadowDefaultMaterial == null) _dropShadowDefaultMaterial = (Material)Resources.Load("Materials/shadow materials/DropShadowDefault"); return _dropShadowDefaultMaterial; }
			set { _dropShadowDefaultMaterial = value; }
		}

        [Tooltip("Treshold for omitting transparent elements on sprites")]
		[SerializeField] [HideInInspector] int pivotDetectionAlphaTreshold = 122;

		private const int LIGHT2DNOTFOUND = 1000;

        [Header("SHADOW CULLING")]

        [Tooltip("Used for optimizing Shadows updates")]
		[SerializeField] public BoxCollider2D enterCollider;
        [Tooltip("Used for optimizing Shadows updates")]
        [SerializeField] public BoxCollider2D exitCollider;

		[HideInInspector] public Cryo<Vector2> distMinMax;
		[HideInInspector] public Cryo<Vector2> shadowLengthMinMax;
		[HideInInspector] public Transform _followPlayer;
		[HideInInspector] public Transform followPlayer
		{
			get
			{
				if (_followPlayer == null) return Camera.main.transform;
				return _followPlayer;
			}
			set
			{
				_followPlayer = value;
			}
		}

		[HideInInspector] public bool extendedUpdateThisFrame = false;
		[Tooltip("Changes the default shadow-pivot source from auto-generated to source-sprite pivot")]
		[HideInInspector] public Cryo<bool> _useSpritePivotForShadowPivot = new Cryo<bool>(false);

		[Tooltip("Flips your shadow sprite depending on its direction (90-270degrees -> flip-x true , 270-90 degrees -> flip-x-false) ")]
		[HideInInspector] [SerializeField] public bool shadowSprFlip = false;
		[Tooltip("Deafult shadow sprite x orientation")]
		[HideInInspector] [SerializeField] public bool defaultShadowSprflipx = false;
		[Tooltip("You can edit each shadow sorting-layer in StylizedShadowsCaster2D settings. This is the default layer")]
		[HideInInspector] [SerializeField] public string ShadowsLayerName = "Shadows";

		#endregion

		#region Callbacks

		/// <summary>
		/// sets the callbacks for realtime editor editing
		/// </summary>
		public void SetCallbacks()
		{

			directionalLightAngle.onValueChanged = OnShadowSettingsChanged;
			_shadowColor.onValueChanged = OnShadowSettingsChanged;
			_shadowReflectiveness.onValueChanged = OnShadowSettingsChanged;
			_shadowAlpha.onValueChanged = OnShadowSettingsChanged;
			_shadowAngle.onValueChanged = OnShadowSettingsChanged;
			_shadowLength.onValueChanged = OnShadowSettingsChanged;
			_shadowNarrowing.onValueChanged = OnShadowSettingsChanged;
			_shadowFalloff.onValueChanged = OnShadowSettingsChanged;
            enableBlur.onValueChanged = OnShadowSettingsChanged;
            blurStrength.onValueChanged = OnShadowSettingsChanged;
            blurSampleSize.onValueChanged = OnShadowSettingsChanged;
            blurDirection.onValueChanged = OnShadowSettingsChanged;
			_useSpritePivotForShadowPivot.onValueChanged = OnPivotSettingsChanged;
			_onlyRenderIn2DLight.onValueChanged = OnShadowSettingsChanged;
			_useClosestPointLightForDirection.onValueChanged = OnShadowSettingsChanged;
            _2dLightsShadowStrength.onValueChanged = OnShadowSettingsChanged;
            _minimumAlphaOutOfLight.onValueChanged = OnShadowSettingsChanged;

        }

		public void OnShadowSettingsChanged()
		{
			SetPivotCorrectionsDictionaries();

			shadowsMaterial.SetColor("_shadowBaseColor", _shadowColor.value);
			shadowsMaterial.SetFloat("_shadowBaseAlpha", _shadowAlpha.value);
			shadowsMaterial.SetFloat("_shadowReflectiveness", _shadowReflectiveness.value);
			shadowsMaterial.SetFloat("_shadowNarrowing", _shadowNarrowing.value);
			shadowsMaterial.SetFloat("_shadowFalloff", _shadowFalloff.value);

            Shader.SetGlobalFloat("_2DLightMLP", _2dLightsShadowStrength.value);
            Shader.SetGlobalFloat("_2DLightMinAlpha", _minimumAlphaOutOfLight.value);
            Shader.SetGlobalInt("_onlyRenderIn2DLight", _onlyRenderIn2DLight.value ? 1 : 0);
			shadowsMaterial.SetInt("_useClosestPointLightForDirection", _useClosestPointLightForDirection.value ? 1  : 0 );

			shadowsMaterial.SetInt("_enableBlur", enableBlur.value ? 1  : 0 );
			shadowsMaterial.SetFloat("_blurStrength", !enableBlur ? 0 : blurStrength.value);
			shadowsMaterial.SetInt("_blurArea", !enableBlur ? 0 : blurSampleSize.value);
			shadowsMaterial.SetVector("_blurDir", !enableBlur ? Vector2.zero : blurDirection.value);


			shadowsMaterial.SetFloat("_directional", isLightDirectional.value == true ? 1 : 0);
			shadowsMaterial.SetVector("_distMinMax", distMinMax.value);

			if (source != null)
				shadowsMaterial.SetVector("_source", source.transform.position);

			extendedUpdateThisFrame = true;
			UpdateShadows(null);

		}

		/// <summary>
		/// same as OnShadowSettingsChanged but for shadow pivot
		/// </summary>
		public void OnPivotSettingsChanged() 
		{
			system.SetPivotCorrectionsDictionaries();

            foreach (StylizedShadowCaster2D c in GameObject.FindObjectsOfType<StylizedShadowCaster2D>(true))
            {

                if (c.overrideCustomPivot.value == false)
                {

					c.customPivot = _useSpritePivotForShadowPivot.value == true ? PivotSourceMode.sprite : PivotSourceMode.auto;
					c.RebuildShadow();
				}
            }
			extendedUpdateThisFrame = true;
			UpdateShadows(Transform.FindObjectsOfType<StylizedShadowCaster2D>().ToDictionary(t => t.transform, t => t.shadowData.shadow));
		}

		#endregion

		#region realtimeUpdates

		string collisionTag = "Shadow";
		public void ColliderEnter(Collider2D collision)
		{
			if (collision.gameObject.tag == collisionTag)
			{
				AddShadow(collision.transform.parent.GetComponent<StylizedShadowCaster2D>().shadowData);
			}
		}

		public void ColliderExit(Collider2D collision)
		{
			if (collision.gameObject.tag == collisionTag)
			{
				shadows.Remove(collision.transform.parent);
			}
		}

		private void Update()
		{
            Shader.SetGlobalMatrix("_camProj", Camera.main.projectionMatrix);
            Shader.SetGlobalMatrix("_camWorldToCam", Camera.main.worldToCameraMatrix);

			if (followPlayer != null)
				transform.position = followPlayer.position;

			if (source != null)
				shadowsMaterial.SetVector("_source", source.transform.position);
			UpdateShadows(null);
		}

		//	shadow clean buffer with precomputed size in order to avoid real-time data allocation
		//	(in order to avoid GC fps drops)
		//  if you are considering removing more than 65536 shadows in one frame, increase the size of the container ( O_ O )
		//  (lower if memory is important)

		StylizedShadowCaster[] casters = new StylizedShadowCaster[65536];

		public void UpdateShadows(Dictionary<Transform, StylizedShadowCaster> dict)
		{

			Profiler.BeginSample("Update Shadows");
			int i = 0;

			GetSpotLights();

            //update shadows
            if (dict == null)
				i = UpdateShadowList(shadows);
			else
			{
				extendedUpdateThisFrame = true;
				i = UpdateShadowList(dict);
			}
			Profiler.BeginSample("Clean Shadows");

			//clean
			for (int k = 0; k < casters.Length && k < i; k++)
				shadows.Remove(casters[k].shadowCaster);

			extendedUpdateThisFrame = false;

			Profiler.EndSample();
			Profiler.EndSample();
		}
		
		/// <summary>
		/// updates an array of shadows and returns the last index of deletion buffer
		/// </summary>
		/// <param name="shadows"></param>
		/// <param name=""></param>
		/// <returns></returns>
		int UpdateShadowList(Dictionary<Transform, StylizedShadowCaster> shadows)
		{
			int i = -1;
			foreach (var shadowPair in shadows)
			{
				var shadow = shadowPair.Value;
				if (shadow.shadow == null)
				{
					i++;
					casters[i] = shadow;
				}
				else
				{

					//update shadow sprite
					if (shadow.shadowSr.sprite != shadow.shadowCasterSr.sprite)
					{
						if (StylizedShadowCaster2D.IsFromSpriteSheet(shadow.shadowSr.sprite))
                        {
                            shadow.shadowSr.sprite = shadow.shadowCasterSr.sprite;
							if (shadow.casterComponent == null) shadow.casterComponent = shadow.shadowCaster.GetComponent<StylizedShadowCaster2D>();
                            shadow.casterComponent.SetSpriteSheetData();
						}
						else 
						{
                            shadow.shadowSr.sprite = shadow.shadowCasterSr.sprite;
                        }
					}

					SetShadowXOrientation(shadow);

                    if (!isLightDirectional.value)
					{
						if (_useClosestPointLightForDirection.value)
						{
							float angleToLight = AngleFromLightToShadow(shadow, out Vector3 lightpos);
							if(angleToLight!=LIGHT2DNOTFOUND) shadow.shadowPivot.rotation = Quaternion.RotateTowards(shadow.shadowPivot.rotation, Quaternion.Euler(_shadowAngle.value, shadow.shadowPivot.transform.rotation.eulerAngles.y,Quaternion.AngleAxis( angleToLight, Vector3.forward).eulerAngles.z), 10000 * Time.deltaTime);
                            if (angleToLight != LIGHT2DNOTFOUND) shadow.shadow.localScale = new Vector3(1, ShadowLengthWithLights2D(shadow.shadowPivot.position, lightpos) * _shadowLength.value,1);
                        }
						else
						{
							shadow.shadowPivot.rotation = Quaternion.RotateTowards(shadow.shadowPivot.rotation, Quaternion.Euler(_shadowAngle.value, shadow.shadowPivot.transform.rotation.eulerAngles.y, Quaternion.AngleAxis(AngleToLightSource(shadow.shadowPivot.position), Vector3.forward).eulerAngles.z), 10000 * Time.deltaTime);
							shadow.shadow.localScale = new Vector3(1, SourceShadowLength(shadow.shadowPivot.position) * _shadowLength.value,1);
                        }

					}

                    //gate to more fps consuming operations
                    if (!extendedUpdateThisFrame)
						continue;

					//update shadow rotation
					shadow.shadowPivot.rotation = Quaternion.AngleAxis(AngleToLightSource(shadow.shadowPivot.position), Vector3.forward);

					if (Application.isPlaying) continue;

                    //update shadow angle
                    if (shadow.shadowPivot.transform.rotation.eulerAngles.x != _shadowAngle.value)
					shadow.shadowPivot.transform.rotation = Quaternion.Euler(_shadowAngle.value, shadow.shadowPivot.transform.rotation.eulerAngles.y, shadow.shadowPivot.transform.rotation.eulerAngles.z);
					//update shadow 
					if (shadow.shadow.localScale != new Vector3(1, _shadowLength.value, 1))
                    {

						shadow.shadow.localScale = new Vector3(1, _shadowLength.value, 1);
						
						
						SetShadowPivotPos(shadow);
						SetShadowPos(shadow);

                    }
				}
			}
			return i;
		}

		#endregion

		#region AddingShadowCasters

		/// <summary>
		/// adds or overwrites cached shadow in shadows dictionary and updates the shadow settings
		/// </summary>
		/// <param name="data"></param>
		public void AddShadow(ShadowData data)
		{
			if (shadows.ContainsKey(data.shadow.shadowCaster))
				shadows[data.shadow.shadowCaster] = data.shadow;
			else if(BoundsOptimizationAllows(data.shadow.shadowSr.bounds) )
			{
				shadows.Add(data.shadow.shadowCaster, data.shadow);
			}

            if (StylizedShadowCaster2D.IsFromSpriteSheet(data.shadow.shadowSr.sprite)) data.shadow.shadowCaster.GetComponent<StylizedShadowCaster2D>().SetSpriteSheetData();

            UpdateShadowPositions(data.shadow);
		}

		public bool BoundsOptimizationAllows(Bounds shadowSpriteBounds) 
		{
			if ((enterCollider == null || exitCollider == null)) CreateEnterAndExitColliders();
			if ((enterCollider == null || exitCollider == null)) return true;
            if (enterCollider.bounds.Intersects2D(shadowSpriteBounds)) return true;
			
			return false;
		}

		/// <summary>
		/// creates two box colliders used for shadow update culling
		/// </summary>
		public void CreateEnterAndExitColliders()
		{
			if (followPlayer == null)
			{
				Debug.LogError("Can't create Box Colliders For Shadow-Culling : Camera Transform is not set in Lighting System Inspector");
				return;
			}

			float camSizeX = 2*Camera.main.orthographicSize * Camera.main.aspect;
			float camSizeY = 2*Camera.main.orthographicSize;

			float marginEnter = 2 * Camera.main.orthographicSize;
			float marginExit = 2 * Camera.main.orthographicSize+2f;

            GameObject enterColliderGO = new GameObject("RenderDistanceColliderEnter");
			GameObject exitColliderGO = new GameObject("RenderDistanceColliderExit");

            enterColliderGO.transform.parent = transform;
            exitColliderGO.transform.parent = transform;

            enterColliderGO.transform.position = followPlayer.transform.position;
            exitColliderGO.transform.position = followPlayer.transform.position;

            BoxCollider2D enterCollider = enterColliderGO.AddComponent<BoxCollider2D>();
            BoxCollider2D exitCollider = exitColliderGO.AddComponent<BoxCollider2D>();

			enterColliderGO.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            exitColliderGO.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;


            enterCollider.isTrigger = true;
            exitCollider.isTrigger = true;

            enterCollider.size = new Vector2(camSizeX + marginEnter , camSizeY + marginEnter);
			exitCollider.size = new Vector2(camSizeX + marginExit , camSizeY + marginExit);

            this.enterCollider = enterCollider;
            this.exitCollider = exitCollider;

			enterColliderGO.AddComponent<Collider2DCallback>().OnTrigger2DEnter = new UnityEngine.Events.UnityEvent<Collider2D>();
			exitColliderGO.AddComponent<Collider2DCallback>().OnTrigger2DExit = new UnityEngine.Events.UnityEvent<Collider2D>();

			Debug.Log("Box Colliders used for shadow-culling were created under the lighting system");
			SetCollidersCallbacks();

            return;
        }

		private void SetCollidersCallbacks() 
		{
			if (enterCollider == null || exitCollider == null) CreateEnterAndExitColliders();
            Collider2DCallback enter = enterCollider.GetComponent<Collider2DCallback>();
			Collider2DCallback exit = exitCollider.GetComponent<Collider2DCallback>();


            if (enter.OnTrigger2DEnter == null) enter.OnTrigger2DEnter = new UnityEngine.Events.UnityEvent<Collider2D>();
			if (exit.OnTrigger2DExit == null) exit.OnTrigger2DExit = new UnityEngine.Events.UnityEvent<Collider2D>();
			enter.OnTrigger2DEnter.AddListener(ColliderEnter);
			exit.OnTrigger2DExit.AddListener(ColliderExit);
        }

        private void UpdateShadowPositions(StylizedShadowCaster shadowCaster)
		{
			shadowCaster.shadowSr.sortingLayerName = !shadowCaster.customShadowLayer ? ShadowsLayerName  : shadowCaster.shadowLayer;
			Transform shadow = shadowCaster.shadow;
			SetShadowPosition(shadowCaster, shadow, shadowCaster.shadowPivot, shadowCaster.shadowCaster, shadowCaster.shadowCasterSr.sprite);
		}

		private void SetShadowXOrientation(StylizedShadowCaster shadow)
        {
			bool flipFlag;
			if (shadowSprFlip)
			{
				if (Mathf.Abs(shadow.shadowPivot.eulerAngles.z % 360) < 90 || Mathf.Abs(shadow.shadowPivot.eulerAngles.z % 360) > 270) flipFlag = (shadow.flipX ? !defaultShadowSprflipx : defaultShadowSprflipx);
				else flipFlag = (shadow.flipX ? !defaultShadowSprflipx : defaultShadowSprflipx);
			}
			else  flipFlag = (shadow.flipX ? !defaultShadowSprflipx : defaultShadowSprflipx);
			shadow.shadowSr.flipX = shadow.shadowCasterSr.flipX ? flipFlag : !flipFlag;

        }

		private void SetShadowPivotPos(StylizedShadowCaster shadowCaster)
        {
			//pivot pos
			switch (shadowCaster.pivotMode)
			{
				case PivotSourceMode.auto:
					shadowCaster.shadowPivot.position = shadowCaster.pivotOffset + (Vector2)shadowCaster.shadowCaster.position - (GetSpritePivot(shadowCaster.shadowCasterSr) ) + GetSpritePivotCorrection(shadowCaster.shadowCasterSr.sprite);
					break;
				case PivotSourceMode.sprite:
					shadowCaster.shadowPivot.position = shadowCaster.pivotOffset + (Vector2)shadowCaster.shadowCaster.position;
					break;
				case PivotSourceMode.custom:
					shadowCaster.shadowPivot.position = shadowCaster.pivotOffset + (Vector2)shadowCaster.pivotObject.position;
					break;
			}
		}
		
		private void SetShadowPos(StylizedShadowCaster shadowCaster)
		{
			//pivot pos
			switch (shadowCaster.pivotMode)
			{
				case PivotSourceMode.auto:
					shadowCaster.shadow.localPosition = (GetSpritePivot(shadowCaster.shadowCasterSr) * new Vector2(1, _shadowLength.value) / shadowCaster.shadowCaster.localScale) - (shadowCaster.pivotOffset / shadowCaster.shadowCaster.localScale) + GetSpritePivotCorrection(shadowCaster.shadowCasterSr.sprite);

                    break;
				case PivotSourceMode.sprite:
					shadowCaster.shadow.localPosition =  Vector2.zero - (shadowCaster.pivotOffset / shadowCaster.shadowCaster.localScale);
					break;
				case PivotSourceMode.custom:
					shadowCaster.shadow.localPosition =  (Vector2)shadowCaster.shadowCaster.position - (Vector2)shadowCaster.pivotObject.position - (shadowCaster.pivotOffset / shadowCaster.shadowCaster.localScale);
					break;
			}
		}

		private void SetShadowPosition(StylizedShadowCaster shadowCaster, Transform shadow, Transform pivotT, Transform originalObject, Sprite t)
		{
			Sprite sprite = originalObject.GetComponent<SpriteRenderer>().sprite;
			SetShadowPivotPos(shadowCaster);
			pivotT.rotation = Quaternion.Euler(_shadowAngle.value, 0, 0);
			SetShadowPos(shadowCaster);
			shadow.localScale = new Vector3(1, _shadowLength.value, 1);
            pivotT.rotation = Quaternion.AngleAxis(AngleToLightSource(pivotT.position), Vector3.forward);

			//update shadow angle
			if (pivotT.transform.rotation.eulerAngles.x != _shadowAngle.value)
				pivotT.transform.rotation = Quaternion.Euler(_shadowAngle.value, pivotT.transform.rotation.eulerAngles.y, pivotT.transform.rotation.eulerAngles.z);
        }

        Vector2 GetSpritePivotCorrection(Sprite t) 
		{
			if (spritePivotCorrections.ContainsKey(t.texture) && !spritePivotCorrectionsSpriteSheet.ContainsKey(t)) return spritePivotCorrections[t.texture]; 
			if (spritePivotCorrectionsSpriteSheet.ContainsKey(t)) return spritePivotCorrectionsSpriteSheet[t];
			return Vector2.zero;
		}

		private float AngleToLightSource(Vector3 point)
		{
			if (isLightDirectional.value || (!isLightDirectional.value && source == null))
				return directionalLightAngle.value;

			var direction = (source.position - point).normalized;
			var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90;
			return (angle + 360) % 360;
		}

		private float DistanceToLightSourceClamped(Vector3 point)
		{
			return Mathf.Clamp(Vector2.Distance(point, source.position), distMinMax.value.x, distMinMax.value.y);
		}

		private float SourceShadowLength(Vector3 point)
		{
			float distance = DistanceToLightSourceClamped(point);
			return Mathf.SmoothStep(shadowLengthMinMax.value.x, shadowLengthMinMax.value.y, distance / distMinMax.value.y);
		}


		#endregion

        #region URP 2D Lights

        private float DistanceToLights2D(Vector3 point, Vector3 light)
        {
            return Mathf.Clamp(Vector2.Distance(point, light), distMinMax.value.x, distMinMax.value.y);
        }

        private float ShadowLengthWithLights2D(Vector3 point, Vector3 light)
        {
            float distance = DistanceToLights2D(point, light);
            return Mathf.SmoothStep(shadowLengthMinMax.value.x, shadowLengthMinMax.value.y, distance / distMinMax.value.y);
        }

        Light2D[] lightsArr;
        Transform[] tranformsArr;

		/// <summary>
		/// Finds the angle to three closest light sources and averages them with weight by inverse distance squared
		/// </summary>
		/// <param name="shadow"></param>
		/// <param name="lightpos"></param>
		/// <returns></returns>
        public float AngleFromLightToShadow(StylizedShadowCaster shadow, out Vector3 lightpos) 
		{
			Vector3 pos = shadow.shadowCaster.position;
			lightpos = pos;

			float angleR = LIGHT2DNOTFOUND;
			float[] angles = new float[3];
            float[] inverseDistances = new float[3];

			int anglesIdx = -1;
			float distSum = 0;
			float smallestDist = 10000;

            for(int i = 0; i < lightsArr.Length && anglesIdx < 2; i++)
			{
				float dist = Vector2.Distance(tranformsArr[i].position, pos);
                if (dist > lightsArr[i].pointLightOuterRadius * 2) continue; // out of radius

				var direction = (tranformsArr[i].position - pos).normalized;
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90; //	get angle between light source and object

				float angleMax = tranformsArr[i].rotation.eulerAngles.z + lightsArr[i].pointLightOuterAngle ;
				float angleMin = tranformsArr[i].rotation.eulerAngles.z - lightsArr[i].pointLightOuterAngle ; // get angles of light source cone

				angle = (angle + 360) % 360;	// 0-360 angle space

                if (angle > angleMax || angle < angleMin) continue; // skip if object is out of light source cone

				if (dist < smallestDist) { smallestDist = dist; lightpos = tranformsArr[i].position; }

                float invDist =  (1 / dist) * (1 / dist);

                angles[++anglesIdx] = angle;  //	add angle to calculations in -180 to 180 angle space
				inverseDistances[anglesIdx] = invDist;
                distSum += invDist;
            }

			if (anglesIdx > -1)
			{
				angleR = 0;    // ready for avg if light sources were found

				for (int i = 0; i < anglesIdx + 1; i++) 
				{ 
					angleR += angles[i] * inverseDistances[i]; 
				}
				angleR /= distSum;

                return (angleR + 360) % 360;
            }

			return LIGHT2DNOTFOUND;
        }

        public static Vector2 rotate(Vector2 v, float delta)
        {
            return new Vector2(
                v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
                v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
            );
        }


        public void GetSpotLights() 
		{
			lightsArr = FindObjectsOfType<Light2D>().Where(l => l.lightType == Light2D.LightType.Point).ToArray();
			tranformsArr = new Transform[lightsArr.Length];
			for (int i = 0; i < lightsArr.Length; i++) tranformsArr[i] = lightsArr[i].transform;
		}


        #endregion

        #region pivot

        public Vector2 GetSpritePivotSpriteSheet(SpriteRenderer orgR ) 
		{

			Sprite org = orgR.sprite;
			//sprite sheets
			if (!spriteNewPivotsSpriteSheets.ContainsKey(org))
			{
				//delete spritesheet texture from pivots if it was placed by accident, as it will overshadow our pivots.
				if (spriteNewPivots.ContainsKey(org.texture))
					spriteNewPivots.Remove(org.texture);

				Vector2 pos = new Vector2(0, 0.5f);
				var col = org.texture.GetPixels( Mathf.FloorToInt(org.rect.xMin), Mathf.FloorToInt(org.rect.yMin), Mathf.FloorToInt(org.rect.xMax - org.rect.xMin), Mathf.FloorToInt(org.rect.yMax - org.rect.yMin) );

				int width = (int)org.rect.width;
				float pivotDetectionAlphaTreshold = this.pivotDetectionAlphaTreshold / 255f;
                //get the pivot by iterating sprite texture and getting the average of the last row of pixels
                for (int i = 0; i < col.Length; i++)
					if (col[i].a  > pivotDetectionAlphaTreshold)
					{
						//get whole row average
						int sum = 0;
						int m = 0;
						for (int k = i; k < (width - (i % width)) + i; k++)
							if (col[k].a > pivotDetectionAlphaTreshold)
							{
								m += 1;
								sum += k;
							}
						int medianIdx = sum / m;

						pos = new Vector2(medianIdx % width, medianIdx / width);
						break;
					}

				pos = new Vector2(Mathf.Lerp(0, 1, pos.x / (float)org.rect.width), Mathf.Lerp(0, 1, pos.y / (float)org.rect.height));

				Vector2 pivot = new Vector2(org.pivot.x / org.rect.width, org.pivot.y / org.rect.height);
				Vector2 offsetVec = pivot - pos;

				//multiply the pivot displacement vector by world sprite size
				Vector3 spriteNewPivotOffset = new Vector3(offsetVec.x, offsetVec.y);
				spriteNewPivotsSpriteSheets[org] = spriteNewPivotOffset;
			}



			return spriteNewPivotsSpriteSheets[org] * new Vector2(orgR.bounds.size.x, orgR.bounds.size.y) ;
		}

		/// <summary>
		/// automatically calculates a new pivot on the bottom center of the sprite, so you don't need to do this manually
		/// </summary>
		/// <param name="org"></param>
		/// <returns></returns>
		public Vector2 GetSpritePivot(SpriteRenderer orgR)
		{
			Sprite org = orgR.sprite;

			if (IsSpriteFromSpriteSheet(org))
				return GetSpritePivotSpriteSheet(orgR);

			if (!spriteNewPivots.ContainsKey(org.texture))
			{
				Vector2 pos = new Vector2(0, 0.5f);
				Color32[] colors = org.texture.GetPixels32();
				int width = org.texture.width;

				//get the pivot by iterating sprite texture and getting the average of the last row of pixels
				for (int i = 0; i < colors.Length; i++)
					if (colors[i].a > pivotDetectionAlphaTreshold)
					{
						//get whole row average
						int sum = 0;
						int m = 0;
						for (int k = i; k < (width - (i % width)) + i; k++)
							if (colors[k].a > pivotDetectionAlphaTreshold)
							{
								m += 1;
								sum += k;
							}
						int medianIdx = sum / m;

						pos = new Vector2(medianIdx % width, medianIdx / width);
						break;
					}

				pos = new Vector2(Mathf.Lerp(0, 1, pos.x / (float)org.rect.width), Mathf.Lerp(0, 1, pos.y / (float)org.rect.height));

				Vector2 pivot = new Vector2(org.pivot.x / org.texture.width, org.pivot.y / org.texture.height);
				Vector2 offsetVec = pivot - pos;

				//multiply the pivot displacement vector by world sprite size
				Vector3 spriteNewPivotOffset = new Vector3(offsetVec.x,offsetVec.y);
				spriteNewPivots[org.texture] = spriteNewPivotOffset;
			}

			return spriteNewPivots[org.texture] * new Vector2(org.bounds.size.x, org.bounds.size.y) ;
		}

        #endregion

        #region utilityFunctions

        private bool IsSpriteFromSpriteSheet(Sprite s)
        {
            if (s.rect.width >= s.texture.width && s.rect.height >= s.texture.height)
                return false;
            return true;

        }

		private void SetWaterTexture()
		{
			RenderTexture tex = (RenderTexture)Resources.Load("Render Textures/WaterRenderText");
			if (tex == null) return;
			if(tex.width != Screen.width || tex.height != Screen.height)
			{
				tex.Release();
				tex.width = Screen.width;
				tex.height = Screen.height;
				tex.Create();
            }
		}

        #endregion
    }

}
