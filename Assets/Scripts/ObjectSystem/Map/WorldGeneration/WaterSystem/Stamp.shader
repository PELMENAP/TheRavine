Shader "Hidden/RippleStamp"
{
    Properties { _Strength("Strength", Float) = 1.0 }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "StampPass"
            Blend One One
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Strength;
            float2 _StampCenter; // UV [0,1]
            float _StampRadius;  // UV-space

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.uv - _StampCenter) / _StampRadius;
                float v = _Strength * saturate(1.0 - d * d);
                return half4(v, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}