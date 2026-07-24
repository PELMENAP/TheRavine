using UnityEngine;
using UnityEditor;

public class TextureArrayCreator : MonoBehaviour
{
    public Texture2D[] grassTextures;

    [ContextMenu("Create Texture Array")]
    public void CreateArray()
    {
        if (grassTextures == null || grassTextures.Length == 0) return;

        int width = grassTextures[0].width;
        Debug.Log(grassTextures[0].format);
        int height = grassTextures[0].height;

        Texture2DArray textureArray =
            new(
                width,
                height,
                grassTextures.Length,
                TextureFormat.RGBA32,
                false)
            {
                filterMode = FilterMode.Point,
                wrapMode = grassTextures[0].wrapMode
            };

        for (int i = 0; i < grassTextures.Length; i++)
        {
            Color[] colors = grassTextures[i].GetPixels();

            textureArray.SetPixels(colors, i);
        }

        textureArray.Apply();

        textureArray.Apply();
        
        AssetDatabase.CreateAsset(textureArray, "Assets/GrassTextureArray.asset");
        AssetDatabase.SaveAssets();
        
        Debug.Log("Texture Array created at Assets/GrassTextureArray.asset");
    }
}