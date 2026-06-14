Shader "Custom/WaterShader_Improved"
{
    Properties
    {
        [HDR] ShallowColor("Shallow Color", Color) = (0.4, 0.6, 0.8, 1)
        [HDR] DeepColor("Deep Color", Color) = (0.05, 0.15, 0.35, 1)
        [HDR] FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        
        Smoothness("Smoothness", Range(0, 1)) = 0.9
        Metallic("Metallic", Range(0, 1)) = 0.0
        
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
        
        RippleStrength("Ripple Strength", Float) = 0.5
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
            ZWrite on
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
            
            // ----------------------------------------------------------------
            // Textures & Samplers
            // ----------------------------------------------------------------
            TEXTURE2D(NormalMap);       SAMPLER(samplerNormalMap);
            TEXTURE2D(FoamTexture);     SAMPLER(samplerFoamTexture);
            TEXTURE2D(_RippleTex);      SAMPLER(sampler_RippleTex);
            TEXTURE2D(_CameraOpaqueTexture);    SAMPLER(sampler_CameraOpaqueTexture);
            TEXTURE2D(_CameraDepthTexture);     SAMPLER(sampler_CameraDepthTexture);
            
            // ----------------------------------------------------------------
            // CBUFFER
            // ----------------------------------------------------------------
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
                float DepthFade;
                float Foam;
                float4 FoamTexture_TexelSize;
                float FoamScale;
                float FoamShoreStrength;
                float FoamCrestStrength;
                float FoamRippleStrength;
                float RippleStrength;
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
                float3 waveNormal : TEXCOORD8;
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                    FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
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
                
                // === VERTEX DISPLACEMENT ===
                // Split ObjectSpacePosition
                float posX = input.positionOS.x;
                float posY = input.positionOS.y;
                float posZ = input.positionOS.z;
                
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

                float3 waveNormal =
                    normalize(float3(
                        -(wave1.x + wave2.x + wave3.x),
                        1,
                        -(wave1.z + wave2.z + wave3.z)
                    ));
                                
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
                float3 rippleNormalForRefract  = LowResNormalFromHeight(_RippleTex, sampler_RippleTex, input.uv0, float2(512,512), RippleRefraction);
                float3 rippleNormalForSurface  = LowResNormalFromHeight(_RippleTex, sampler_RippleTex, input.uv0, float2(512,512), RippleStrength);
                 float rippleHeight = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, input.uv0).r;
                float3 finalNormalTS_refract  = lerp(normalTS, rippleNormalForRefract,  rippleHeight);
                float3 finalNormalTS_surface  = lerp(normalTS, rippleNormalForSurface, rippleHeight);

                
                // === REFRACTION (двойное искажение с depth check) ===
                // Нормали * Refraction
                float3 refractOffset = Refraction * finalNormalTS_refract;
                
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
                float3 normalWS = TransformTangentToWorld(finalNormalTS_surface, tangentToWorld);
                normalWS = NormalizeNormalPerPixel(normalWS);
                
                // === FOAM ===
                // Foam texture
                float2 foamUV =
                    input.positionWS.xz *
                    FoamScale +
                    _Time.y * float2(0.03, 0.01);
                float4 foamTex = SAMPLE_TEXTURE2D(FoamTexture, samplerFoamTexture, foamUV);
                float4 foamTex2 =
                    SAMPLE_TEXTURE2D(
                        FoamTexture,
                        samplerFoamTexture,
                        foamUV * 2.7 +
                        _Time.y * float2(-0.02, 0.04));
                
                foamTex =
                    lerp(
                        foamTex,
                        foamTex2,
                        0.5);
                
                // Depth-based foam mask
                float foamDepth = SceneDepth_Eye(screenUV) - surfaceDepth;
                float foamFade = saturate(foamDepth / Foam);

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
                half3 reflColor = GlossyEnvironmentReflection(reflectDir, input.positionWS,  1.0 - Smoothness, 1.0, screenUV);

                // === PBR LIGHTING ===
                // Для back face: Smoothness = 0
                float smoothness = lerp(0, Smoothness, isFrontFace);
                
                // InputData для UniversalFragmentPBR
                InputData inputData;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = float4(0, 0, 0, 0);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = 1.0;
                
                SurfaceData surfaceData;
                
                surfaceData.albedo = baseColor;
                surfaceData.specular = 0.0;
                surfaceData.metallic = Metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = finalNormalTS_surface;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = float3(0, 0, 0);

                float waterAlpha =
                    lerp(
                        0.2,
                        0.9,
                        depthFade);

                surfaceData.alpha = waterAlpha;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;

                float dist = length(input.positionWS - _WorldSpaceCameraPos);
                float distFade = saturate((dist - 50.0) / 100.0);
                surfaceData.albedo = lerp(surfaceData.albedo, DeepColor.rgb, distFade);
                surfaceData.smoothness = lerp(Smoothness, Smoothness * 0.7, distFade); // меньше бликов вдали

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                 // Alpha: back face = меньше
                float alpha = lerp(0.5, 1.0, isFrontFace);
                color.a = alpha;

                float3 scatteredLight = 0;

                float waterThickness =
                    max(
                        sceneDepth2 - surfaceDepth,
                        0);
                
                float transmission =
                    exp(
                        -waterThickness * 0.15);
                
                float3 sceneLighting = 0;

                Light mainLight =
                    GetMainLight(inputData.shadowCoord);

                sceneLighting +=
                    mainLight.color *
                    mainLight.shadowAttenuation;

                // === ADDITIONAL LIGHTS ===
                uint additionalLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    half3 halfDirAdd = normalize(addLight.direction + viewDirWS);
                    half3 specAdd = pow(saturate(dot(normalWS, halfDirAdd)), Smoothness * 128.0) *
                        addLight.color * addLight.distanceAttenuation * Smoothness;
                    color.rgb += specAdd * isFrontFace;

                    scatteredLight +=
                        addLight.color *
                        addLight.distanceAttenuation *
                        transmission;

                    sceneLighting +=
                        addLight.color *
                        addLight.distanceAttenuation;

                LIGHT_LOOP_END
                
                waterColor +=
                    scatteredLight *
                    0.05;

                color.rgb = MixFog(color.rgb, input.fogFactor);

                float lighting =
                    saturate(
                        Luminance(sceneLighting));

                color.rgb +=
                    waterColor *
                    lighting;
                
                float F0 = 0.02;

                float fresnel =
                    F0 + (1.0 - F0) *
                    pow(1.0 - saturate(dot(viewDirWS, normalWS)), 5.0);

                color.rgb += reflColor * fresnel;

                return color;
            }
            ENDHLSL
        }
    }
}