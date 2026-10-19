using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>실험실 Renderer2D 전용. 활성 게임 카메라만1패스, Overlay HUD는 변형하지 않습니다.</summary>
public sealed class ScreenSplitRendererFeature : ScriptableRendererFeature
{
    [SerializeField, AssetsOnly, LabelText("사선 화면 분할 셰이더")] private Shader _shader = null;
    private SplitPass _pass;
    private Material _material;
    private bool _warned;

    public override void Create() => _pass = new SplitPass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (ScreenSplitEffect.ActiveCount == 0 || renderingData.cameraData.cameraType != CameraType.Game
            || renderingData.cameraData.renderType != CameraRenderType.Base
            || !ScreenSplitEffect.TryGetFrame(camera, out ScreenSplitEffect.Frame frame)) return;
        if (_shader == null || !_shader.isSupported)
        {
            if (!_warned) { Debug.LogWarning("[ScreenSplit] 화면 분할 셰이더를 사용할 수 없어 연출만 생략합니다."); _warned = true; }
            return;
        }
        // No material/texture is allocated by a dormant feature. The material is reused after the first cast.
        if (_material == null) _material = CoreUtils.CreateEngineMaterial(_shader);
        if (_material == null) return;
        if (_pass == null) _pass = new SplitPass();
        _pass.Setup(_material, frame);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material); _material = null; _pass = null;
    }

    private sealed class SplitPass : ScriptableRenderPass
    {
        private Material _material;
        private ScreenSplitEffect.Frame _frame;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private static readonly int TextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int ScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int LineId = Shader.PropertyToID("_SplitLine");
        private static readonly int WarpId = Shader.PropertyToID("_SplitWarp");
        private static readonly int ColorId = Shader.PropertyToID("_SplitColor");
        private static readonly int SizeId = Shader.PropertyToID("_ReferenceSize");

        private sealed class PassData
        {
            public TextureHandle Source;
            public Material Material;
            public MaterialPropertyBlock Properties;
            public ScreenSplitEffect.Frame Frame;
        }

        public SplitPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
            // Renderer2D ignores requiresIntermediateTexture. Color requests its source attachment
            // without a CopyColor pass; the dormant feature never enqueues this request.
            ConfigureInput(ScriptableRenderPassInput.Color);
            profilingSampler = new ProfilingSampler("HTH Screen Split");
        }

        public void Setup(Material material, ScreenSplitEffect.Frame frame) { _material = material; _frame = frame; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (_material == null || resources.isActiveTargetBackBuffer) return;
            TextureHandle source = resources.activeColorTexture;
            if (!source.IsValid()) return;
            TextureDesc description = renderGraph.GetTextureDesc(source);
            description.name = "HTH Screen Split Color";
            description.clearBuffer = false;
            description.depthBufferBits = DepthBits.None;
            description.msaaSamples = MSAASamples.None;
            TextureHandle destination = renderGraph.CreateTexture(description);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("HTH Screen Split", out PassData data, profilingSampler))
            {
                data.Source = source; data.Material = _material; data.Properties = _properties; data.Frame = _frame;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    MaterialPropertyBlock properties = pass.Properties;
                    properties.Clear();
                    properties.SetTexture(TextureId, pass.Source);
                    properties.SetVector(ScaleBiasId, new Vector4(1, 1, 0, 0));
                    properties.SetVector(LineId, pass.Frame.Line);
                    properties.SetVector(WarpId, pass.Frame.Warp);
                    properties.SetColor(ColorId, pass.Frame.Color);
                    properties.SetVector(SizeId, new Vector4(640, 480, 1f / 640f, 1f / 480f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3, 1, properties);
                });
            }
            // Ping-pong the active colour instead of copying it back: only one effect draw is added.
            resources.cameraColor = destination;
        }
    }
}
