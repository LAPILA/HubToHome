Shader "HubToHome/Weather/Fog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Fog Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            // Both the existing UniversalRenderer and Renderer2D accept this unlit pass.
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _WeatherHole;
                float _WeatherHoleEnabled;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 positionWS : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS).xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 fog = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                // Quantized, gently drifting coverage keeps the no-art sample recognizable as fog.
                float2 pixelWorld = floor(input.positionWS * 16.0) / 16.0;
                float drift = _Time.y * .13;
                float variation = .83 + .12 * sin(pixelWorld.x * .9 + drift) * sin(pixelWorld.y * 1.2 - drift * .7);
                float distanceToTarget = distance(input.positionWS, _WeatherHole.xy);
                float hole = smoothstep(_WeatherHole.z, _WeatherHole.z + max(.001, _WeatherHole.w), distanceToTarget);
                fog.a *= variation * lerp(1.0, hole, _WeatherHoleEnabled);
                return fog;
            }
            ENDHLSL
        }
    }
}
