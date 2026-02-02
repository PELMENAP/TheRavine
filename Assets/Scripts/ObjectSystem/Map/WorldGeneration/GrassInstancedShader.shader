Shader "Custom/ChunkGrassShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.3, 0.6, 0.2, 1)
        _TipColor ("Tip Color", Color) = (0.5, 0.8, 0.3, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _WindSpeed ("Wind Speed", Float) = 1.0
        _WindStrength ("Wind Strength", Float) = 0.1
        
        [Header(Advanced Coloring)]
        _HeightColorBlend ("Height Color Blend", Range(0, 1)) = 1.0
        _InstanceColorBlend ("Instance Color Blend", Range(0, 1)) = 1.0
        _AmbientOcclusion ("Ambient Occlusion", Range(0, 1)) = 0.2
        
        [Header(Wind Advanced)]
        _WindDirection ("Wind Direction", Vector) = (1, 0, 1, 0)
        _WindGustStrength ("Wind Gust Strength", Range(0, 1)) = 0.3
        _WindGustFrequency ("Wind Gust Frequency", Float) = 2.3
    }
    
    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
        }
        LOD 200
        Cull Off
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
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
            float _HeightColorBlend;
            float _InstanceColorBlend;
            float _AmbientOcclusion;
            float4 _WindDirection;
            float _WindGustStrength;
            float _WindGustFrequency;
        CBUFFER_END
        
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        float3 ApplyWind(float3 positionWS, float heightFactor)
        {
            float time = _Time.y * _WindSpeed;
            float3 windDir = normalize(_WindDirection.xyz);
            
            float mainWave = sin(time + dot(positionWS.xz, windDir.xz) * 0.1);
            float gustWave = sin(time * _WindGustFrequency + positionWS.x * 0.05) * _WindGustStrength;
            float microWave = sin(time * 4.7 + positionWS.x * 0.3 + positionWS.z * 0.3) * 0.2;
            
            float totalWind = (mainWave + gustWave + microWave) * _WindStrength;
            float windEffect = pow(heightFactor, 2.0);
            
            positionWS += windDir * totalWind * windEffect;
            
            return positionWS;
        }
        
        float3 TransformObjectToWorldWithWind(float4 positionOS, float4x4 instanceTRS, out float heightFactor)
        {
            float3 positionWS = mul(positionOS, instanceTRS).xyz;
            heightFactor = positionOS.y;
            return ApplyWind(positionWS, heightFactor);
        }
        
        ENDHLSL
        
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            
            ZTest LEqual
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
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
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                InstanceData instance = instanceData[input.instanceID];
                
                float heightFactor;
                float3 positionWS = TransformObjectToWorldWithWind(input.positionOS, instance.trs, heightFactor);
                
                float3 normalWS = normalize(mul((float3x3)instance.trs, input.normalOS));
                
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
                
                float4 heightColor = lerp(_BaseColor, _TipColor, input.heightFactor);
                float4 finalColor = lerp(float4(1, 1, 1, 1), heightColor, _HeightColorBlend);
                finalColor = lerp(finalColor, finalColor * input.color, _InstanceColorBlend);
                finalColor *= texColor;
                
                float ao = lerp(1.0, 1.0 - _AmbientOcclusion, 1.0 - input.heightFactor);
                
                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * max(0.0, dot(input.normalWS, mainLight.direction));
                lighting += SampleSH(input.normalWS);
                
                finalColor.rgb *= lighting * ao;
                
                return finalColor;
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
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            float3 _LightDirection;
            
            float4 GetShadowPositionHClip(float3 positionWS)
            {
                float4 positionCS = TransformWorldToHClip(positionWS);
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return positionCS;
            }
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                
                InstanceData instance = instanceData[input.instanceID];
                
                float heightFactor;
                float3 positionWS = TransformObjectToWorldWithWind(input.positionOS, instance.trs, heightFactor);
                
                output.positionCS = GetShadowPositionHClip(positionWS);
                output.uv = input.uv;
                
                return output;
            }
            
            float4 ShadowFrag(Varyings input) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {       
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                InstanceData instance = instanceData[input.instanceID];

                float heightFactor;
                float3 positionWS = TransformObjectToWorldWithWind(input.positionOS, instance.trs, heightFactor);

                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                return o;
            }

            float4 DepthFrag(Varyings input) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Lit"
}