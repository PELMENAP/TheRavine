Shader "The Ravine/WaterShader"
{
    Properties
    {
        [HDR] ShallowColor("Shallow Color", Color) = (0.4, 0.6, 0.8, 1)
        [HDR] DeepColor("Deep Color", Color) = (0.05, 0.15, 0.35, 1)
        [HDR] FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        
        Smoothness("Smoothness", Range(0, 1)) = 0.9
        
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
        FoamShoreStrength("Shore Foam", Range(0,10)) = 3
        FoamCrestStrength("Crest Foam", Range(0,10)) = 2
        FoamRippleStrength("Ripple Foam", Range(0,10)) = 1
        
        RippleRefraction("Ripple Refraction", Float) = 0.1
        [NoScaleOffset] _RippleTex("Ripple Height Map", 2D) = "white" {}
        
        _Displacement("Displacement", Float) = 0.3
        _Scale("Displacement Scale", Float) = 10.0
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
            ZWrite off
            Cull Off
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            
            TEXTURE2D(NormalMap);       SAMPLER(samplerNormalMap);
            TEXTURE2D(FoamTexture);     SAMPLER(samplerFoamTexture);
            TEXTURE2D(_RippleTex);      SAMPLER(sampler_RippleTex);
            TEXTURE2D(_CameraOpaqueTexture);    SAMPLER(sampler_CameraOpaqueTexture);
            TEXTURE2D(_CameraDepthTexture);     SAMPLER(sampler_CameraDepthTexture);
            
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
                float RippleRefraction;
                float4 _RippleTex_TexelSize;
                float _Displacement;
                float _Scale;
                float Smoothness;
                float Metallic;
            CBUFFER_END
            
            // ----------------------------------------------------------------
            // STRUCTS
            // ----------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv0 : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv0 : TEXCOORD3;
                float4 positionNDC : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float3 viewDirWS : TEXCOORD6;
                float fogFactor : TEXCOORD7;
                float4 shadowCoord : TEXCOORD8;
            };
            
            // ----------------------------------------------------------------
            // HELPER FUNCTIONS (аналоги Shader Graph нодов)
            // ----------------------------------------------------------------
            
            // Deterministic Gradient Noise (из Hashes.hlsl)
            float2 GradientNoise_Dir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x;
                float fx = x / 41;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }
            
            float GradientNoise(float2 UV, float3 Scale)
            {
                float2 p = UV * Scale.xy;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(GradientNoise_Dir(ip), fp);
                float d01 = dot(GradientNoise_Dir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(GradientNoise_Dir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(GradientNoise_Dir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }
            
            // Low Res Normal From Height — 5 сэмплов height map
            float3 LowResNormalFromHeight(Texture2D heightTex, SamplerState samp, 
                                         float2 uv, float2 resolution, float strength)
            {
                float2 texel = float2(1.0 / resolution.x, 1.0 / resolution.y) * strength;
                
                // Center
                float hC = SAMPLE_TEXTURE2D(heightTex, samp, uv).r;
                // Left
                float hL = SAMPLE_TEXTURE2D(heightTex, samp, uv - float2(texel.x, 0)).r;
                // Right
                float hR = SAMPLE_TEXTURE2D(heightTex, samp, uv + float2(texel.x, 0)).r;
                // Down
                float hD = SAMPLE_TEXTURE2D(heightTex, samp, uv - float2(0, texel.y)).r;
                // Up
                float hU = SAMPLE_TEXTURE2D(heightTex, samp, uv + float2(0, texel.y)).r;
                
                float dx = ((hR - hC) + (hC - hL)) * 0.5; // = (hR - hL) * 0.5
                float dy = ((hU - hC) + (hC - hD)) * 0.5; // = (hU - hD) * 0.5
                
                float3 tangentX = float3(texel.x, 0, dx);
                float3 tangentY = float3(0, texel.y, dy);
                
                float3 normal = cross(tangentX, tangentY);
                return normalize(normal);
            }
            
            // TilingAndOffset
            float2 TilingAndOffset(float2 UV, float2 Tiling, float2 Offset)
            {
                return UV * Tiling + Offset;
            }
            
            // NormalStrength
            float3 NormalStrengthFun(float3 normal, float strength)
            {
                return float3(normal.rg * strength, lerp(1, normal.b, saturate(strength)));
            }
            
            // SceneDepth Eye
            float SceneDepth_Eye(float2 screenUV)
            {
                float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, 
                    sampler_CameraDepthTexture, screenUV).r;
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }
            
            // SceneColor
            float3 SceneColor(float2 screenUV)
            {
                return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, 
                    sampler_CameraOpaqueTexture, screenUV).rgb;
            }

            float3 GerstnerWave(
                float2 worldXZ,
                float amplitude,
                float wavelength,
                float speed,
                float2 direction)
            {
                float k = 6.2831853 / wavelength;

                float phase =
                    k * dot(direction, worldXZ)
                    + speed * _Time.y;

                float s = sin(phase);
                float c = cos(phase);

                return float3(
                    direction.x * amplitude * c,
                    amplitude * s,
                    direction.y * amplitude * c
                );
            }
            
            // ----------------------------------------------------------------
            // VERTEX SHADER
            // ----------------------------------------------------------------
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // WorldSpacePosition XZ как UV для noise
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float2 noiseUV = worldPos.xz;
                
                // Divide by Scale
                noiseUV /= _Scale;
                
                // Time / 100
                float time = _TimeParameters.x / 100.0;
                
                // TilingAndOffset (Tiling = 1,1, Offset = time)
                float2 animatedUV = TilingAndOffset(noiseUV, float2(1, 1), time.xx);
                
                // Gradient Noise (Scale = 10)
                float3 wave1 =
                    GerstnerWave(
                        worldPos.xz,
                        0.15,
                        8.0,
                        1.5,
                        normalize(float2(1,0.3)));

                float3 wave2 =
                    GerstnerWave(
                        worldPos.xz,
                        0.08,
                        4.0,
                        2.1,
                        normalize(float2(-0.7,1)));

                float3 wave3 =
                    GerstnerWave(
                        worldPos.xz,
                        0.04,
                        2.0,
                        3.0,
                        normalize(float2(0.2,-1)));

                float displacement =
                    GradientNoise(
                        animatedUV,
                        float3(10,10,10))
                    * _Displacement
                    * 0.15;

                float3 displacedPos =
                    input.positionOS.xyz +
                    wave1 +
                    wave2 +
                    wave3;

                displacedPos.y += displacement;

                                
                // === TRANSFORM ===
                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedPos);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv0 = input.uv0;
                output.positionNDC = posInputs.positionNDC;
                output.screenPos = ComputeScreenPos(posInputs.positionCS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                
                return output;
            }
            
            // ----------------------------------------------------------------
            // FRAGMENT SHADER
            // ----------------------------------------------------------------
            float4 frag(Varyings input, float facing : VFACE) : SV_Target
            {
                // === FRONT FACE ===
                float isFrontFace = max(0, facing);
                
                // === SCREEN POSITION ===
                float2 screenUV = input.positionNDC.xy / input.positionNDC.w;
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                
                
                // === NORMAL MAPS ===
                // WorldPos XZ как базовые UV
                float2 worldUV = input.positionWS.xz;
                
                // --- Normal 1 ---
                // UV1: RG = Tiling, BA = Offset
                float2 tiling1 = UV1.xy;
                float2 offset1 = UV1.zw;
                float2 speed1 = WaterSpeed1;
                float2 animatedOffset1 = offset1 + _TimeParameters.x * speed1;
                float2 uv1 = TilingAndOffset(worldUV, tiling1, animatedOffset1);
                float3 normal1 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap, samplerNormalMap, uv1));
                
                // --- Normal 2 ---
                float2 tiling2 = UV2.xy;
                float2 offset2 = UV2.zw;
                float2 speed2 = WaterSpeed2;
                float2 animatedOffset2 = offset2 + _TimeParameters.x * speed2;
                float2 uv2 = TilingAndOffset(worldUV, tiling2, animatedOffset2);
                float3 normal2 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap, samplerNormalMap, uv2));
                
                // Lerp normals
                float3 blendedNormal = lerp(normal1, normal2, WaterLerp);
                
                // Normal Strength
                float3 normalTS = NormalStrengthFun(blendedNormal, NormalStrength);
                
                // === RIPPLES ===
                // LowResNormalFromHeight для _RippleTex
                float3 rippleNormalForRefract = LowResNormalFromHeight(_RippleTex, sampler_RippleTex, input.uv0, _RippleTex_TexelSize.zw, RippleRefraction);
                float rippleHeight = abs(SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, input.uv0).r);
                float3 finalNormalTS  = lerp(normalTS, rippleNormalForRefract,  rippleHeight);

                
                // === REFRACTION (двойное искажение с depth check) ===
                // Нормали * Refraction
                float3 refractOffset = Refraction * finalNormalTS;
                
                // Первое искажение для depth check
                float2 refractedUV1 = screenUV + refractOffset.xy;
                float sceneDepth1 = SceneDepth_Eye(refractedUV1);
                float depthDiff1 = surfaceDepth - sceneDepth1;
                float refractMask = step(depthDiff1, 0.0);
                
                // Умножаем offset на маску (не рефрактим за объектами)
                float3 safeRefract = refractOffset * refractMask;
                
                // Финальные UV для SceneColor
                float2 finalRefractedUV = screenUV + safeRefract.xy;
                
                // SceneColor
                float3 sceneColor = SceneColor(finalRefractedUV);
                
                // === DEPTH-BASED COLOR ===
                // SceneDepth по финальным UV
                float sceneDepth2 = SceneDepth_Eye(finalRefractedUV);
                float depthDiff2 = sceneDepth2 - surfaceDepth;
                
                // ShallowColor * SceneColor (УМНОЖЕНИЕ, не lerp!)
                float3 shallowColor =
                    sceneColor +
                    ShallowColor.rgb * 0.25;
                
                // Depth fade: (sceneDepth - surfaceDepth) / Depth
                float depthFade = saturate(depthDiff2 / Depth);
                
                // Lerp(Shallow*SceneColor, DeepColor, depthFade)
                float3 waterColor = lerp(shallowColor, DeepColor.rgb, depthFade);

                // === NORMALS TO WORLD SPACE ===
                float3x3 tangentToWorld = CreateTangentToWorld(
                    input.normalWS, input.tangentWS.xyz, input.tangentWS.w);
                float3 normalWS = TransformTangentToWorld(finalNormalTS, tangentToWorld);
                normalWS = NormalizeNormalPerPixel(normalWS);
                
                // === FOAM ===
                // Foam texture
                float2 foamUV =
                    input.positionWS.xz *
                    FoamScale +
                    _Time.y * float2(0.03, 0.01);
                float4 foamTex = SAMPLE_TEXTURE2D(FoamTexture, samplerFoamTexture, foamUV);
                
                // Depth-based foam mask
                float foamDepth = SceneDepth_Eye(screenUV) - surfaceDepth;

                float shoreFoam =
                    smoothstep(
                        1.0,
                        0.0,
                        foamDepth / Foam
                    );

                float crestFoam =
                    saturate(
                        (1.0 - normalWS.y) * 8.0
                    );

                float rippleFoam =
                    saturate(rippleHeight * 2.0);

                float foamMask =
                    shoreFoam * FoamShoreStrength +
                    crestFoam * FoamCrestStrength +
                    rippleFoam * FoamRippleStrength;

                foamMask = saturate(foamMask);
                
                // FoamTex * foamMask + 0.2 * foamMask (базовая пена)
                float4 foam = foamTex * foamMask + 0.2 * foamMask;
                
                // === BACK FACE LOGIC ===
                // Front face: full water with foam, reflection, etc.
                // Back face: only ShallowColor * SceneColor (простое преломление)
                float3 frontFaceColor = lerp(waterColor, FoamColor.rgb, foam.rgb);;
                float3 backFaceColor = shallowColor; // ShallowColor * SceneColor
                
                float3 baseColor = lerp(backFaceColor, frontFaceColor, isFrontFace);
                
                // === REFLECTION ===
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                
                half3 reflColor = GlossyEnvironmentReflection(reflectDir, input.positionWS, 1.0 - Smoothness, 1.0);

                // === РУЧНОЕ ОСВЕЩЕНИЕ (вместо UniversalFragmentPBR) ===

                // 1. GI (Light Probes / Sky)
                float3 bakedGI = SampleSH(normalWS);
                float3 ambient = bakedGI * lerp(baseColor, float3(0.1, 0.1, 0.1), 0.8);

                // 2. Main Light
                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));

                // Diffuse
                float3 mainDiffuse = baseColor * mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * NdotL);

                // Specular
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(Smoothness * 10.0) + 1.0;
                float3 mainSpecular = pow(NdotH, specPower) * mainLight.color * mainLight.shadowAttenuation * mainLight.distanceAttenuation * Smoothness;

                // 3. Additional Lights
                float3 addDiffuse = 0;
                float3 addSpecular = 0;
                float3 scatteredLight = 0;

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    
                    float NdotLAdd = saturate(dot(normalWS, addLight.direction));
                    addDiffuse += baseColor * addLight.color * addLight.distanceAttenuation * NdotLAdd;
                    
                    float3 halfDirAdd = normalize(addLight.direction + viewDirWS);
                    float NdotHAdd = saturate(dot(normalWS, halfDirAdd));
                    addSpecular += pow(NdotHAdd, specPower) * addLight.color * addLight.distanceAttenuation * Smoothness;
                    
                    // Fake scattering
                    scatteredLight += addLight.color * addLight.distanceAttenuation;
                LIGHT_LOOP_END
                #endif

                // 4. Fake water scattering / transmission
                float waterThickness = max(sceneDepth2 - surfaceDepth, 0);
                float transmission = exp(-waterThickness * 0.15);
                float3 scattering = scatteredLight * transmission * 0.05;

                // 5. Сборка финального цвета
                float3 finalColor = ambient + mainDiffuse + mainSpecular + addDiffuse + addSpecular + scattering;

                float ao = SampleAmbientOcclusion(screenUV);
                ambient *= ao;
                finalColor *= ao;

                // 6. Дистанс фейд
                float dist = length(input.positionWS - _WorldSpaceCameraPos);
                float distFade = saturate((dist - 50.0) / 100.0);
                float lightIntensity = saturate(Luminance(bakedGI) + Luminance(mainLight.color));
                float3 deepColorAdjusted = DeepColor.rgb * max(lightIntensity, 0.05); // минимум 5% яркости
                finalColor = lerp(finalColor, deepColorAdjusted, distFade);

                // 8. Water color boost
                float lighting = saturate(Luminance(mainLight.color * mainLight.shadowAttenuation + scatteredLight));
                finalColor += waterColor * lighting * 0.5;

                // 9. Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                // 10. Alpha
                float waterAlpha = lerp(0.2, 0.9, depthFade);
                float alpha = lerp(0.5, waterAlpha, isFrontFace);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}