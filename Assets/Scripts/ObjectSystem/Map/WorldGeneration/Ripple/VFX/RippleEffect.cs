using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RippleEffect : MonoBehaviour
{
    public int TextureSize = 512;
    public RenderTexture ObjectsRT;
    private RenderTexture CurrRT, PrevRT, TempRT;
    public Shader RippleShader, AddShader;
    private Material RippleMat, AddMat;
    void Start()
    {
        //Creating render textures and materials
        CurrRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        PrevRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        TempRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        RippleMat = new Material(RippleShader);
        AddMat = new Material(AddShader);

        //Change the texture in the material of this object to the render texture calculated by the ripple shader.
        GetComponent<Renderer>().material.SetTexture("_RippleTex", CurrRT); //The result water material changed on this line
    }

    private int _frameSkip = 0;

    private void LateUpdate()
    {
        RippleStampSystem.Instance.FlushToRT(CurrRT);
        if (++_frameSkip < 2) return;
        _frameSkip = 0;

        // Один бlit — симуляция волны
        RippleMat.SetTexture("_PrevRT", PrevRT);
        RippleMat.SetTexture("_CurrentRT", CurrRT);
        Graphics.Blit(null, TempRT, RippleMat);

        // Правильный своп: CurrRT = новое состояние, PrevRT = старое CurrRT
        (CurrRT, PrevRT) = (TempRT, CurrRT);
        TempRT = PrevRT; // TempRT теперь = бывший PrevRT, используется на следующем шаге

        GetComponent<Renderer>().material.SetTexture("_RippleTex", CurrRT);
    }
}
