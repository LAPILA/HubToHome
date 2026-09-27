Shader "Hub To Home/2D/Pixel Foliage UV Lit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _MaskTex("Light Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _SwayPixels("Horizontal Sway (source pixels)", Range(0, 8)) = 2
        _VerticalPixels("Vertical Sway (source pixels)", Range(0, 8)) = 0
        _SwaySpeed("Wind Cycles Per Second", Range(0, 3)) = 0.45
        _WindScale("Wind Spatial Scale", Range(0.05, 4)) = 1.1
        _RootPin("Anchor Strength", Range(0, 1)) = 0
        [Toggle] _PinFromTop("Anchor From Top", Float) = 0
        _EdgeLockPixels("Keep Tile Edges Fixed (pixels)", Range(0, 8)) = 2
        [HideInInspector] [PerRendererData] _FoliageRegion("Sprite Pixel Region", Vector) = (0,0,32,32)
        [HideInInspector] [PerRendererData] _FoliageLayout("Pixel Foliage Layout", Vector) = (32,1,0,0)
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Foliage Lit"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex FoliageVertex
            #pragma fragment FoliageFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "PixelFoliageUV.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color : COLOR;
                float2 foliagePositionWS : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            Varyings FoliageVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonLitVertex(input);
                o.foliagePositionWS = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 FoliageFragment(Varyings input) : SV_Target
            {
                input.uv = PixelFoliageUV(input.uv, input.foliagePositionWS);
                return CommonLitFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Foliage Normals"
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex FoliageNormalsVertex
            #pragma fragment FoliageNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "PixelFoliageUV.hlsl"
            struct Attributes { COMMON_2D_NORMALS_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4 color : COLOR;
                float2 foliagePositionWS : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            Varyings FoliageNormalsVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.foliagePositionWS = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 FoliageNormalsFragment(Varyings input) : SV_Target
            {
                input.uv = PixelFoliageUV(input.uv, input.foliagePositionWS);
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Foliage Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex FoliageForwardVertex
            #pragma fragment FoliageForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "PixelFoliageUV.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 foliagePositionWS : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            Varyings FoliageForwardVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.foliagePositionWS = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 FoliageForwardFragment(Varyings input) : SV_Target
            {
                input.uv = PixelFoliageUV(input.uv, input.foliagePositionWS);
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
