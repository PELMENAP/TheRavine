using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TextureArrayCreator : MonoBehaviour
{
    public Texture2D[] grassTextures;
    public string assetPath = "Assets/GrassTextureArray.asset";
    public bool mipmaps = true;
    public bool linear;
    public FilterMode filterMode = FilterMode.Bilinear;
    [Range(0, 16)] public int anisoLevel = 4;

#if UNITY_EDITOR
    [ContextMenu("Create Texture Array")]
    public void CreateArray()
    {
        if (grassTextures == null || grassTextures.Length == 0) return;

        int width = grassTextures[0].width;
        int height = grassTextures[0].height;

        for (int i = 0; i < grassTextures.Length; i++)
        {
            Texture2D t = grassTextures[i];
            if (t == null || t.width != width || t.height != height || !t.isReadable)
            {
                Debug.LogError($"[TextureArrayCreator] Texture {i} must be readable and {width}x{height}");
                return;
            }
        }

        var textureArray = new Texture2DArray(width, height, grassTextures.Length, TextureFormat.RGBA32, mipmaps, linear)
        {
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Repeat,
            anisoLevel = anisoLevel
        };

        for (int i = 0; i < grassTextures.Length; i++)
            textureArray.SetPixels32(grassTextures[i].GetPixels32(), i, 0);

        textureArray.Apply(mipmaps, false);

        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(textureArray, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Texture Array created at {assetPath}");
    }
#endif
}
