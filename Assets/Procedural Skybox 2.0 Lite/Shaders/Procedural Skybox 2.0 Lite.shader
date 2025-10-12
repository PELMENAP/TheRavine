Shader "Skybox/Procedural Skybox 2.0 Lite"
{
    Properties
    {
        _SunSize ("Sun Size", Range(0,1)) = 0.05
        _SunHaze ("Sun Haze", Range(0,0.5)) = 0.1
        _AtmosphereThickness ("Atmosphere Thickness", Range(0.1,5)) = 0.5
        _ZenithColor ("Zenith Color", Color)  = (0.3921568,0.7058823,1,1)
        _HorizonColor ("Horizon Color", Color) = (1,1,1,1)
        _GroundColor     ("Ground Color", Color) = (1,1,1,1)
        _SkyExposure ("Sky Exposure", Range(0,4)) = 1
        _HorizonHeight ("Horizon Height", Range(-0.5,0.5)) = 0
        _CloudTint ("Cloud Tint", Color) = (1,1,1,1)
        _CloudSeed ("Cloud Seed", Range(0,1000)) = 0
        _CloudRotationY ("Cloud Rotation Y", Range(0,360)) = 180
        _CloudCoverage ("Cloud Coverage", Range(0,1)) = 1.0
        _CloudSoftness  ("Cloud Softness", Range(0.2,1)) = 0.5
        _CloudScale ("Cloud Scale", Range(1,2)) = 2
        _CloudBaseHeight ("Cloud Base Height", Range(0,0.5)) = 0.25
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" }
        Cull Off
        Lighting On
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float  _SunSize;
            float  _SunHaze;
            float  _AtmosphereThickness;
            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            float  _SkyExposure;
            float  _HorizonHeight;
            fixed4 _CloudTint;
            float  _CloudSeed;
            float _CloudRotationY;
            float  _CloudCoverage;
            float _CloudSoftness;
            float  _CloudScale;
            float  _CloudBaseHeight;

            static const float LITE_SUN_CONVERGENCE   = 4.5;
            static const float LITE_CLOUD_LIGHT_INT   = 1.0;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 ProcessSky(float3 dir)
            {
                float y = saturate(dir.y * 0.5 + 0.5) * 2.0 - 1.0;
                float yShift = y - _HorizonHeight;

                float ySky    = saturate(yShift);
                float yGround = saturate(-yShift);

                float tSky    = pow(ySky, _AtmosphereThickness);
                float tGround = yGround;

                fixed3 top    = lerp(_HorizonColor.rgb, _ZenithColor.rgb, tSky);
                fixed3 bottom = lerp(_GroundColor.rgb,   _HorizonColor.rgb, tGround);

                float blend = smoothstep(-0.02, 0.02, yShift);
                fixed3 col = lerp(bottom, top, blend);
                return fixed4(col, 1);
            }

            fixed DiscFade_HQ(float d, float s)
            {
                float conv = LITE_SUN_CONVERGENCE;
                float edge = max(s / (conv * 6.0), 1e-4);
                return 1.0 - smoothstep(s - edge, s, d);
            }

            inline fixed3 ProcessSun(float3 dir)
            {
                if (_SunSize <= 0) return 0;

                float3 sDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 nDir = normalize(dir);

                float d = length(nDir - sDir);

                float core = DiscFade_HQ(d, _SunSize);
                float u    = saturate(d / max(_SunSize, 1e-4));
                float limb = pow(1.0 - u, 0.35);
                core *= limb;

                float hazeR = max(_SunSize * lerp(1.5, 5.0, saturate(_SunHaze * 2.0)), _SunSize + 1e-4);
                float x = max(d - _SunSize, 0.0) / (hazeR - _SunSize);
                float halo = exp2(-4.0 * x * x);

                fixed3 L = GammaToLinearSpace(_LightColor0.rgb);
                fixed3 coreCol = L * fixed3(1.08, 0.95, 0.90);
                fixed3 haloCol = L * fixed3(1.00, 0.92, 0.85);

                float hazeStrength = lerp(0.5, 3.0, saturate(_SunHaze * 2.0));
                fixed3 col = coreCol * core + haloCol * halo * hazeStrength;

                return saturate(col);
            }

            float  hash31_fast(float3 p)
            {
                p += _CloudSeed;
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33_fast(float3 p)
            {
                p += _CloudSeed;
                p = frac(p * 0.1031);
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yzz) * p.zyx);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = hash31_fast(i + float3(0,0,0));
                float n100 = hash31_fast(i + float3(1,0,0));
                float n010 = hash31_fast(i + float3(0,1,0));
                float n110 = hash31_fast(i + float3(1,1,0));
                float n001 = hash31_fast(i + float3(0,0,1));
                float n101 = hash31_fast(i + float3(1,0,1));
                float n011 = hash31_fast(i + float3(0,1,1));
                float n111 = hash31_fast(i + float3(1,1,1));

                return lerp(
                    lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                    lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y),
                    u.z
                );
            }

            float fbm3(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll] for (int i = 0; i < 4; ++i)
                {
                    v += noise3(p) * a;
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            float sphereFalloff(float d, float s)
            {
                float k = saturate((1.0 - d) / max(s, 1e-3));
                return k * k * (3.0 - 2.0 * k);
            }

            float blobDensity(float3 localPos, float3 cellSeed)
            {
                float s = sphereFalloff(length(localPos), _CloudSoftness);
                float n = fbm3(localPos * 2.0 + cellSeed * 5.37);
                n = saturate((n - 0.3) * 2.0);
                return s * n;
            }

            fixed4 ProcessClouds(float3 dir, fixed3 skyCol)
            {
                float3 nDir = normalize(dir);

                float rad = radians(_CloudRotationY);
                float s = sin(rad), c = cos(rad);
                float3 rotDir = float3(nDir.x * c - nDir.z * s, nDir.y, nDir.x * s + nDir.z * c);

                float baseOffset = rotDir.y - _CloudBaseHeight;
                if (baseOffset <= 0.0) return fixed4(skyCol, 0);

                float3 rdir = normalize(rotDir);
                float3 pos  = float3(rotDir.x, baseOffset, rotDir.z) * _CloudScale;

                float2 camXZ = _WorldSpaceCameraPos.xz;
                pos.xz += camXZ * (0.001 * _CloudScale);

                const float cellSize = 1.5;
                const int   LAYERS   = 3;
                const float STEP     = 0.6;

                float density = 0.0;

                [loop]
                for (int l = 0; l < LAYERS; ++l)
                {
                    float3 lp = pos + rdir * ((l - 1) * STEP);

                    float3 gPos = lp / cellSize;
                    float3 b    = floor(gPos);
                    float3 f    = gPos - b;

                    const float maxLayerOffset = STEP;
                    const float planeMargin    = max(0.12, maxLayerOffset / cellSize * 0.55);

                    int3 minC = (int3)b - int3(1,1,1);
                    int3 maxC = (int3)b + int3(1,1,1);

                    if (f.x < planeMargin) minC.x -= 1; else if (f.x > 1.0 - planeMargin) maxC.x += 1;
                    if (f.y < planeMargin) minC.y -= 1; else if (f.y > 1.0 - planeMargin) maxC.y += 1;
                    if (f.z < planeMargin) minC.z -= 1; else if (f.z > 1.0 - planeMargin) maxC.z += 1;

                    float d = 0.0;
                    const float maxR  = (1.0 + _CloudSoftness);
                    const float maxR2 = maxR * maxR;

                    for (int ix = minC.x; ix <= maxC.x; ++ix)
                    for (int iy = minC.y; iy <= maxC.y; ++iy)
                    for (int iz = minC.z; iz <= maxC.z; ++iz)
                    {
                        float3 cell = float3(ix, iy, iz);
                        float3 randOffset = hash33_fast(cell) - 0.5;
                        float3 centre = (cell + randOffset) * cellSize;
                        float3 dv = lp - centre;

                        if (dot(dv, dv) > maxR2) continue;
                        d += blobDensity(dv, cell);
                    }
                    density += d;
                }

                density = saturate(density / LAYERS);
                density = saturate(density - (1.0 - _CloudCoverage));

                float heightFade = saturate(baseOffset / 0.15);
                density *= heightFade;

                float densOver = density;

                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float  mu     = saturate(dot(nDir, sunDir));

                fixed3 lightCol   = GammaToLinearSpace(_LightColor0.rgb);
                float  sunVisible = saturate(sunDir.y * 0.5 + 0.5);

                float g = lerp(0.3, 0.78, sunVisible);
                float denom = 1.0 + g * g - 2.0 * g * mu;
                float phase = (1.0 - g * g) / max(pow(denom, 1.5), 1e-3);

                fixed3 tint = _CloudTint.rgb;
                fixed3 tintedLight = lightCol * tint;

                float  transV = exp(-densOver * 1.35);
                fixed3 scatter = tintedLight * phase * LITE_CLOUD_LIGHT_INT * (1.0 - transV) * sunVisible;

                float  silver = pow(saturate(1.0 - densOver), 3.0) * pow(mu, 8.0) * (LITE_CLOUD_LIGHT_INT * 0.35) * sunVisible;
                fixed3 silverCol = tintedLight * silver;

                fixed3 outCol = lerp(skyCol, tint, densOver);
                outCol = saturate(outCol + scatter * densOver + silverCol);

                return fixed4(outCol, densOver);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 sky = ProcessSky(i.dir).rgb;

                fixed4 cloud = ProcessClouds(i.dir, sky);
                fixed3 result = lerp(sky, cloud.rgb, cloud.a);

                result += ProcessSun(i.dir) * (1.0 - cloud.a);
                result *= _SkyExposure;

                return fixed4(saturate(result), 1);
            }
            ENDCG
        }
    }
    FallBack Off
}