using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>저스트가드 동안만 배경 마스크와 약한 파장을 그리는 Renderer2D 연출입니다.</summary>
public sealed class ParryBackgroundRippleRendererFeature : ScriptableRendererFeature
{
    [SerializeField, AssetsOnly, LabelText("배경 파장 설정")] private ParryBackgroundRippleProfile _profile = null;
    [SerializeField, AssetsOnly, LabelText("배경 구분 셰이더")] private Shader _maskShader = null;
    [SerializeField, AssetsOnly, LabelText("파장 셰이더")] private Shader _rippleShader = null;
    [SerializeField, LabelText("배경 정렬 레이어")] private string _backgroundSortingLayer = "Background";

    private RipplePass _pass;
    private Material _backgroundMaterial, _occluderMaterial, _rippleMaterial;
    private short _backgroundValue;
    private bool _hasBackgroundLayer, _warned;
    private static readonly int MaskValueId = Shader.PropertyToID("_MaskValue");

    public override void Create()
    {
        ParryBackgroundRipple.Unregister(this);
        _hasBackgroundLayer = false;
        // Sorting layer IDs are not GameObject layer masks. Resolve the configured layer once.
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name != _backgroundSortingLayer || layer.value < short.MinValue || layer.value >= short.MaxValue) continue;
            _backgroundValue = (short)layer.value;
            _hasBackgroundLayer = true;
            break;
        }
        ReleaseMaterials();
        _pass = new RipplePass();
        _warned = false;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!_hasBackgroundLayer || _profile == null || !_profile.HasValidSettings)
        {
            ParryBackgroundRipple.Unregister(this);
            return;
        }
        Camera camera = renderingData.cameraData.camera;
        if (renderingData.cameraData.cameraType != CameraType.Game
            || renderingData.cameraData.renderType != CameraRenderType.Base) return;
        ParryBackgroundRipple.Register(camera, _profile, this);
        if (!ParryBackgroundRipple.TryGetFrame(camera, out ParryBackgroundRipple.Frame frame)) return;
        if (_maskShader == null || _rippleShader == null || !_maskShader.isSupported || !_rippleShader.isSupported)
        {
            if (!_warned) { Debug.LogWarning("[ParryBackgroundRipple] 셰이더를 사용할 수 없어 배경 파장만 생략합니다."); _warned = true; }
            return;
        }
        // No materials, camera copies or render passes are needed while the effect is idle.
        if (_backgroundMaterial == null)
        {
            _backgroundMaterial = CoreUtils.CreateEngineMaterial(_maskShader);
            _occluderMaterial = CoreUtils.CreateEngineMaterial(_maskShader);
            _rippleMaterial = CoreUtils.CreateEngineMaterial(_rippleShader);
            _backgroundMaterial.SetFloat(MaskValueId, 1f);
            _occluderMaterial.SetFloat(MaskValueId, 0f);
        }
        if (_pass == null) _pass = new RipplePass();
        _pass.Setup(_backgroundMaterial, _occluderMaterial, _rippleMaterial, _backgroundValue, frame);
        renderer.EnqueuePass(_pass);
    }

    private void ReleaseMaterials()
    {
        CoreUtils.Destroy(_backgroundMaterial); CoreUtils.Destroy(_occluderMaterial); CoreUtils.Destroy(_rippleMaterial);
        _backgroundMaterial = null; _occluderMaterial = null; _rippleMaterial = null;
    }

    protected override void Dispose(bool disposing)
    {
        ParryBackgroundRipple.Unregister(this);
        ReleaseMaterials();
        _pass = null;
    }

    private sealed class RipplePass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("Universal2D"), new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit")
        };
        private static readonly int SourceId = Shader.PropertyToID("_BlitTexture");
        private static readonly int MaskId = Shader.PropertyToID("_BackgroundMask");
        private static readonly int ScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int GeometryId = Shader.PropertyToID("_RippleGeometry");
        private static readonly int AmplitudeId = Shader.PropertyToID("_RippleAmplitude");
        private static readonly int ColorId = Shader.PropertyToID("_RippleColor");
        private static readonly int SizeId = Shader.PropertyToID("_ReferenceSize");
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly ProfilingSampler _maskSampler = new ProfilingSampler("HTH Parry Background Mask");
        private Material _background, _occluder, _ripple;
        private short _backgroundValue;
        private ParryBackgroundRipple.Frame _frame;

        private sealed class MaskData { public RendererListHandle Background, Occluders; }
        private sealed class WarpData
        {
            public TextureHandle Source, Mask;
            public Material Material;
            public MaterialPropertyBlock Properties;
            public Vector4 Geometry;
            public ParryBackgroundRipple.Frame Frame;
        }

        public RipplePass()
        {
            // Protect characters before bloom/other post effects spread their colour into the background.
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Color);
            profilingSampler = new ProfilingSampler("HTH Parry Background Ripple");
        }

        public void Setup(Material background, Material occluder, Material ripple, short backgroundValue,
            ParryBackgroundRipple.Frame frame)
        {
            _background = background; _occluder = occluder; _ripple = ripple;
            _backgroundValue = backgroundValue; _frame = frame;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (_ripple == null || resources.isActiveTargetBackBuffer) return;
            TextureHandle source = resources.activeColorTexture;
            if (!source.IsValid()) return;
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            // Match the intermediate render target's GPU projection, including Pixel Perfect adjustments.
            Matrix4x4 vp = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * cameraData.GetViewMatrix();
            Vector4 clip = vp * new Vector4(_frame.OriginWorld.x, _frame.OriginWorld.y, _frame.OriginWorld.z, 1f);
            if (clip.w <= 0f) return;
            Vector2 center = new Vector2(clip.x, clip.y) / clip.w * .5f + Vector2.one * .5f;
            if (SystemInfo.graphicsUVStartsAtTop) center.y = 1f - center.y;

            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            RendererListHandle background = CreateList(graph, renderingData, cameraData, lightData, _background,
                new SortingLayerRange(_backgroundValue, _backgroundValue));
            RendererListHandle occluders = CreateList(graph, renderingData, cameraData, lightData, _occluder,
                new SortingLayerRange((short)(_backgroundValue + 1), short.MaxValue));
            if (!background.IsValid() || !occluders.IsValid()) return;

            TextureDesc description = graph.GetTextureDesc(source);
            description.name = "HTH Parry Background Mask";
            description.colorFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
            description.depthBufferBits = DepthBits.None;
            description.msaaSamples = MSAASamples.None;
            description.filterMode = FilterMode.Point;
            description.clearBuffer = true;
            description.clearColor = Color.black;
            TextureHandle mask = graph.CreateTexture(description);
            using (var builder = graph.AddRasterRenderPass<MaskData>("HTH Parry Background Mask", out MaskData data, _maskSampler))
            {
                data.Background = background; data.Occluders = occluders;
                builder.UseRendererList(background); builder.UseRendererList(occluders);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                // An empty scene must still clear to protected black, never leave an undefined mask.
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (MaskData pass, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(pass.Background);
                    context.cmd.DrawRendererList(pass.Occluders);
                });
            }

            description = graph.GetTextureDesc(source);
            description.name = "HTH Parry Background Ripple Color";
            description.depthBufferBits = DepthBits.None;
            description.msaaSamples = MSAASamples.None;
            description.clearBuffer = false;
            TextureHandle destination = graph.CreateTexture(description);
            using (var builder = graph.AddRasterRenderPass<WarpData>("HTH Parry Background Ripple", out WarpData data, profilingSampler))
            {
                data.Source = source; data.Mask = mask; data.Material = _ripple; data.Properties = _properties; data.Frame = _frame;
                data.Geometry = new Vector4(center.x, center.y, _frame.RadiusPixels, _frame.WidthPixels);
                builder.UseTexture(source, AccessFlags.Read); builder.UseTexture(mask, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (WarpData pass, RasterGraphContext context) =>
                {
                    MaterialPropertyBlock properties = pass.Properties;
                    properties.Clear();
                    properties.SetTexture(SourceId, pass.Source); properties.SetTexture(MaskId, pass.Mask);
                    properties.SetVector(ScaleBiasId, new Vector4(1, 1, 0, 0));
                    properties.SetVector(GeometryId, pass.Geometry);
                    properties.SetFloat(AmplitudeId, pass.Frame.AmplitudePixels);
                    properties.SetColor(ColorId, pass.Frame.RingColor);
                    properties.SetVector(SizeId, new Vector4(640, 480, 1f / 640f, 1f / 480f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3, 1, properties);
                });
            }
            resources.cameraColor = destination;
        }

        private static RendererListHandle CreateList(RenderGraph graph, UniversalRenderingData renderingData,
            UniversalCameraData cameraData, UniversalLightData lightData, Material material, SortingLayerRange range)
        {
            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(ShaderTags, renderingData, cameraData,
                lightData, SortingCriteria.CommonTransparent);
            drawing.overrideMaterial = material;
            drawing.overrideMaterialPassIndex = 0;
            drawing.perObjectData = PerObjectData.None;
            FilteringSettings filtering = new FilteringSettings(RenderQueueRange.all, cameraData.camera.cullingMask)
                { sortingLayerRange = range };
            return graph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
        }
    }
}
