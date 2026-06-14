Shader "Unlit/Add"
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
            Name "AddPass"

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

            TEXTURE2D(_ObjectsRT);
            SAMPLER(sampler_ObjectsRT);

            TEXTURE2D(_CurrentRT);
            SAMPLER(sampler_CurrentRT);

            float4 _ObjectsRT_ST;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _ObjectsRT);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex1 = SAMPLE_TEXTURE2D(_ObjectsRT, sampler_ObjectsRT, input.uv);
                half4 tex2 = SAMPLE_TEXTURE2D(_CurrentRT, sampler_CurrentRT, input.uv);
                return tex1 + tex2;
            }
            ENDHLSL
        }
    }
}