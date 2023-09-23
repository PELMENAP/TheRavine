using Modern2D;
using Unity.Mathematics;
using UnityEngine;


namespace Water2D
{


    [ExecuteInEditMode]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CloudShadowsSystem : MonoBehaviour
    {

        #region singleton

        private void Awake()
        {
            Singleton();
        }

        void Singleton()
        {
            if (system != null && system != this) Destroy(this);
            else if (system == null) system = (this);
        }

        #endregion

        #region variables

        public static CloudShadowsSystem system;

        [SerializeField][HideInInspector] public Cryo<CloudType> cloudType = new Cryo<CloudType>(CloudType.worleyNoise);
        [SerializeField][HideInInspector] public Cryo<bool> overrideMainCamera = new Cryo<bool>(false);
        [SerializeField][HideInInspector] public Cryo<Camera> overrideCam;

        [SerializeField][HideInInspector] public Cryo<Vector2> tiling = new Cryo<Vector2>(new Vector2(1.0f, 1.5f));
        [SerializeField][HideInInspector] public Cryo<float2x2> fbsMatrix = new Cryo<float2x2>(new float2x2(1.6f, 1.2f, -1.2f, 1.6f));
        [SerializeField][HideInInspector] public Cryo<float> scale = new Cryo<float>(0.6f);
        [SerializeField][HideInInspector] public Cryo<float> alpha = new Cryo<float>(0.9f);
        [SerializeField][HideInInspector] public Cryo<float> maxAlpha = new Cryo<float>(0.75f);

        [SerializeField][HideInInspector] public Cryo<float> scrollSpeed = new Cryo<float>(0.1f);

        [SerializeField][HideInInspector] public Cryo<float> speed1 = new Cryo<float>(10f);
        [SerializeField][HideInInspector] public Cryo<float> speed2 = new Cryo<float>(6f);

        [SerializeField][HideInInspector] public Cryo<Vector2> cloudsDirection1 = new Cryo<Vector2>(new Vector2(1.0f, 0f));
        [SerializeField][HideInInspector] public Cryo<Vector2> cloudsDirection2 = new Cryo<Vector2>(new Vector2(1.0f, 0.2f));

        [SerializeField][HideInInspector] public Cryo<Color> cloudsColor = new Cryo<Color>(Color.black);
        [SerializeField][HideInInspector] public Cryo<bool> smoothStep = new Cryo<bool>(false);

        [SerializeField][HideInInspector] public Cryo<Vector3> sunDirection = new Cryo<Vector3>(new Vector3(1.0f, -1.0f, 1.0f));


        [SerializeField][HideInInspector] Camera _cam;
        Camera cam
        {
            get
            {
                if (_cam == null)
                    if (overrideMainCamera.value && overrideCam.value != null) _cam = overrideCam.value;
                    else _cam = Camera.main;

                return _cam;
            }
            set { _cam = value; }
        }

        [SerializeField][HideInInspector] SpriteRenderer _sr;
        SpriteRenderer sr
        {
            get { if (_sr == null) _sr = GetComponent<SpriteRenderer>(); return _sr; }
            set { _sr = value; }
        }

        [SerializeField][HideInInspector] Material _mat;
        Material mat
        {
            get
            {
                if (_mat == null)
                {
                    _mat = new Material(Shader.Find(shaderPath));
                    _mat.name = "cloudShadowsMat";
                    sr.material = _mat;
                }
                return _mat;
            }
            set { _mat = value; }
        }

        private const string texPath = "sprites/all/Textures/1024x1024";
        private const string shaderPath = "Custom/cloudShadows";

        #endregion

        void SetCallbacks()
        {
            cloudType.onValueChanged = SetShaderVariables;
            overrideMainCamera.onValueChanged = SetShaderVariables;
            overrideCam.onValueChanged = SetShaderVariables;
            tiling.onValueChanged = SetShaderVariables;
            scale.onValueChanged = SetShaderVariables;
            alpha.onValueChanged = SetShaderVariables;
            maxAlpha.onValueChanged = SetShaderVariables;
            speed1.onValueChanged = SetShaderVariables;
            speed2.onValueChanged = SetShaderVariables;
            cloudsDirection1.onValueChanged = SetShaderVariables;
            cloudsDirection2.onValueChanged = SetShaderVariables;
            cloudsColor.onValueChanged = SetShaderVariables;
            smoothStep.onValueChanged = SetShaderVariables;
            scrollSpeed.onValueChanged = SetShaderVariables;
            sunDirection.onValueChanged = SetShaderVariables;
        }

        private void OnEnable() => Setup();
        void Start() => Setup();

        void Setup()
        {
            SetCallbacks();
            FollowCamera();
            ResizeSpriteToScreen(sr, cam, 1, 1);
            SetShaderVariables();
        }

        void Update()
        {
            FollowCamera();
            ResizeSpriteToScreen(sr, cam, 1, 1);
            mat.SetVector("_pos", transform.position);
        }

        void FollowCamera()
        {
            transform.position = cam.transform.position + new Vector3(0, 0, 10);
            transform.rotation = cam.transform.rotation;
        }

        public void SetShaderVariables()
        {
            if (sr.sharedMaterial != mat) sr.sharedMaterial = mat;

            if (overrideMainCamera.value && overrideCam != null) cam = overrideCam;

            mat.SetInt("_cloudMode", (int)cloudType.value);

            mat.SetVector("_tiling", tiling.value);
            mat.SetFloat("_scale", scale.value);
            mat.SetFloat("_alpha", alpha.value);
            mat.SetFloat("_cloudAlphaMax", maxAlpha.value);

            mat.SetFloat("_scrollSpeed", scrollSpeed.value);

            mat.SetFloat("_speed1", speed1.value);
            mat.SetFloat("_speed2", speed2.value);

            mat.SetVector("_cloudsDir1", cloudsDirection1.value);
            mat.SetVector("_cloudsDir2", cloudsDirection2.value);

            mat.SetVector("_cloudColor", cloudsColor.value);
            mat.SetInt("_step", smoothStep.value ? 1 : 0);

            mat.SetVector("_sunDirection", sunDirection.value);
            mat.SetVector("_ma1", FtoV(fbsMatrix.value.c0));
            mat.SetVector("_ma2", FtoV(fbsMatrix.value.c1));
        }

        Vector2 FtoV(float2 f)
        { return new Vector2(f.x, f.y); }

        void ResizeSpriteToScreen(SpriteRenderer theSprite, Camera theCamera, int fitToScreenWidth, int fitToScreenHeight)
        {
            SpriteRenderer sr = theSprite;

            theSprite.transform.localScale = new Vector3(1, 1, 1);
            Texture2D t = (Texture2D)Resources.Load(texPath);
            if (sr.sprite == null) sr.sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));

            float width = sr.sprite.bounds.size.x;
            float height = sr.sprite.bounds.size.y;

            float worldScreenHeight = (float)(theCamera.orthographicSize * 2.0);
            float worldScreenWidth = (float)(worldScreenHeight / Screen.height * Screen.width);

            if (worldScreenHeight < 0.01f || worldScreenHeight < 0.01f) return;

            if (fitToScreenWidth != 0)
            {
                Vector2 sizeX = new Vector2(worldScreenWidth / width / fitToScreenWidth, theSprite.transform.localScale.y);
                theSprite.transform.localScale = sizeX;
            }

            if (fitToScreenHeight != 0)
            {
                Vector2 sizeY = new Vector2(theSprite.transform.localScale.x, worldScreenHeight / height / fitToScreenHeight);
                theSprite.transform.localScale = sizeY;
            }
        }
    }

    public enum CloudType
    {
        gradientNoise = 0,
        brownianNoise = 1,
        worleyNoise = 2,
        brownianOnGradientNoise = 3
    }

}