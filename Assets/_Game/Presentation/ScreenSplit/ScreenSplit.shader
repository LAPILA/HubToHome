Shader "Hidden/HubToHome/ScreenSplit"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "ScreenSplit"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _SplitLine; // tangent.xy, pivot UV.zw
            float4 _SplitWarp; // separation pixels, cos twist, sin twist, line width pixels
            half4 _SplitColor;
            float4 _ReferenceSize;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 tangent = _SplitLine.xy;
                float2 normal = float2(-tangent.y, tangent.x);
                float2 p = (input.texcoord - _SplitLine.zw) * _ReferenceSize.xy;
                float distance = dot(p, normal);
                float side = step(0, distance) * 2 - 1;
                float2 offset = p - normal * (side * _SplitWarp.x);
                float sine = -side * _SplitWarp.z;
                float2 warped = float2(_SplitWarp.y * offset.x - sine * offset.y,
                    sine * offset.x + _SplitWarp.y * offset.y);
                float2 uv = warped * _ReferenceSize.zw + _SplitLine.zw;
                // Point sampling preserves sharp texels without a blur/noise/depth texture.
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float inBounds = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                float gap = step(abs(distance), _SplitWarp.x);
                // Draw only outside both cut edges, never as a stripe through the black centre.
                float edgeDistance = abs(distance) - _SplitWarp.x;
                float cutLineMask = (step(0, edgeDistance) - step(_SplitWarp.w, edgeDistance)) * _SplitColor.a;
                color.rgb *= inBounds;
                color.rgb = lerp(color.rgb, _SplitColor.rgb, cutLineMask);
                // Keep the cut completely black, including its exact boundary.
                color.rgb *= 1 - gap;
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
