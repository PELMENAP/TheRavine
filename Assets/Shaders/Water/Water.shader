Shader "The Ravine/Water/WaterShader"
{
    Properties
    {
        [HDR] ShallowColor("Shallow Color", Color) = (0.4, 0.6, 0.8, 1)
        [HDR] DeepColor("Deep Color", Color) = (0.05, 0.15, 0.35, 1)
        [HDR] FoamColor("Foam Color", Color) = (1, 1, 1, 1)

        Smoothness("Smoothness", Range(0, 1)) = 0.9
        ReflectionStrength("Reflection Strength", Range(0, 1)) = 1
        FresnelF0("Fresnel F0", Range(0, 0.1)) = 0.02
        SunGlint("Sun Glint", Float) = 3
        SunGlintPower("Sun Glint Power", Range(64, 2048)) = 512
        SSRIntensity("SSR Intensity", Range(0, 1)) = 1
        SSRStep("SSR Start Step", Float) = 1
        SSRThickness("SSR Thickness", Float) = 3
        SSRMaxDistance("SSR Max Camera Distance", Float) = 120
        SSRBlur("SSR Blur", Range(0, 2)) = 0.5
        SSRRayFade("SSR Ray Fade Distance", Float) = 80
        [IntRange] SSRStepCount("SSR Steps", Range(1, 32)) = 16
        [IntRange] SSRRefineCount("SSR Refine Steps", Range(0, 8)) = 4
        SSRMinWeight("SSR Min Reflection Weight", Range(0, 0.5)) = 0.01
        [Toggle(_WATER_SSR_DISABLED)] SSRDisabled("Disable SSR", Float) = 0
        ReflectionDistortion("Reflection Distortion", Range(0, 1)) = 0.3
        ReflectionGain("Reflection Gain", Range(0, 8)) = 1

        NightFloor("Night Floor", Range(0, 0.1)) = 0.01
        BodyBoost("Body Boost", Range(0, 1)) = 0.5

        [HDR] Absorption("Absorption Per Meter", Color) = (0.35, 0.08, 0.04, 1)
        AbsorptionStrength("Absorption Strength", Range(0, 2)) = 0.7

        [Toggle(_WATER_LOCAL_CAUSTICS)] LocalCaustics("Local Caustics (off if URP Pass active)", Float) = 0
        [NoScaleOffset] CausticTex("Caustic Texture", 2D) = "white" {}
        CausticStrength("Caustic Strength", Range(0, 2)) = 0.5
        CausticScale("Caustic Scale", Float) = 0.15
        CausticSpeed("Caustic Speed", Float) = 0.03
        CausticDepth("Caustic Depth", Float) = 6

        ShoreSoftness("Shore Softness", Float) = 0.4

        DebugView("Debug View (0 off, 1 reflection, 2 fresnel, 3 scene color, 4 depth fade, 5 normal)", Range(0, 5)) = 0

        [Normal][NoScaleOffset] NormalMap("Normal Map", 2D) = "bump" {}
        UV1("UV1 (TilingXY, OffsetZW)", Vector) = (1, 1, 0, 0)
        UV2("UV2 (TilingXY, OffsetZW)", Vector) = (2, 2, 0, 0)
        WaterSpeed1("Water Speed 1", Vector) = (0.05, 0.02, 0, 0)
        WaterSpeed2("Water Speed 2", Vector) = (0.03, 0.01, 0, 0)
        WaterLerp("Normal Blend", Range(0, 1)) = 0.5
        NormalStrength("Normal Strength", Float) = 1.0

        Refraction("Refraction Strength", Float) = 0.1
        Depth("Depth", Float) = 5.0

        Foam("Foam Depth", Float) = 1.0
        [NoScaleOffset] FoamTexture("Foam Texture", 2D) = "white" {}
        FoamScale("Foam Scale", Float) = 3.0
        FoamShoreStrength("Shore Foam", Range(0, 10)) = 3
        FoamCrestStrength("Crest Foam", Range(0, 10)) = 2
        FoamRippleStrength("Ripple Foam", Range(0, 10)) = 1
        FoamIntensity("Foam Intensity", Range(0, 1)) = 1
        FoamThreshold("Foam Threshold", Range(0, 0.95)) = 0.1

        RippleRefraction("Ripple Normal Scale", Float) = 2.0
        RippleShading("Ripple Shading", Range(0, 1)) = 0.35
        [NoScaleOffset] RippleTex("Ripple State Map (R:H, GB:Grad)", 2D) = "black" {}

        Displacement("Displacement", Float) = 0.3
        Scale("Displacement Scale", Float) = 10.0
        Turbidity("Turbidity (1/m)", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #pragma shader_feature_local_fragment _ _WATER_SSR_DISABLED
            #pragma shader_feature_local_fragment _ _WATER_LOCAL_CAUSTICS

            #define SSR_MAX_STEPS 32
            #define SSR_MAX_REFINE 8

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(NormalMap);            SAMPLER(samplerNormalMap);
            TEXTURE2D(FoamTexture);          SAMPLER(samplerFoamTexture);
            TEXTURE2D(RippleTex);            SAMPLER(samplerRippleTex);
            TEXTURE2D(CausticTex);           SAMPLER(samplerCausticTex);
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
            TEXTURE2D(_CameraDepthTexture);  SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(RippleBuffer)
                float4 RippleWindow;
            CBUFFER_END

            CBUFFER_START(UnityPerMaterial)
                float4 ShallowColor;
                float4 DeepColor;
                float4 FoamColor;
                float4 NormalMap_TexelSize;
                float4 UV1;
                float4 UV2;
                float2 WaterSpeed1;
                float2 WaterSpeed2;
                float WaterLerp;
                float NormalStrength;
                float Refraction;
                float Depth;
                float Foam;
                float4 FoamTexture_TexelSize;
                float FoamScale;
                float FoamShoreStrength;
                float FoamCrestStrength;
                float FoamRippleStrength;
                float FoamIntensity;
                float FoamThreshold;
                float RippleRefraction;
                float RippleShading;
                float4 RippleTex_TexelSize;
                float Displacement;
                float Scale;
                float Smoothness;
                float ReflectionStrength;
                float FresnelF0;
                float SunGlint;
                float SunGlintPower;
                float SSRIntensity;
                float SSRStep;
                float SSRThickness;
                float SSRMaxDistance;
                float SSRBlur;
                float SSRRayFade;
                float SSRStepCount;
                float SSRRefineCount;
                float SSRMinWeight;
                float ReflectionDistortion;
                float ReflectionGain;
                float NightFloor;
                float BodyBoost;
                float4 Absorption;
                float AbsorptionStrength;
                float CausticStrength;
                float CausticScale;
                float CausticSpeed;
                float CausticDepth;
                float ShoreSoftness;
                float DebugView;
                float Turbidity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 tangentWS   : TEXCOORD2;
                float2 uv0         : TEXCOORD3;
                float4 positionNDC : TEXCOORD4;
                float3 viewDirWS   : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            float2 GradientNoise_Dir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float GradientNoise(float2 UV, float2 scaleXY)
            {
                float2 p = UV * scaleXY;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(GradientNoise_Dir(ip), fp);
                float d01 = dot(GradientNoise_Dir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(GradientNoise_Dir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(GradientNoise_Dir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            float2 TilingAndOffset(float2 UV, float2 Tiling, float2 Offset)
            {
                return UV * Tiling + Offset;
            }

            float3 NormalStrengthFun(float3 normal, float strength)
            {
                return float3(normal.rg * strength, lerp(1, normal.b, saturate(strength)));
            }

            float SceneDepth_Eye(float2 screenUV)
            {
                float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float3 SceneColor(float2 screenUV)
            {
                return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV).rgb;
            }

            bool SSRProject(float3 posWS, out float2 uv, out float eyeDepth)
            {
                float4 clip = TransformWorldToHClip(posWS);
                eyeDepth = clip.w;
                uv = float2(clip.x, clip.y * _ProjectionParams.x) / max(clip.w, 1e-4) * 0.5 + 0.5;
                return clip.w > 0.0 && all(uv > 0.0) && all(uv < 1.0);
            }

            float SSRDepthDiff(float2 uv, float eyeDepth)
            {
                float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;
                return eyeDepth - LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float SSRDiff(float3 posWS, out float2 uv)
            {
                float eyeDepth;
                if (!SSRProject(posWS, uv, eyeDepth)) return -1e4;
                return SSRDepthDiff(uv, eyeDepth);
            }

            float3 SSRColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv, 0).rgb;
            }

            float4 SSRTrace(float3 posWS, float3 dirWS, float jitter, float distRatio)
            {
                float stepLen = SSRStep;
                float t = stepLen * jitter;
                int maxSteps = clamp((int)(SSRStepCount + 0.5), 1, SSR_MAX_STEPS);
                int minSteps = min(maxSteps, 4);
                int stepCount = clamp((int)lerp((float)maxSteps, (float)minSteps, distRatio), 1, maxSteps);
                int refineCount = clamp((int)(SSRRefineCount + 0.5), 0, SSR_MAX_REFINE);

                [loop]
                for (int i = 0; i < stepCount; i++)
                {
                    float prevT = t;
                    t += stepLen;
                    stepLen *= 1.25;

                    float2 uv;
                    float eyeDepth;
                    if (!SSRProject(posWS + dirWS * t, uv, eyeDepth)) break;

                    float diff = SSRDepthDiff(uv, eyeDepth);
                    if (diff > 0.0 && diff < SSRThickness + stepLen)
                    {
                        float lo = prevT;
                        float hi = t;
                        float2 hiUV = uv;

                        [loop]
                        for (int j = 0; j < refineCount; j++)
                        {
                            float mid = 0.5 * (lo + hi);
                            float2 midUV;
                            if (SSRDiff(posWS + dirWS * mid, midUV) > 0.0)
                            {
                                hi = mid;
                                hiUV = midUV;
                            }
                            else
                            {
                                lo = mid;
                            }
                        }

                        float2 e = saturate(min(hiUV, 1.0 - hiUV) * 8.0);
                        float radius = SSRBlur * 0.01 * saturate(hi / 40.0);
                        float3 color = SSRColor(hiUV);
                        [branch]
                        if (radius > 1e-5)
                        {
                            color = color * 0.4
                                + (SSRColor(hiUV + float2(radius, 0.0))
                                + SSRColor(hiUV - float2(radius, 0.0))
                                + SSRColor(hiUV + float2(0.0, radius))
                                + SSRColor(hiUV - float2(0.0, radius))) * 0.15;
                        }
                        float rayFade = 1.0 - saturate(hi / max(SSRRayFade, 1e-3));
                        return float4(color, e.x * e.y * rayFade);
                    }
                }

                return 0;
            }

            float3 GerstnerWave(
                float2 worldXZ,
                float amplitude,
                float wavelength,
                float speed,
                float2 direction,
                inout float2 slopeXZ)
            {
                float k = 6.2831853 / wavelength;
                float phase = k * dot(direction, worldXZ) + speed * _Time.y;
                float s, c;
                sincos(phase, s, c);

                float ka = k * amplitude * c;
                slopeXZ += direction * ka;

                return float3(
                    direction.x * amplitude * c,
                    amplitude * s,
                    direction.y * amplitude * c
                );
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float2 noiseUV = worldPos.xz / Scale;
                float time = _TimeParameters.x / 100.0;
                float2 animatedUV = TilingAndOffset(noiseUV, float2(1, 1), time.xx);

                float2 slopeXZ = 0.0;
                float3 wave1 = GerstnerWave(worldPos.xz, 0.15, 8.0, 1.5, normalize(float2(1.0, 0.3)), slopeXZ);
                float3 wave2 = GerstnerWave(worldPos.xz, 0.08, 4.0, 2.1, normalize(float2(-0.7, 1.0)), slopeXZ);
                float3 wave3 = GerstnerWave(worldPos.xz, 0.04, 2.0, 3.0, normalize(float2(0.2, -1.0)), slopeXZ);

                float displacement = GradientNoise(animatedUV, float2(10, 10)) * Displacement * 0.15;

                float3 displacedPositionWS = worldPos + wave1 + wave2 + wave3;
                displacedPositionWS.y += displacement;

                float4 positionCS = TransformWorldToHClip(displacedPositionWS);
                float4 ndc = positionCS * 0.5;
                float4 positionNDC;
                positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                positionNDC.zw = positionCS.zw;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 waveNormalWS = normalize(float3(-slopeXZ.x, 1.0, -slopeXZ.y));
                float3 waveTangentWS = normalize(normalInputs.tangentWS - waveNormalWS * dot(normalInputs.tangentWS, waveNormalWS));

                output.positionCS = positionCS;
                output.positionWS = displacedPositionWS;
                output.normalWS = waveNormalWS;
                output.tangentWS = float4(waveTangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv0 = worldPos.xz * RippleWindow.z;
                output.positionNDC = positionNDC;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(displacedPositionWS);
                output.fogFactor = ComputeFogFactor(positionCS.z);

                return output;
            }

            float4 frag(Varyings input, float facing : VFACE) : SV_Target
            {
                float isFrontFace = step(0.0, facing);
                float2 screenUV = input.positionNDC.xy / input.positionNDC.w;
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);

                // === NORMAL MAPS ===
                float2 worldUV = input.positionWS.xz;
                float2 uv1 = TilingAndOffset(worldUV, UV1.xy, UV1.zw + _TimeParameters.x * WaterSpeed1);
                float3 normal1 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap, samplerNormalMap, uv1));

                float2 uv2 = TilingAndOffset(worldUV, UV2.xy, UV2.zw + _TimeParameters.x * WaterSpeed2);
                float3 normal2 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap, samplerNormalMap, uv2));

                float3 normalTS = NormalStrengthFun(lerp(normal1, normal2, WaterLerp), NormalStrength);

                // === RIPPLES (1 выборка вместо 6, только внутри окна) ===
                float2 rippleUV = input.uv0;
                float2 rippleDelta = abs(rippleUV - RippleWindow.xy);
                float rippleEdge = 1.0 - smoothstep(0.44, 0.49, max(rippleDelta.x, rippleDelta.y));

                float rippleHeight = 0.0;
                float3 finalNormalTS = normalTS;

                [branch]
                if (rippleEdge > 1e-4)
                {
                    float3 rippleData = SAMPLE_TEXTURE2D_LOD(RippleTex, samplerRippleTex, rippleUV, 0).rgb;
                    rippleHeight = abs(rippleData.r) * rippleEdge;
                    float3 rippleNormalTS = normalize(float3(
                        -rippleData.g * RippleRefraction,
                        -rippleData.b * RippleRefraction,
                        1.0));
                    finalNormalTS = normalize(lerp(normalTS, rippleNormalTS, saturate(rippleHeight * 2.0)));
                }

                // === REFRACTION ===
                float3 refractOffset = Refraction * finalNormalTS;
                float2 refractedUV1 = screenUV + refractOffset.xy;
                float sceneDepth1 = SceneDepth_Eye(refractedUV1);
                float refractMask = step(surfaceDepth - sceneDepth1, 0.0);

                float2 finalRefractedUV = screenUV + (refractOffset * refractMask).xy;
                float3 sceneColor = SceneColor(finalRefractedUV);

                // === DEPTH-BASED COLOR ===
                float sceneDepth2 = SceneDepth_Eye(finalRefractedUV);
                float depthDiff2 = max(sceneDepth2 - surfaceDepth, 0.0);

                float3 absorbT = exp(-Absorption.rgb * (depthDiff2 * AbsorptionStrength));
                float3 shallowColor = sceneColor * absorbT + ShallowColor.rgb * 0.25;
                float murk = 1.0 - exp(-depthDiff2 * Turbidity);
                float depthFade = saturate(max(depthDiff2 / Depth, murk));
                float3 waterColor = lerp(shallowColor, DeepColor.rgb, depthFade);

                #if defined(_WATER_LOCAL_CAUSTICS)
                float3 bottomWS = _WorldSpaceCameraPos + (input.positionWS - _WorldSpaceCameraPos) * (sceneDepth2 / max(surfaceDepth, 1e-3));
                float2 causticUV = bottomWS.xz * CausticScale;
                float causticTime = _Time.y * CausticSpeed;
                float caustic1 = SAMPLE_TEXTURE2D(CausticTex, samplerCausticTex, causticUV + float2(causticTime, causticTime * 0.7)).r;
                float caustic2 = SAMPLE_TEXTURE2D(CausticTex, samplerCausticTex, causticUV * 1.3 - float2(causticTime * 0.8, -causticTime)).r;
                float causticFade = saturate(1.0 - depthDiff2 / max(CausticDepth, 1e-3)) * saturate(depthDiff2 * 2.0);
                float caustic = min(caustic1, caustic2) * 2.0 * CausticStrength * causticFade * isFrontFace;
                #else
                float caustic = 0.0;
                #endif

                // === NORMALS TO WORLD SPACE ===
                float3x3 tangentToWorld = CreateTangentToWorld(
                    normalize(input.normalWS), normalize(input.tangentWS.xyz), input.tangentWS.w);
                float3 baseNormalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                float3 rippleNormalWS = NormalizeNormalPerPixel(TransformTangentToWorld(finalNormalTS, tangentToWorld));
                float3 normalWS = NormalizeNormalPerPixel(lerp(baseNormalWS, rippleNormalWS, RippleShading));

                float3 viewDirWS = normalize(input.viewDirWS);

                // === BACK FACE: ОКНО СНЕЛЛИУСА (взгляд снизу вверх из-под воды) ===
                if (isFrontFace < 0.5)
                {
                    float3 underNormalWS = -normalWS;
                    float underNdotV = saturate(dot(underNormalWS, viewDirWS));
                    float snellWindow = smoothstep(0.61, 0.71, underNdotV);

                    float3 tirColor = DeepColor.rgb * 0.65;
                    float3 skyRefract = sceneColor * lerp(ShallowColor.rgb, float3(1.0, 1.0, 1.0), 0.65);
                    float3 underFinal = lerp(tirColor, skyRefract, snellWindow);
                    return float4(underFinal, 1.0);
                }

                // === FOAM ===
                float2 foamUV = input.positionWS.xz * FoamScale + _Time.y * float2(0.03, 0.01);
                float4 foamTex = SAMPLE_TEXTURE2D(FoamTexture, samplerFoamTexture, foamUV);
                float foamDepth = SceneDepth_Eye(screenUV) - surfaceDepth;

                float shoreFoam = smoothstep(1.0, 0.0, foamDepth / Foam);
                float crestFoam = saturate((1.0 - normalWS.y) * 8.0);
                float rippleFoam = saturate(rippleHeight * 2.0);

                float foamMask = saturate(
                    shoreFoam * FoamShoreStrength +
                    crestFoam * FoamCrestStrength +
                    rippleFoam * FoamRippleStrength);
                foamMask = saturate((foamMask - FoamThreshold) / max(1.0 - FoamThreshold, 1e-3)) * FoamIntensity;
                float4 foam = foamTex * foamMask + 0.2 * foamMask;

                float3 baseColor = lerp(waterColor, FoamColor.rgb, foam.rgb);

                float3 reflectNormal = normalize(lerp(float3(0.0, 1.0, 0.0), normalWS, ReflectionDistortion));
                float3 reflectDir = reflect(-viewDirWS, reflectNormal);
                reflectDir.y = abs(reflectDir.y);

                half3 reflColor = GlossyEnvironmentReflection(
                    (half3)reflectDir,
                    input.positionWS,
                    (half)(1.0 - Smoothness),
                    1.0h,
                    screenUV) * ReflectionGain;
                    
                float NdotV = saturate(dot(reflectNormal, viewDirWS));
                float fresnel = (FresnelF0 + (1.0 - FresnelF0) * pow(1.0 - NdotV, 5.0)) * ReflectionStrength;

                float dist = length(input.positionWS - _WorldSpaceCameraPos);

                #if !defined(_WATER_SSR_DISABLED)
                float ssrDistRatio = saturate(dist / max(SSRMaxDistance, 1e-3));
                float ssrFade = saturate((SSRMaxDistance - dist) / max(SSRMaxDistance * 0.3, 1e-3)) * SSRIntensity;
                if (fresnel > SSRMinWeight && ssrFade > 0.0)
                {
                    float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
                    float4 ssr = SSRTrace(input.positionWS, reflectDir, jitter, ssrDistRatio);
                    reflColor = lerp(reflColor, ssr.rgb, ssr.a * ssrFade);
                }
                #endif

                float3 bakedGI = SampleSH(normalWS);
                float3 ambient = bakedGI * lerp(baseColor, float3(0.1, 0.1, 0.1), 0.8);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float mainAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                float3 mainDiffuse = baseColor * mainLight.color * (mainAtten * NdotL);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(Smoothness * 10.0) + 1.0;
                float3 mainSpecular = pow(NdotH, specPower) * mainLight.color * mainAtten * Smoothness;

                float3 addDiffuse = 0;
                float3 addSpecular = 0;
                float3 scatteredLight = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                uint additionalLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float NdotLAdd = saturate(dot(normalWS, addLight.direction));
                    addDiffuse += baseColor * addLight.color * addLight.distanceAttenuation * NdotLAdd;

                    float3 halfDirAdd = normalize(addLight.direction + viewDirWS);
                    float NdotHAdd = saturate(dot(normalWS, halfDirAdd));
                    addSpecular += pow(NdotHAdd, specPower) * addLight.color * addLight.distanceAttenuation * Smoothness;

                    scatteredLight += addLight.color * addLight.distanceAttenuation;
                LIGHT_LOOP_END

                float transmission = exp(-depthDiff2 * 0.15);
                float3 scattering = scatteredLight * transmission * 0.05;

                float3 finalColor = ambient + mainDiffuse + mainSpecular + addDiffuse + addSpecular + scattering;

                float distFade = saturate((dist - 50.0) / 100.0);
                float lightIntensity = saturate(Luminance(bakedGI) + Luminance(mainLight.color));
                float3 deepColorAdjusted = DeepColor.rgb * max(lightIntensity, NightFloor);
                finalColor = lerp(finalColor, deepColorAdjusted, distFade);

                finalColor = lerp(finalColor, reflColor, fresnel);
                finalColor += pow(NdotH, SunGlintPower) * SunGlint * mainLight.color * mainAtten;

                float lighting = saturate(Luminance(mainLight.color * mainLight.shadowAttenuation + scatteredLight));
                finalColor += waterColor * lighting * BodyBoost * saturate(lightIntensity);
                finalColor += caustic * mainLight.color * (mainLight.shadowAttenuation * (1.0 - distFade));

                finalColor = MixFog(finalColor, input.fogFactor);

                float alpha = lerp(lerp(0.2, 0.9, depthFade), 1.0, fresnel);

                if (DebugView > 0.5)
                {
                    int view = (int)(DebugView + 0.5);
                    float3 debugColor = view == 1 ? (float3)reflColor
                        : view == 2 ? fresnel.xxx
                        : view == 3 ? sceneColor
                        : view == 4 ? depthFade.xxx
                        : normalWS * 0.5 + 0.5;
                    return float4(debugColor, 1.0);
                }

                float shoreEdge = smoothstep(0.0, max(ShoreSoftness, 1e-3), foamDepth);
                float shoreMask = max(shoreEdge, foamMask);

                return float4(finalColor * shoreMask, alpha * shoreMask);
            }
            ENDHLSL
        }
    }
}