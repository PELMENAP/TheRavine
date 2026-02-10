using UnityEngine;
using System;

[Serializable]
public struct GrassRegionData
{
    public float minHeight;
    public Color baseColor;
    public Color tipColor;
    public float scaleMultiplier;
    public float densityMultiplier;
    public bool enabled;
}

[CreateAssetMenu(fileName = "GrassRegionConfig", menuName = "World/Grass Region Config")]
public class GrassRegionConfig : ScriptableObject
{
    [SerializeField] private GrassRegionData[] regions = new GrassRegionData[]
    {
        new GrassRegionData 
        { 
            minHeight = -10f, 
            baseColor = new Color(0.2f, 0.5f, 0.1f), 
            tipColor = new Color(0.4f, 0.7f, 0.2f),
            scaleMultiplier = 0.8f,
            densityMultiplier = 1.2f,
            enabled = true
        },
        new GrassRegionData 
        { 
            minHeight = 5f, 
            baseColor = new Color(0.3f, 0.6f, 0.2f), 
            tipColor = new Color(0.5f, 0.8f, 0.3f),
            scaleMultiplier = 1.0f,
            densityMultiplier = 1.0f,
            enabled = true
        },
        new GrassRegionData 
        { 
            minHeight = 15f, 
            baseColor = new Color(0.4f, 0.5f, 0.3f), 
            tipColor = new Color(0.6f, 0.7f, 0.4f),
            scaleMultiplier = 0.6f,
            densityMultiplier = 0.5f,
            enabled = true
        },
        new GrassRegionData 
        { 
            minHeight = 25f, 
            baseColor = Color.white, 
            tipColor = Color.white,
            scaleMultiplier = 0.0f,
            densityMultiplier = 0.0f,
            enabled = false
        }
    };
}