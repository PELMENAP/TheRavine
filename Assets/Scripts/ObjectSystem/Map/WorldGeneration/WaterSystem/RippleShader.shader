Shader "The Ravine/Water/RippleShader"
{
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "RipplePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(PrevRT);
            SAMPLER(sampler_PrevRT);

            TEXTURE2D(CurrentRT);
            SAMPLER(sampler_CurrentRT);

            float4 CurrentRT_TexelSize;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 e = float3(CurrentRT_TexelSize.xy, 0.0);
                float2 uv = input.uv;
                float speed = 0.2;

                float p10 = SAMPLE_TEXTURE2D(CurrentRT, sampler_CurrentRT, uv - e.zy * speed).x;
                float p01 = SAMPLE_TEXTURE2D(CurrentRT, sampler_CurrentRT, uv - e.xz * speed).x;
                float p21 = SAMPLE_TEXTURE2D(CurrentRT, sampler_CurrentRT, uv + e.xz * speed).x;
                float p12 = SAMPLE_TEXTURE2D(CurrentRT, sampler_CurrentRT, uv + e.zy * speed).x;

                float p11 = SAMPLE_TEXTURE2D(PrevRT, sampler_PrevRT, uv).x;

                float d = (p10 + p01 + p21 + p12) * 0.5 - p11;
                d *= 0.99;

                return half4(d, d, d, 1.0);
            }
            ENDHLSL
        }
    }
}

