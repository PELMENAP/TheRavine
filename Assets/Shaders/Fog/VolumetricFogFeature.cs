using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class VolumetricFogFeature : ScriptableRendererFeature
{
    public enum FogResolution
    {
        Full = 1,
        Half = 2,
        Quarter = 4
    }

    [Serializable]
    public sealed class Settings
    {
        public Material fogMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public FogResolution resolution = FogResolution.Half;
        [Tooltip("Порог плотности, ниже которого пасс вообще не ставится в очередь (0 мс).")]
        public float densityCutoff = 0.0002f;
        public bool renderInSceneView = true;
    }

    private const int PassFullRes = 0;
    private const int PassHalfRes = 1;
    private const int PassUpsample = 2;

    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int VolumetricFogTextureId = Shader.PropertyToID("_VolumetricFogTexture");
    private static readonly int VolumetricFogTexelSizeId = Shader.PropertyToID("_VolumetricFogTexture_TexelSize");

    [SerializeField] private Settings settings = new();

    private VolumetricFogPass _pass;

    public override void Create()
    {
        _pass = new VolumetricFogPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Material mat = settings.fogMaterial;
        if (mat == null) return;

        var camData = renderingData.cameraData;
        if (camData.renderType == CameraRenderType.Overlay) return;

        var camType = camData.cameraType;
        if (camType != CameraType.Game && (!settings.renderInSceneView || camType != CameraType.SceneView))
            return;

        if (mat.HasProperty(DensityId) && mat.GetFloat(DensityId) < settings.densityCutoff)
            return;

        if (mat.HasProperty(ColorId) && mat.GetColor(ColorId).a <= 0.001f)
            return;

        int downsample = (int)settings.resolution;
        if (downsample > 1 && mat.passCount < 3)
            downsample = 1;

        _pass.Setup(mat, settings.renderPassEvent, downsample);
        renderer.EnqueuePass(_pass);
    }

    private sealed class VolumetricFogPass : ScriptableRenderPass
    {
        private Material _material;
        private int _downsample;

        private sealed class RaymarchPassData
        {
            public Material Material;
        }

        private sealed class FullResPassData
        {
            public TextureHandle Source;
            public Material Material;
        }

        private sealed class UpsamplePassData
        {
            public TextureHandle Source;
            public TextureHandle FogTexture;
            public Vector4 FogTexelSize;
            public Material Material;
        }

        public VolumetricFogPass()
        {
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(Material material, RenderPassEvent passEvent, int downsample)
        {
            _material = material;
            renderPassEvent = passEvent;
            _downsample = Mathf.Max(1, downsample);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle depth = resourceData.cameraDepthTexture;
            if (!source.IsValid() || !depth.IsValid()) return;

            TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);
            if (sourceDesc.msaaSamples != MSAASamples.None) return;

            TextureDesc destDesc = sourceDesc;
            destDesc.name = "VolumetricFog_Dest";
            destDesc.clearBuffer = false;
            destDesc.depthBufferBits = DepthBits.None;
            destDesc.msaaSamples = MSAASamples.None;
            destDesc.bindTextureMS = false;

            TextureHandle destination = renderGraph.CreateTexture(destDesc);

            if (_downsample == 1)
            {
                RecordFullRes(renderGraph, resourceData, source, depth, destination);
            }
            else
            {
                RecordDownsampled(renderGraph, resourceData, source, depth, sourceDesc, destination);
            }

            resourceData.cameraColor = destination;
        }

        private void RecordFullRes(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            TextureHandle source,
            TextureHandle depth,
            TextureHandle destination)
        {
            using var builder = renderGraph.AddRasterRenderPass<FullResPassData>("VolumetricFog_FullRes", out var passData);
            passData.Source = source;
            passData.Material = _material;

            builder.UseTexture(source, AccessFlags.Read);
            builder.UseTexture(depth, AccessFlags.Read);
            DeclareShadowDependencies(builder, resourceData);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (FullResPassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, PassFullRes);
            });
        }

        private void RecordDownsampled(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            TextureHandle source,
            TextureHandle depth,
            in TextureDesc sourceDesc,
            TextureHandle destination)
        {
            int fogWidth = Mathf.Max(1, sourceDesc.width / _downsample);
            int fogHeight = Mathf.Max(1, sourceDesc.height / _downsample);

            TextureDesc fogDesc = sourceDesc;
            fogDesc.name = _downsample == 2 ? "VolumetricFog_HalfRes" : "VolumetricFog_QuarterRes";
            fogDesc.width = fogWidth;
            fogDesc.height = fogHeight;
            fogDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            fogDesc.clearBuffer = false;
            fogDesc.depthBufferBits = DepthBits.None;
            fogDesc.msaaSamples = MSAASamples.None;
            fogDesc.bindTextureMS = false;
            fogDesc.filterMode = FilterMode.Bilinear;
            fogDesc.wrapMode = TextureWrapMode.Clamp;

            TextureHandle fogTexture = renderGraph.CreateTexture(fogDesc);

            using (var builder = renderGraph.AddRasterRenderPass<RaymarchPassData>("VolumetricFog_Raymarch", out var passData))
            {
                passData.Material = _material;

                builder.UseTexture(depth, AccessFlags.Read);
                DeclareShadowDependencies(builder, resourceData);
                builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (RaymarchPassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, PassHalfRes);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<UpsamplePassData>("VolumetricFog_Upsample", out var passData))
            {
                passData.Source = source;
                passData.FogTexture = fogTexture;
                passData.FogTexelSize = new Vector4(1f / fogWidth, 1f / fogHeight, fogWidth, fogHeight);
                passData.Material = _material;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.UseTexture(fogTexture, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (UpsamplePassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(VolumetricFogTextureId, data.FogTexture);
                    ctx.cmd.SetGlobalVector(VolumetricFogTexelSizeId, data.FogTexelSize);
                    Blitter.BlitTexture(ctx.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, PassUpsample);
                });
            }
        }

        private static void DeclareShadowDependencies(IRasterRenderGraphBuilder builder, UniversalResourceData resourceData)
        {
            if (resourceData.mainShadowsTexture.IsValid())
                builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);

            if (resourceData.additionalShadowsTexture.IsValid())
                builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);
        }
    }
}