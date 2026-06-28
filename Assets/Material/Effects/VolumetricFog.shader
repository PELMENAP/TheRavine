Shader "The Ravine/VolumetricFog"
{
    Properties
    {
        _Color                           ("Fog Color",               Color)   = (1,1,1,1)
        _ShadowColor                     ("Shadow Color",            Color)   = (0.5,0.5,0.5,1)
        _SelfShadowColor                 ("Self Shadow Color",       Color)   = (0,0,0,1)
        _Density                         ("Density",                 Range(0, 0.3))   = 1.0
        _MaxDistance                     ("Max Distance",            Float)   = 100.0
        _HeightMaskBlend                 ("Height Mask Blend",       Range(0, 1))   = 1.0
        _HeightMaskLength                ("Height Mask Length",      Float)   = 0.0
        _HeightMaskFalloff               ("Height Mask Falloff",     Range(0, 10))   = 1.0
        _HeightMaskRemapMin              ("Height Mask Remap Min",   Range(0, 1))   = 0.0
        _HeightMaskRemapMax              ("Height Mask Remap Max",   Range(0, 1))   = 1.0
        _HeightMaskTexture               ("Height Mask Texture",     2D)      = "white" {}
        _HeightMaskTextureAmplitude      ("HM Tex Amplitude",        Float)   = 1.0
        _HeightMaskTextureScale          ("HM Tex Scale",            Float)   = 1.0
        _HeightMaskTextureAnimation      ("HM Tex Animation",        Vector)  = (0,0,0,0)
        _MainLightSelfShadowDistance     ("Main Light SS Distance",  Float)   = 10.0
        _AdditionalLightSelfShadowDistance ("Add Light SS Distance", Float)   = 10.0
        _SelfShadowPower                 ("Self Shadow Power",       Float)   = 1.0
        _SelfShadowRemapMin              ("Self Shadow Remap Min",   Float)   = 0.0
        _SelfShadowRemapMax              ("Self Shadow Remap Max",   Float)   = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "VolumetricFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HeightMaskTexture);
            SAMPLER(sampler_HeightMaskTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _HeightMaskTexture_ST;
                half4  _Color;
                half4  _ShadowColor;
                half4  _SelfShadowColor;
                half   _Density;
                float  _MaxDistance;
                half   _HeightMaskBlend;
                float  _HeightMaskLength;
                half   _HeightMaskFalloff;
                half   _HeightMaskRemapMin;
                half   _HeightMaskRemapMax;
                half   _HeightMaskTextureAmplitude;
                half   _HeightMaskTextureScale;
                half2  _HeightMaskTextureAnimation;
                float  _MainLightSelfShadowDistance;
                float  _AdditionalLightSelfShadowDistance;
                half   _SelfShadowPower;
                half   _SelfShadowRemapMin;
                half   _SelfShadowRemapMax;
            CBUFFER_END

            struct FogParams
            {
                half  density;
                half  heightMaskBlend;
                float heightMaskLength;
                half  heightMaskFalloff;
                half  heightMaskRemapMin;
                half  heightMaskRemapMax;
                half  heightMaskTextureAmplitude;
                half  heightMaskTextureScale;
                half2 heightMaskTextureAnimation;
            };

            half EvaluateDensity(float3 positionWS, FogParams p)
            {
                float2 uv = positionWS.xz * ((float)p.heightMaskTextureScale * _HeightMaskTexture_ST.xy)
                          - (_HeightMaskTexture_ST.zw + (float2)p.heightMaskTextureAnimation * _Time.y);
                half s = SAMPLE_TEXTURE2D_LOD(_HeightMaskTexture, sampler_LinearRepeat, uv, 0).r;
                s = abs(s);
                s = s * 2.0h - 1.0h;
                s *= p.heightMaskTextureAmplitude;

                float heightAbove = (positionWS.y - (float)s) - p.heightMaskLength;
                half heightFactor = (half)smoothstep((float)p.heightMaskRemapMin, (float)p.heightMaskRemapMax, heightAbove);
                return lerp(1.0h, exp(-heightFactor * p.heightMaskFalloff), p.heightMaskBlend);
            }

            half ComputeSelfShadow(float3 positionWS, float3 lightDir, float dist, FogParams p)
            {
                float opticalDepth = 0.0;
                float stepSize = dist * 0.5;
                UNITY_UNROLL
                for (int j = 1; j <= 2; ++j)
                {
                    float3 sp = positionWS + lightDir * (float)j * stepSize;
                    opticalDepth += (float)(EvaluateDensity(sp, p) * p.density) * stepSize;
                }
                return exp(-(half)opticalDepth);
            }

            float GetRayDistance(int index, int stepCount, float maxDistance, float jitter)
            {
                float u = ((float)index + jitter) / (float)stepCount;
                return u * u * maxDistance;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvSS = input.texcoord;

                float rawDepth = SampleSceneDepth(uvSS);
                float4 positionVS = mul(UNITY_MATRIX_I_P, ComputeClipSpacePosition(uvSS, rawDepth));
                positionVS.xyz /= positionVS.w;

                float  distToSurface = length(positionVS.xyz);
                float3 dirWS        = mul((float3x3)UNITY_MATRIX_I_V, normalize(positionVS.xyz));
                float3 rayOrigin    = GetCameraPositionWS();

                uint frame = (uint)(_Time.y * 60.0) % 60u;
                half noise = (half)InterleavedGradientNoise(uvSS * _ScaledScreenParams.xy, (float)frame);
                float maxDist  = min(distToSurface, _MaxDistance);
                float rayStep = maxDist / 16;
                half  densityPerStep = _Density * (half)rayStep;

                FogParams fp;
                fp.density                    = _Density;
                fp.heightMaskBlend            = _HeightMaskBlend;
                fp.heightMaskLength           = _HeightMaskLength;
                fp.heightMaskFalloff          = _HeightMaskFalloff;
                fp.heightMaskRemapMin         = _HeightMaskRemapMin;
                fp.heightMaskRemapMax         = _HeightMaskRemapMax;
                fp.heightMaskTextureAmplitude = _HeightMaskTextureAmplitude;
                fp.heightMaskTextureScale     = _HeightMaskTextureScale;
                fp.heightMaskTextureAnimation = _HeightMaskTextureAnimation;

                Light mainLight  = GetMainLight();
                bool  mainLightOn = max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b)) > 0.001;

                half3 lighting      = 0.0h;
                half  transmittance = 1.0h;
                half shadow;
                

                [unroll]
                for (int i = 0; i < 16; ++i)
                {
                    float t      = GetRayDistance(i,     16, maxDist, noise);
                    float nextT  = GetRayDistance(i + 1, 16, maxDist, noise);
                    float stepLength = nextT - t;

                    if (t > maxDist) break;

                    float3 pos     = rayOrigin + dirWS * t;
                    half   fogDens = EvaluateDensity(pos, fp) * _Density * (half)stepLength;
                    half   stepTrans = exp(-fogDens);
                    half   scatter   = 1.0h - stepTrans;
                    half3  stepLight = 0.0h;

                    if (mainLightOn)
                    {
                        if (mainLightOn && (i % 4 == 0))
                        {
                            float4 shadowCoord = TransformWorldToShadowCoord(pos);
                            shadow = (half)MainLightShadow(shadowCoord, pos, half4(1,1,1,1), _MainLightOcclusionProbes);
                        }
                        shadow = lerp(1.0h, shadow, _ShadowColor.a);

                        half selfShadow = 1.0h;
                        if (shadow > 0.001h)
                        {
                            selfShadow = ComputeSelfShadow(pos, mainLight.direction, _MainLightSelfShadowDistance, fp);
                            selfShadow = lerp(1.0h, selfShadow, shadow * _SelfShadowColor.a);
                            selfShadow = (half)smoothstep((float)_SelfShadowRemapMin, (float)_SelfShadowRemapMax, (float)selfShadow);
                            selfShadow = pow(selfShadow, _SelfShadowPower);
                        }

                        half3 ml = (half3)mainLight.color;
                        ml = lerp(ml, _ShadowColor.rgb,     1.0h - shadow);
                        ml = lerp(ml, _SelfShadowColor.rgb, 1.0h - selfShadow);
                        stepLight += ml;
                    }

                    uint lightCount = min(GetAdditionalLightsCount(), 8);
                    [loop]
                    LIGHT_LOOP_BEGIN(lightCount)
                    {
                        Light al       = GetAdditionalLight(lightIndex, pos);
                        half  alShadow = (half)AdditionalLightShadow(lightIndex, pos, al.direction, half4(1,1,1,1), half4(1,1,1,1));
                        alShadow = lerp(1.0h, alShadow, _ShadowColor.a);
                        half alDist = (half)al.distanceAttenuation;

                        if (alShadow > 0.001h && alDist > 0.00001h)
                        {
                            float ssDist = _AdditionalLightSelfShadowDistance;
                            half alSelf = ComputeSelfShadow(pos, al.direction, ssDist, fp);
                            alSelf = lerp(1.0h, alSelf, alShadow * _SelfShadowColor.a);
                            alSelf = (half)smoothstep((float)_SelfShadowRemapMin, (float)_SelfShadowRemapMax, (float)alSelf);
                            alSelf = pow(alSelf, _SelfShadowPower);

                            half3 alColor = (half3)al.color;
                            alColor = lerp(alColor, _ShadowColor.rgb,     1.0h - alShadow);
                            alColor = lerp(alColor, _SelfShadowColor.rgb, 1.0h - alSelf);
                            alColor *= alDist;
                            stepLight += alColor;
                        }
                    }
                    LIGHT_LOOP_END

                    stepLight     *= scatter;
                    lighting      += stepLight * transmittance;
                    transmittance *= stepTrans;

                    if (transmittance < 0.001h)
                    {
                        transmittance = 0.0h;
                        break;
                    }
                }

                lighting *= _Color.rgb;
                half4 scene = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvSS);
                return half4(lerp(scene.rgb, scene.rgb * transmittance + lighting, _Color.a), scene.a);
            }
            ENDHLSL
        }
    }
}