#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ZLinq;

public static class GrassTextureArrayBaker
{
    [MenuItem("Tools/Grass/Bake Texture Array")]
    static void Bake()
    {
        var guids = Selection.assetGUIDs;
        var sprites = guids
            .AsValueEnumerable()
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
            .Where(s => s != null)
            .ToArray();

        if (sprites.Length == 0) return;

        const int Resolution = 256;
        var array = GrassTextureArrayFactory.Build(sprites, Resolution);

        var path = "Assets/Grass/GrassTexArray.asset";
        AssetDatabase.CreateAsset(array, path);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Saved Texture2DArray → {path}, slices: {sprites.Length}");
    }
}

public sealed class GrassTextureArrayFactory
{
    public static Texture2DArray Build(Sprite[] sprites, int resolution = 256)
    {
        var array = new Texture2DArray(resolution, resolution, sprites.Length,
            TextureFormat.RGBA32, mipChain: true, linear: false);

        for (int i = 0; i < sprites.Length; i++)
        {
            var src = sprites[i].texture;
            
            // Спрайт может быть частью атласа — копируем через RenderTexture
            var rt = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            
            var readback = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            readback.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            
            array.SetPixels(readback.GetPixels(), i, miplevel: 0);
            Object.Destroy(readback);
        }

        array.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        return array;
    }
}
#endif