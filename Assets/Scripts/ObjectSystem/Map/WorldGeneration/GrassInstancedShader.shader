Shader "Custom/GrassInstancedShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.3, 0.6, 0.2, 1)
        _TipColor ("Tip Color", Color) = (0.5, 0.8, 0.3, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _WindSpeed ("Wind Speed", Float) = 1.0
        _WindStrength ("Wind Strength", Float) = 0.1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Off
        
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct InstanceData
            {
                float4x4 trs;
                float4 color;
            };
            
            StructuredBuffer<InstanceData> instanceData;
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _MainTex_ST;
                float _Cutoff;
                float _WindSpeed;
                float _WindStrength;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float heightFactor : TEXCOORD3;
            };
            
            float3 ApplyWind(float3 positionWS, float heightFactor)
            {
                float windPhase = _Time.y * _WindSpeed + positionWS.x * 0.1 + positionWS.z * 0.1;
                float windWave = sin(windPhase) * _WindStrength;
                
                positionWS.x += windWave * heightFactor;
                positionWS.z += cos(windPhase * 0.7) * _WindStrength * 0.5 * heightFactor;
                
                return positionWS;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                InstanceData instance = instanceData[input.instanceID];
                
                float3 positionWS = mul(instance.trs, float4(input.positionOS.xyz, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)instance.trs, input.normalOS));
                
                float heightFactor = input.positionOS.y;
                positionWS = ApplyWind(positionWS, heightFactor);
                
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = instance.color;
                output.heightFactor = heightFactor;
                
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                clip(texColor.a - _Cutoff);
                
                float4 color = lerp(_BaseColor, _TipColor, input.heightFactor);
                color *= input.color;
                color *= texColor;
                
                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * max(0.0, dot(input.normalWS, mainLight.direction));
                lighting += SampleSH(input.normalWS);
                
                color.rgb *= lighting;
                
                return color;
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct InstanceData
            {
                float4x4 trs;
                float4 color;
            };
            
            StructuredBuffer<InstanceData> instanceData;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            float3 _LightDirection;
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                
                InstanceData instance = instanceData[input.instanceID];
                float3 positionWS = mul(instance.trs, float4(input.positionOS.xyz, 1.0)).xyz;
                
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                
                return output;
            }
            
            float4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Lit"
}