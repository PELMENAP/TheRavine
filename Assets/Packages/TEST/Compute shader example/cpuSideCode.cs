using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class CpuSideCode : MonoBehaviour
{
    public InputActionReference mouseScrollAction;
    public InputActionReference middleMouseAction;
    public InputActionReference mouseMoveAction;

    static public int kiCalc;
    static public ComputeBuffer areaRectBuffer;
    static public ComputeBuffer colorsBuffer;
    static public double[] areaRectArray;
    static public Color[] colorArray;
    static public RenderTexture outputTexture;
    static public GameObject mainCanvas;
    static public UnityEngine.UI.Image outputImage;
    static public ComputeShader _shader;
    static public double depthFactor;
    static public double cx, cy;
    static public bool move;
    static public bool inputChange;

    private Vector2 mouseDelta;

    void Start()
    {
        staticInit();
    }

    static public void staticInit()
    {
        initControls();
        initTexture();
        initCanvas();
        initBuffers();
        initShader();
    }

    static void initControls()
    {
        depthFactor = 1.0;
        cx = 0;
        cy = 0;
        move = false;
        inputChange = true;
    }

    static void initTexture()
    {
        outputTexture = new RenderTexture(1024, 1024, 32);
        outputTexture.enableRandomWrite = true;
        outputTexture.Create();
        outputTexture.filterMode = FilterMode.Point;
    }

    static public void initCanvas()
    {
        mainCanvas = GameObject.Find("canvas");
        mainCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
        mainCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
        mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 1.0f;
        outputImage = GameObject.Find("canvas/image").GetComponent<UnityEngine.UI.Image>();
        outputImage.material.mainTexture = outputTexture;
        outputImage.type = UnityEngine.UI.Image.Type.Simple;
        outputImage.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1080);
    }

    static void initBuffers()
    {
        int i;
        areaRectArray = new double[4] { -2.0f, -2.0f, 2.0f, 2.0f };
        areaRectBuffer = new ComputeBuffer(areaRectArray.Length, sizeof(double));
        areaRectBuffer.SetData(areaRectArray);

        colorArray = new Color[256];
        for (i = 0; i < colorArray.Length; i++)
        {
            colorArray[i] = new Color(0, 0, 0, 1);
            if (i >= 0 && i < 128)
                colorArray[i] += new Color(0, 0, Mathf.PingPong(i * 4, 256) / 256, 1);
            if (i >= 64 && i < 192)
                colorArray[i] += new Color(0, Mathf.PingPong((i - 64) * 4, 256) / 256, 0, 1);
            if (i >= 128 && i < 256)
                colorArray[i] += new Color(Mathf.PingPong(i * 4, 256) / 256, 0, 0, 1);
        }
        colorsBuffer = new ComputeBuffer(colorArray.Length, 4 * 4);
        colorsBuffer.SetData(colorArray);
    }

    static void initShader()
    {
        _shader = Resources.Load<ComputeShader>("csFractal");
        kiCalc = _shader.FindKernel("pixelCalc");
        _shader.SetBuffer(kiCalc, "rect", areaRectBuffer);
        _shader.SetBuffer(kiCalc, "colors", colorsBuffer);
        _shader.SetTexture(kiCalc, "textureOut", outputTexture);
    }

    static void calcFractal()
    {
        _shader.Dispatch(kiCalc, 32, 32, 1);
    }

    void Update()
    {
        HandleInput();
        if (inputChange)
        {
            calcFractal();
            inputChange = false;
        }
    }
	public double borderChange = 2.0f, k = 0.0009765625f, scrooll;
	public Vector2 direction;

    void HandleInput()
    {
		depthFactor -= 0.02 * depthFactor * scrooll;
		inputChange = true;

		cx -= 100 * k * depthFactor * direction.x;
		cy -= 100 * k * depthFactor * direction.y;

        if (inputChange)
        {
            areaRectArray[0] = cx - depthFactor * borderChange;
            areaRectArray[1] = cy - depthFactor * borderChange;
            areaRectArray[2] = cx + depthFactor * borderChange;
            areaRectArray[3] = cy + depthFactor * borderChange;
            areaRectBuffer.SetData(areaRectArray);
        }
    }

    void OnDestroy()
    {
        areaRectBuffer.Release();
        colorsBuffer.Release();
    }
}
