using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RippleEffect : MonoBehaviour
{
    public int TextureSize = 1024;
    private RenderTexture CurrRT, PrevRT, TempRT;
    public Shader RippleShader;
    private Material RippleMat;
    private Material _waterMat;
    void Start()
    {
        //Creating render textures and materials
        CurrRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        PrevRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        TempRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        RippleMat = new Material(RippleShader);
    
        _waterMat = GetComponent<Renderer>().material;
        _waterMat.SetTexture("_RippleTex", CurrRT); //The result water material changed on this line
    }

    private int _frameSkip = 0;

    private void LateUpdate()
    {
        RippleStampSystem.Instance.FlushToRT(CurrRT);
        if (++_frameSkip < 2) return;
        _frameSkip = 0;

        RippleMat.SetTexture("_PrevRT", PrevRT);
        RippleMat.SetTexture("_CurrentRT", CurrRT);
        Graphics.Blit(null, TempRT, RippleMat);

        (CurrRT, PrevRT) = (TempRT, CurrRT);
        TempRT = PrevRT;

        _waterMat.SetTexture("_RippleTex", CurrRT);
    }
}
