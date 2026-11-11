Shader "The Ravine/Skybox/Clouds Optimized"
{
    Properties 
    {
        _NoiseTex ("Noise", 2D) = "black" {}
        _HazeColor ("Color", Color) = (1,1,1,1)
        _HazeScale ("Scale", Range(0,1)) = 0.2
        _HazeThreshold("Threshold", Range(0,1)) = 0
        _HazeIntensity("Intensity", Float) = 1.0
        _HazeRotationAxis("Rotation Axis", Vector) = (0.0,-1.0,0.0)
        _HazeRotationSpeed("Rotation Speed", Float) = 0.2

        [Space(20)]
        [NoScaleOffset] _Tex1 ("Cubemap", Cube) = "black" {}
        _AlphaLayer1 ("Alpha", Range(0,1)) = 1.0
        _HShiftLayer1 ("Horizontal Shift", Range(-7, 7)) = 0
        _RotationSpeed1 ("Horizontal Speed", Float) = 0.05
        _VShiftLayer1 ("Vertical Shift", Range(-0.5,0.25)) = 0
        _PhaseLayer1 ("Vertical Motion", Range(-0.1,0.1)) = 0.01

        [Space(20)]
        [NoScaleOffset] _Tex2 ("Cubemap", Cube) = "black" {}
        _AlphaLayer2 ("Alpha", Range(0,1)) = 1.0
        _HShiftLayer2 ("Horizontal Shift", Range(-7, 7)) = 0
        _RotationSpeed2 ("Horizontal Speed", Float) = 0.08
        _VShiftLayer2 ("Vertical Shift", Range(-0.5,0.25)) = 0
        _PhaseLayer2 ("Vertical Motion", Range(-0.1,0.1)) = -0.015

        [Space(20)]
        [NoScaleOffset] _Tex3 ("Cubemap", Cube) = "black" {}
        _AlphaLayer3 ("Alpha", Range(0,1)) = 1.0
        _HShiftLayer3 ("Horizontal Shift", Range(-7, 7)) = 0
        _RotationSpeed3 ("Horizontal Speed", Float) = 0.11
        _VShiftLayer3 ("Vertical Shift", Range(-0.5,0.25)) = 0
        _PhaseLayer3 ("Vertical Motion", Range(-0.1,0.1)) = 0.02

        [Space(20)]
        [Header(Sun Properties)]
        _SunColor ("Sun Tint", Color) = (1,0.9568,0.8392)
        _SunSize ("Sun Size", Range(0,1)) = 0.15
        _SunFlare("Sun Flare", Range(0, 1.0)) = 0.4
		_MieStrength("Mie Strength", Range(0,5)) = 1.0
		_MieAnisotropy("Mie Anisotropy", Range(0,0.99)) = 0.76
		_AtmosphereDensity("Atmosphere Density", Range(0,10)) = 3.0

        [Space(20)]
        [Header(Night and Moon Properties)]
        _MoonDirection("Moon Direction", Vector) = (0,0,0,0)
        [NoScaleOffset] _MoonTex ("Moon Texture", 2D) = "black" {}
        _MoonSize ("Moon Size Factor", Float) = 5
        _MoonFlare("Moon Flare", Range(0, 1.0)) = 0.03
        _StarSize ("Star Size", Range(10,1000)) = 200
        _StarDensity("Star Density", Range(0,0.35)) = 0.04
        _StarBrightness ("Star Brightness", Range(0, 1)) = 0.5
        _StarBlinking("Star Blinking", Range(0, 1)) = 0.2
        _NightBlend ("Night Blend", Range(0,1)) = 0
        _StarDarkColor("Star Darker Color", Color) = (1.0,0.3,0.1)
        _StarBrightColor("Star Brighter Color", Color) = (0.2,0.9,1.0)

        [Space(20)]
        [Header(Sky Properties)]
        _SkyTint ("Sky Tint", Color) = (0.52,0.5,1)
        _SkyTintUpward ("Sky Tint Upward", Color) = (0.2,0.4,1)
        _SkyNightColor ("Sky Night Color", Color) = (0,0,0)
        _NightTintColor ("Night Tint Color", Color) = (0,0,0.0002)
        _FogColor ("Fog Color", Color) = (0.8,0.8,0.8)
        _FogBaseHeight("Fog Base Height", Range(-1, 1)) = 0
        _FogHeight("Fog Height", Range(0.1, 100)) = 1
        _FogPower("Fog Power", Range(0.1, 100)) = 3.0
        _FogMinAmount ("Fog Min Amount", Range(0, 1)) = 0
        _FogAmount ("Fog Amount Multiplier", Range(0, 1)) = 1.0
        _FogDaylightInfluence("Fog Daylight Influence", Range(0, 1)) = 1
        _FogAlpha("Fog Alpha", Range(0, 1)) = 1
        _Exposure ("Exposure", Float) = 1.0
    }

    SubShader 
    {
        Tags 
        { 
            "Queue"="Background" 
            "RenderType"="Background" 
            "PreviewType"="Skybox" 
        }

        Cull Off ZWrite Off

        Pass 
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _AlphaLayer1;
                half _AlphaLayer2;
                half _AlphaLayer3;
                float _RotationSpeed1;
                float _RotationSpeed2;
                float _RotationSpeed3;
                float _VShiftLayer1;
                float _VShiftLayer2;
                float _VShiftLayer3;
                float _PhaseLayer1;
                float _PhaseLayer2;
                float _PhaseLayer3;
                float _HShiftLayer1;
                float _HShiftLayer2;
                float _HShiftLayer3;
                half3 _SkyTint;
                half3 _SkyTintUpward;
                half3 _SkyNightColor;
                half3 _NightTintColor;
                half _Exposure;
                half _FogMinAmount;
                half _FogHeight;
                half _FogAmount;
                half _FogBaseHeight;
                half _FogPower;
                half _FogDaylightInfluence;
                half _FogAlpha;
                half3 _FogColor;
                float _StarSize;
                float _StarDensity;
                float _StarBrightness;
                float _StarBlinking;
                half _NightBlend;
                half3 _StarDarkColor;
                half3 _StarBrightColor;
                half _SunSize;
                half _SunFlare;
                half3 _SunColor;
				half _MieStrength;
				half _MieAnisotropy;
				half _AtmosphereDensity;
                float4 _MoonDirection;
                float _MoonSize;
                half _MoonFlare;
                half _HazeScale;
                half4 _HazeColor;
                half _HazeIntensity;
                half _HazeThreshold;
                float3 _HazeRotationAxis;
                float _HazeRotationSpeed;
            CBUFFER_END

            TEXTURECUBE(_Tex1); SAMPLER(sampler_Tex1);
            TEXTURECUBE(_Tex2); SAMPLER(sampler_Tex2);
            TEXTURECUBE(_Tex3); SAMPLER(sampler_Tex3);
            TEXTURE2D(_MoonTex); SAMPLER(sampler_MoonTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float3 uvLayer1: TEXCOORD0;
                float3 uvLayer2: TEXCOORD1;
                float3 uvLayer3: TEXCOORD2;
                float3 ray : TEXCOORD4;
                float3 hazeRay : TEXCOORD6;

            };

            float3 RotateAroundY(float3 v, float angle) 
            {
                float s, c;
                sincos(angle, s, c);
                return float3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
            }

            float3 RotateAroundAxis(float3 v, float3 axis, float angle) 
            {
                float s, c;
                sincos(angle, s, c);
                return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

			float2 DirectionalOffset(float3 ray, float3 dir)
            {
                float3 up = abs(dir.y) > 0.999 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(up, dir));
                float3 realUp = cross(dir, right);
                return float2(dot(ray, right), dot(ray, realUp));
            }

            float3 GetMoonDirection(float3 sunDir)
            {
                return (dot(_MoonDirection.xyz, _MoonDirection.xyz) < 0.0001)
                    ? -sunDir
                    : normalize(_MoonDirection.xyz);
            }

            half3 ACESFilm(half3 x)
            {
                half a = 2.51h;
                half b = 0.03h;
                half c = 2.43h;
                half d = 0.59h;
                half e = 0.14h;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

			float MiePhase(float mu, float g) 
            {
                float gg = g * g;
                
                float denom_base = 1.0 + gg - 2.0 * g * mu;
                float rsub = rsqrt(denom_base);
                return (0.07957747154 * (1.0 - gg)) * (rsub * rsub * rsub);
            }


            inline half3 BlendClouds(half3 color, TextureCube cube, SamplerState samplerState, float3 uv, half alpha, half3 skyColor) 
            {
                half4 tex = SAMPLE_TEXTURECUBE(cube, samplerState, uv);
                tex.rgb *= skyColor;
                tex.a *= alpha;
                return lerp(color, tex.rgb, tex.a);
            }

            v2f vert (appdata v) 
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.ray = v.vertex.xyz;
                
				o.uvLayer1 = RotateAroundY(v.vertex.xyz, _Time.x * _RotationSpeed1 + _HShiftLayer1);
				o.uvLayer1.y += _SinTime.x * _PhaseLayer1 - _VShiftLayer1;

				o.uvLayer2 = RotateAroundY(v.vertex.xyz, _Time.x * _RotationSpeed2 + _HShiftLayer2);
				o.uvLayer2.y += _SinTime.x * _PhaseLayer2 - _VShiftLayer2;

				o.uvLayer3 = RotateAroundY(v.vertex.xyz, _Time.x * _RotationSpeed3 + _HShiftLayer3);
				o.uvLayer3.y += _SinTime.x * _PhaseLayer3 - _VShiftLayer3;
                
                o.hazeRay = RotateAroundAxis(v.vertex.xyz, _HazeRotationAxis, _Time.x * _HazeRotationSpeed);

                return o;
            }

            half4 frag (v2f i) : SV_Target 
            {
                float3 ray = normalize(i.ray);
                float3 sunDir = normalize(_MainLightPosition.xyz);

                float cosTheta = dot(sunDir, ray);
                
                half xSun = pow(
					saturate(cosTheta),
					512.0h
				);
                half sunFlare = _SunFlare * xSun;

                half3 skyColor = lerp(
					_SkyTint,
					_SkyTintUpward,
					saturate(ray.y)
				);

				float mu = dot(ray, sunDir);
				float horizon = 1.0 - saturate(ray.y);
				float atmosphereDepth = pow(horizon, _AtmosphereDensity);

				float miePhase = MiePhase(mu, _MieAnisotropy);
				half3 mieColor =
					_SunColor *
					miePhase *
					atmosphereDepth *
					_MieStrength;

				skyColor += mieColor;

                float y = ray.y;
                
                half fog = saturate(exp(_FogPower * (-y / _FogHeight + _FogBaseHeight)) * _FogAmount + _FogMinAmount);
                fog *= _FogAlpha;

                float hy = abs(sunDir.y) + abs(y);
                half t = saturate((0.25 - sunDir.y) * 4.0);
				half3 sunsetColor = half3(1.0, 0.45, 0.15);

				skyColor =
					lerp(
						skyColor,
						sunsetColor,
						t * atmosphereDepth
					);

				half3 ozoneColor = half3(0.45, 0.25, 0.8);
				float twilight = saturate(-sunDir.y * 5.0);

				skyColor +=
					ozoneColor *
					twilight *
					atmosphereDepth *
					1.15;

                half daylight = saturate(1.0 + sunDir.y * 2.0 - (1.0 - cosTheta) * 0.06);

                half nightBlend = _NightBlend;
                half dayBlend   = 1.0h - nightBlend;
                skyColor = lerp(_SkyNightColor, skyColor, dayBlend);

                half sunDist = sqrt(saturate(2.0 - 2.0 * cosTheta));
                half sunIntensity = 1.0 - smoothstep(0.0, _SunSize, sunDist);

                sunIntensity *= 1.5;
                sunIntensity *= sunIntensity;
                half3 sunColor = _SunColor;

                sunColor *= sunIntensity;
                half3 moonColor = (half3)0.0;
                half3 starColor = (half3)0.0;

                sunColor     *= dayBlend;
                half3 sunFlareColor = sunColor * sunFlare * dayBlend;

				if (nightBlend > 0.05)
				{
					float3 moonDir = GetMoonDirection(sunDir);
					
					float moonCosTheta = dot(moonDir, ray);
					half xMoon = pow(saturate(moonCosTheta), 256.0h);
					half moonFlare = _MoonFlare * xMoon;
					
					float moonDistSq = 2.0 - 2.0 * moonCosTheta;

					if (moonDistSq < 0.3) 
					{
						moonColor = (half3)moonFlare;
						float2 moonUV = DirectionalOffset(ray, moonDir);
						half3 moonTex = SAMPLE_TEXTURE2D_LOD(_MoonTex, sampler_MoonTex, moonUV * _MoonSize + 0.5, 0).rgb;
						moonColor += moonTex;
						sunColor = (half3)0.0;
					}


					float3 p = ray * _StarSize;
					float br = smoothstep(1.0 - _StarDensity, 1.0, hash13(floor(p)));
					float3 f = frac(p) - 0.5;
					float sqDist = dot(f, f);
					float star = smoothstep(_StarBrightness * _StarBrightness, 0.0, sqDist) * br;
					star *= saturate(1.0 - saturate(frac(br * 10000.0 + _Time.w) - 0.3) * _StarBlinking);
					star = saturate(star * (1.0 - fog) - moonFlare * 25.0 - dayBlend * dayBlend * 64.0);

					starColor = star * lerp(_StarDarkColor, _StarBrightColor, br);
					starColor *= starColor;
				}

                half sunVisibility = smoothstep(0.0, 0.15, daylight);
                sunColor *= sunVisibility;
                sunFlareColor *= sunVisibility;

                half3 col = skyColor + sunColor + sunFlareColor * 0.5 + (moonColor + starColor) * saturate(ray.y + 0.2);

                half3 skyTint = half3(1.0, 1.0, 1.0 - t);
                skyTint = lerp(_NightTintColor, skyTint, dayBlend);
                
				float3 hazeRay = normalize(i.hazeRay);
				float3 hazeUV = hazeRay * _HazeScale;
				half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, hazeUV.zy).r;
				half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, hazeUV.xz).r;
				half n3 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, hazeUV.xy).r;
				float3 triW = abs(hazeRay);
				float invSum = rcp(triW.x + triW.y + triW.z);
				float3 weights = triW * invSum;
				half haze = dot(half3(n1, n2, n3), weights);
				haze = saturate(haze * _HazeIntensity - _HazeThreshold);
				half3 hazeColor = _HazeColor.rgb;
				haze *= _HazeColor.a;
				col = lerp(col, hazeColor * skyTint, haze);

                if(_AlphaLayer1 > 0) col = BlendClouds(col, _Tex1, sampler_Tex1, i.uvLayer1, _AlphaLayer1, skyTint);
                if(_AlphaLayer2 > 0) col = BlendClouds(col, _Tex2, sampler_Tex2, i.uvLayer2, _AlphaLayer2, skyTint);
				if(_AlphaLayer3 > 0) col = BlendClouds(col, _Tex3, sampler_Tex3, i.uvLayer3, _AlphaLayer3, skyTint);

                half3 fogColor = _FogColor;
                half3 fogSkyTint = lerp(1.0, skyTint, _FogDaylightInfluence);
                col = lerp(col, fogColor * fogSkyTint, fog);

                col = ACESFilm(col * _Exposure);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}