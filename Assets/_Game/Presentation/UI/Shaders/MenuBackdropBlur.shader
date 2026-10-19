Shader "Hidden/HubToHome/MenuBackdropBlur"
{
    Properties { _MainTex ("Background", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Direction;
            half4 frag(v2f_img input) : SV_Target
            {
                float2 delta = _Direction.xy;
                half4 color = tex2D(_MainTex, input.uv) * 0.375h;
                color += tex2D(_MainTex, input.uv + delta) * 0.25h;
                color += tex2D(_MainTex, input.uv - delta) * 0.25h;
                color += tex2D(_MainTex, input.uv + delta * 2) * 0.0625h;
                color += tex2D(_MainTex, input.uv - delta * 2) * 0.0625h;
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
