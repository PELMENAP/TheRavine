Shader "TheRavine/Menu/BoidsRender"
{
    Properties
    {
        _ColorA ("Slow Color", Color) = (0.1, 0.45, 0.9, 1)
        _ColorB ("Fast Color", Color) = (0.95, 0.35, 0.7, 1)
        _FogColor ("Fade Color", Color) = (0.02, 0.03, 0.08, 1)
        _Rim ("Wing Tip Glow", Range(0, 1)) = 0.25
        _Scale ("Scale", Float) = 0.35
        _FlapSpeed ("Flap Speed", Float) = 9
        _FlapAmp ("Flap Amplitude", Float) = 0.5
        _BankGain ("Bank Gain", Float) = 0.12
        _SpeedRange ("Speed Range", Vector) = (4, 9, 0, 0)
        _FadeRange ("Fade Range", Vector) = (30, 110, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Boid
            {
                float3 position;
                float phase;
                float3 velocity;
                float bank;
            };

            StructuredBuffer<Boid> _Boids;
            float3 _Center;
            float _Tm;

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _FogColor;
                float4 _SpeedRange;
                float4 _FadeRange;
                float _Rim;
                float _Scale;
                float _FlapSpeed;
                float _FlapAmp;
                float _BankGain;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 color : TEXCOORD1;
            };

            float BoidHash(uint n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            Varyings vert(Attributes v, uint id : SV_InstanceID)
            {
                Boid b = _Boids[id];
                float speed = length(b.velocity);
                float3 f = b.velocity / max(speed, 1e-4);
                float3 refUp = abs(f.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 r = normalize(cross(refUp, f));
                float3 u = cross(f, r);

                float sn, cs;
                sincos(b.bank * _BankGain, sn, cs);
                float3 r2 = r * cs + u * sn;
                float3 u2 = cross(f, r2);

                float flap = sin(_Tm * _FlapSpeed * (0.6 + speed * 0.12) + b.phase) * _FlapAmp * v.uv.x;
                float3 local = v.positionOS * _Scale;
                local.y += flap * _Scale;

                float3 world = _Center + b.position + r2 * local.x + u2 * local.y + f * local.z;

                Varyings o;
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = normalize(r2 * v.normalOS.x + u2 * v.normalOS.y + f * v.normalOS.z);

                float t = saturate((speed - _SpeedRange.x) / max(_SpeedRange.y - _SpeedRange.x, 1e-3));
                float h = BoidHash(id);
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(t + (h - 0.5) * 0.35));
                col *= lerp(0.8, 1.2, h);
                col += v.uv.x * _Rim * col;

                float fade = saturate((o.positionCS.w - _FadeRange.x) / max(_FadeRange.y - _FadeRange.x, 1e-3));
                o.color = lerp(col, _FogColor.rgb, fade);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light l = GetMainLight();
                float ndl = abs(dot(normalize(i.normalWS), l.direction));
                float3 lit = i.color * (0.45 + 0.55 * ndl);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Boid
            {
                float3 position;
                float phase;
                float3 velocity;
                float bank;
            };

            StructuredBuffer<Boid> _Boids;
            float3 _Center;
            float _Tm;

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _FogColor;
                float4 _SpeedRange;
                float4 _FadeRange;
                float _Rim;
                float _Scale;
                float _FlapSpeed;
                float _FlapAmp;
                float _BankGain;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsDepth vertDepth(Attributes v, uint id : SV_InstanceID)
            {
                Boid b = _Boids[id];
                float speed = length(b.velocity);
                float3 f = b.velocity / max(speed, 1e-4);
                float3 refUp = abs(f.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 r = normalize(cross(refUp, f));
                float3 u = cross(f, r);

                float sn, cs;
                sincos(b.bank * _BankGain, sn, cs);
                float3 r2 = r * cs + u * sn;
                float3 u2 = cross(f, r2);

                float flap = sin(_Tm * _FlapSpeed * (0.6 + speed * 0.12) + b.phase) * _FlapAmp * v.uv.x;
                float3 local = v.positionOS * _Scale;
                local.y += flap * _Scale;

                float3 world = _Center + b.position + r2 * local.x + u2 * local.y + f * local.z;

                VaryingsDepth o;
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }

            half4 fragDepth(VaryingsDepth i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}