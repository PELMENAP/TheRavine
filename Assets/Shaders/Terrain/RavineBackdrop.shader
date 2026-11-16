Shader "The Ravine/Terrain/RavineBackdrop"
{
    Properties
    {
        [Header(Ground)]
        _TexScale ("Biome Texture Scale", Float) = 0.1
        _BackdropClimate ("Valley Climate (temperature, moisture)", Vector) = (0.5, 0.5, 0, 0)
        _PlateauCooling ("Plateau Cooling", Range(0, 1)) = 0.35

        [Header(Rock)]
        _RockTex ("Rock", 2D) = "gray" {}
        _RockColor ("Rock Tint", Color) = (1, 1, 1, 1)
        _RockScale ("Rock Scale", Float) = 0.08
        _RockSlopeMin ("Rock Slope Min", Range(0, 1)) = 0.25
        _RockSlopeMax ("Rock Slope Max", Range(0, 1)) = 0.45

        [Header(Silhouette)]
        _NoiseScale ("Noise Scale", Float) = 0.002
        _NoiseHeight ("Noise Height", Float) = 120

        [Header(Distance)]
        _PullStart ("Pull Start (fraction of far plane)", Range(0.5, 0.99)) = 0.9
        _PullLimit ("Pull Limit (fraction of far plane)", Range(0.5, 0.999)) = 0.98
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/Include/BiomeCommon.hlsl"
        #include "Assets/Shaders/Include/RavineProfile.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _TexScale;
            float4 _BackdropClimate;
            float _PlateauCooling;
            float _RockScale;
            float _RockSlopeMin;
            float _RockSlopeMax;
            float4 _RockColor;
            float _NoiseScale;
            float _NoiseHeight;
            float _PullStart;
            float _PullLimit;
        CBUFFER_END

        float _BackdropSide;
        float4 _BackdropReach;
        float _BackdropHaze;

        TEXTURE2D_ARRAY(_BiomeAlbedo); SAMPLER(sampler_BiomeAlbedo);
        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);

        struct BackdropAttributes
        {
            float4 positionOS : POSITION;
        };

        float BackdropHash(float2 p)
        {
            p = frac(p * float2(0.1031, 0.1030));
            p += dot(p, p.yx + 33.33);
            return frac((p.x + p.y) * p.x);
        }

        float BackdropNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = BackdropHash(i);
            float b = BackdropHash(i + float2(1.0, 0.0));
            float c = BackdropHash(i + float2(0.0, 1.0));
            float d = BackdropHash(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float BackdropHeight(float2 xz)
        {
            float2 p = xz * _NoiseScale;
            float n = BackdropNoise(p) * 0.65 + BackdropNoise(p * 2.9) * 0.35;
            return RavineElevation(xz.y) + n * _NoiseHeight;
        }

        void BackdropVertex(BackdropAttributes input, out float3 groundWS, out float3 normalWS, out float3 drawnWS, out float trueDistance)
        {
            float3 p = TransformObjectToWorld(float3(input.positionOS.x, 0.0, input.positionOS.z * _BackdropSide));
            float h = BackdropHeight(p.xz);
            float hx = BackdropHeight(p.xz + float2(1.0, 0.0));
            float hz = BackdropHeight(p.xz + float2(0.0, 1.0));
            p.y = h;
            groundWS = p;
            normalWS = normalize(float3(h - hx, 1.0, h - hz));

            float3 toVertex = p - _WorldSpaceCameraPos;
            trueDistance = length(toVertex);
            float farPlane = _ProjectionParams.z;
            float start = farPlane * _PullStart;
            float limit = farPlane * max(_PullLimit, _PullStart);
            float pulled = trueDistance <= start
                ? trueDistance
                : start + (limit - start) * (1.0 - exp(-(trueDistance - start) / farPlane));
            drawnWS = _WorldSpaceCameraPos + toVertex * (pulled / max(trueDistance, 1e-4));
        }

        void ClipStreamedTerrain(float3 groundWS)
        {
            float2 d = abs(groundWS.xz - _BackdropReach.xy);
            clip(max(d.x, d.y) - _BackdropReach.z);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 groundWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float trueDistance : TEXCOORD2;
            };

            Varyings vert(BackdropAttributes input)
            {
                Varyings output;
                float3 drawnWS;
                BackdropVertex(input, output.groundWS, output.normalWS, drawnWS, output.trueDistance);
                output.positionCS = TransformWorldToHClip(drawnWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ClipStreamedTerrain(input.groundWS);

                float3 normalWS = normalize(input.normalWS);
                float blend = RavineBlend(input.groundWS.z);

                float2 climate = _BackdropClimate.xy;
                climate.x -= _PlateauCooling * blend;
                BiomeBlend biome = BiomeTop2(climate);
                float3 albedo = SAMPLE_TEXTURE2D_ARRAY(_BiomeAlbedo, sampler_BiomeAlbedo, input.groundWS.xz * _TexScale, biome.layerA).rgb;
                albedo *= _BiomeTint[biome.indexA].rgb * biome.weightA + _BiomeTint[biome.indexB].rgb * biome.weightB;

                float rockMask = smoothstep(_RockSlopeMin, _RockSlopeMax, 1.0 - saturate(normalWS.y));
                UNITY_BRANCH
                if (rockMask > 0.001)
                {
                    float2 uv = abs(normalWS.x) > abs(normalWS.z) ? input.groundWS.zy : input.groundWS.xy;
                    float3 rock = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uv * _RockScale).rgb * _RockColor.rgb;
                    albedo = lerp(albedo, rock, rockMask);
                }

                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction)) + SampleSH(normalWS);
                float3 color = albedo * lighting;

                float haze = 1.0 - exp(-_BackdropHaze * input.trueDistance);
                color = lerp(color, unity_FogColor.rgb, haze);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 groundWS : TEXCOORD0;
            };

            DepthVaryings DepthVert(BackdropAttributes input)
            {
                DepthVaryings output;
                float3 normalWS, drawnWS;
                float trueDistance;
                BackdropVertex(input, output.groundWS, normalWS, drawnWS, trueDistance);
                output.positionCS = TransformWorldToHClip(drawnWS);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                ClipStreamedTerrain(input.groundWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 groundWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            NormalsVaryings DepthNormalsVert(BackdropAttributes input)
            {
                NormalsVaryings output;
                float3 drawnWS;
                float trueDistance;
                BackdropVertex(input, output.groundWS, output.normalWS, drawnWS, trueDistance);
                output.positionCS = TransformWorldToHClip(drawnWS);
                return output;
            }

            half4 DepthNormalsFrag(NormalsVaryings input) : SV_Target
            {
                ClipStreamedTerrain(input.groundWS);
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
                #else
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
