Shader "Hidden/HubToHome/BackgroundRipple"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "BackgroundRipple"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_BackgroundMask);
            float4 _RippleGeometry; // centre UV.xy, radius pixels, band half-width pixels
            float _RippleAmplitude;
            half4 _RippleColor;
            float4 _ReferenceSize;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float2 p = (uv - _RippleGeometry.xy) * _ReferenceSize.xy;
                float distance = length(p);
                float band = (distance - _RippleGeometry.z) / _RippleGeometry.w;
                float envelope = saturate(1 - abs(band));
                half destinationMask = SAMPLE_TEXTURE2D_X(_BackgroundMask, sampler_PointClamp, uv).r;
                if (envelope <= 0 || destinationMask < .5h) return original;

                // A compact bipolar wave, normalised to the configured displacement. No noise/blur/depth.
                float displacement = band * envelope * envelope * 6.75 * _RippleAmplitude;
                float2 candidateUV = uv + p / max(distance, .001) * displacement * _ReferenceSize.zw;
                if (any(candidateUV < 0) || any(candidateUV > 1)) return original;
                half sourceMask = SAMPLE_TEXTURE2D_X(_BackgroundMask, sampler_PointClamp, candidateUV).r;
                // Checking both endpoints prevents moving a sprite OR sampling it into adjacent background.
                if (sourceMask < .5h) return original;
                half4 warped = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, candidateUV);
                warped.rgb = lerp(warped.rgb, _RippleColor.rgb, envelope * _RippleColor.a);
                return warped;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
