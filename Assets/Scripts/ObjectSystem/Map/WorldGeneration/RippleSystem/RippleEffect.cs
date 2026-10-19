using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RippleEffect : MonoBehaviour
{
    public const int TextureSize = 512;
    private RenderTexture CurrRT, PrevRT, TempRT;
    public Shader RippleShader;
    private Material RippleMat;
    private Material waterMat;
    private void Start()
    {
        CurrRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat
        };
        CurrRT.Create();

        PrevRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat
        };
        PrevRT.Create();

        TempRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat
        };
        TempRT.Create();


        RippleMat = new Material(RippleShader);
    
        waterMat = GetComponent<Renderer>().material;
        waterMat.SetTexture("RippleTex", CurrRT);
    }

    private int frameSkip = 0;

    private void LateUpdate()
    {
        RippleStampSystem.Instance.FlushToRT(CurrRT);
        if (++frameSkip < 2) return;
        frameSkip = 0;

        RippleMat.SetTexture("PrevRT", PrevRT);
        RippleMat.SetTexture("CurrentRT", CurrRT);
        Graphics.Blit(null, TempRT, RippleMat);

        var next = PrevRT;
        PrevRT = CurrRT;
        CurrRT = TempRT;
        TempRT = next;

        waterMat.SetTexture("RippleTex", CurrRT);
    }

    private void OnDestroy()
    {
        CurrRT?.Release();
        PrevRT?.Release();
        TempRT?.Release();
    }
}
