using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class WaterUnderwaterFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class Settings
    {
        public Material effectMaterial;
        public bool enableCaustics = true;
        public bool enableUnderwaterPost = true;
        public float defaultWaterLevel = 0f;
        [Tooltip("Запас под near plane камеры для плавного входа в воду.")]
        public float nearPlaneMargin = 0.3f;
    }
    public static Func<Vector3, float> WaterHeightProvider;

    [SerializeField] private Settings settings = new();

    private static readonly int WaterLevelId = Shader.PropertyToID("_WaterLevel");
    private static readonly int CameraUnderwaterId = Shader.PropertyToID("_CameraUnderwater");

    private CausticsPass _causticsPass;
    private UnderwaterPostPass _underwaterPass;

    public override void Create()
    {
        _causticsPass = new CausticsPass();
        _underwaterPass = new UnderwaterPostPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.effectMaterial == null) return;

        var camType = renderingData.cameraData.cameraType;
        if (camType != CameraType.Game && camType != CameraType.SceneView) return;

        Vector3 camPos = renderingData.cameraData.worldSpaceCameraPos;
        float waterLevel = WaterHeightProvider != null ? WaterHeightProvider(camPos) : settings.defaultWaterLevel;
        bool isUnderwater = camPos.y < waterLevel + settings.nearPlaneMargin;
        float underwaterFlag = isUnderwater ? 1f : 0f;

        if (settings.enableCaustics)
        {
            _causticsPass.Setup(settings.effectMaterial, waterLevel, underwaterFlag);
            renderer.EnqueuePass(_causticsPass);
        }

        if (settings.enableUnderwaterPost && isUnderwater)
        {
            _underwaterPass.Setup(settings.effectMaterial, waterLevel, underwaterFlag);
            renderer.EnqueuePass(_underwaterPass);
        }
    }

    private sealed class CausticsPass : ScriptableRenderPass
    {
        private Material _material;
        private float _waterLevel;
        private float _cameraUnderwater;

        private sealed class PassData
        {
            public Material Material;
            public float WaterLevel;
            public float CameraUnderwater;
        }

        public CausticsPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques + 1;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(Material material, float waterLevel, float cameraUnderwater)
        {
            _material = material;
            _waterLevel = waterLevel;
            _cameraUnderwater = cameraUnderwater;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle color = resourceData.activeColorTexture;
            TextureHandle depth = resourceData.cameraDepthTexture;
            if (!color.IsValid() || !depth.IsValid()) return;

            using var builder = renderGraph.AddRasterRenderPass<PassData>("WaterCausticsPass", out var passData);
            passData.Material = _material;
            passData.WaterLevel = _waterLevel;
            passData.CameraUnderwater = _cameraUnderwater;

            builder.UseTexture(depth, AccessFlags.Read);
            if (resourceData.mainShadowsTexture.IsValid())
                builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);

            builder.SetRenderAttachment(color, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.SetGlobalFloat(WaterLevelId, data.WaterLevel);
                ctx.cmd.SetGlobalFloat(CameraUnderwaterId, data.CameraUnderwater);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3);
            });
        }
    }

    private sealed class UnderwaterPostPass : ScriptableRenderPass
    {
        private Material _material;
        private float _waterLevel;
        private float _cameraUnderwater;

        private sealed class PassData
        {
            public TextureHandle Source;
            public Material Material;
            public float WaterLevel;
            public float CameraUnderwater;
        }

        public UnderwaterPostPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(Material material, float waterLevel, float cameraUnderwater)
        {
            _material = material;
            _waterLevel = waterLevel;
            _cameraUnderwater = cameraUnderwater;
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

            TextureDesc desc = sourceDesc;
            desc.name = "WaterUnderwaterPost_Dest";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;

            TextureHandle destination = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("WaterUnderwaterPostPass", out var passData))
            {
                passData.Source = source;
                passData.Material = _material;
                passData.WaterLevel = _waterLevel;
                passData.CameraUnderwater = _cameraUnderwater;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalFloat(WaterLevelId, data.WaterLevel);
                    ctx.cmd.SetGlobalFloat(CameraUnderwaterId, data.CameraUnderwater);
                    Blitter.BlitTexture(ctx.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, 1);
                });
            }

            resourceData.cameraColor = destination;
        }
    }
}