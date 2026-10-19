Shader "Hidden/HubToHome/BackgroundRippleMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _MaskValue ("Background Permission", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend Off Cull Off ZWrite Off ZTest Always ColorMask R
        Pass
        {
            Name "BackgroundMask"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _MaskValue;
            CBUFFER_END
            Varyings MaskVertex(Attributes input)
            {
                // Frame sprites/Tilemaps only: an override material cannot inherit GPU SpriteSkin keywords.
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 MaskFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = CommonUnlitFragment(input, input.color).a;
                // Protect even translucent sprite edges instead of pulling character pixels into the wave.
                clip(alpha - .001h);
                return half4(_MaskValue, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
