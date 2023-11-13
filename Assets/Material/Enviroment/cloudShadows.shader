Shader "Custom/cloudShadows"
{

    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _PerlinNoise ("Albedo (R)", 2D) = "white" {}
        _WorleyNoise ("Albedo (R)", 2D) = "white" {}
        
        _tiling("tiling",vector) = (1,1.5,0,0)
        _scale("scale",float) = 0.6
        _alpha("alpha",float) = 0.9
        _cloudAlphaMax("cloudAlphaMax",float) = 0.62

        _speed1("speed1",float) = 10
        _speed2("speed2",float) = 6
        _cloudsDir1("cloudsDir1",vector) = (1.0,0.0,0,0)
        _cloudsDir2("cloudsDir2",vector) = (1.0,0.2,0,0)

        _cloudColor("cloudColor",Color) = (0,0,0,0)
        _step("smoothShading",int) = 0

        _sunDirection("sunDirection",vector) = (1.0,-1.0,1.0,0)

        _fastMode("fastMode",int) = 0
        _cloudMode("cloudsMode",int) = 2

        _ma1("ma1",vector) = (1.6,-1.2,0,0)
        _ma2("ma2",vector) = (1.2,1.6,0,0)
    }
    SubShader
    {
        //universal render pipeline options
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
             Tags { "LightMode" = "Universal2D" }
 
             HLSLPROGRAM

             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
             
             //variables
             sampler2D _PerlinNoise;
             sampler2D _WorleyNoise;
             
             uniform float2 _tiling;
             uniform float _scale;
             uniform float _alpha;
             uniform float _cloudAlphaMax;

             uniform float _speed1;
             uniform float _speed2;

             uniform float2 _cloudsDir1;
             uniform float2 _cloudsDir2;

             uniform float3 _cloudColor;
             uniform float3 _sunDirection;

             uniform float2 _ma1;
             uniform float2 _ma2;

             uniform float2 _pos;
             uniform float _scrollSpeed;


             uniform int _step;
             uniform int _fastMode;
             uniform int _cloudMode;

             //utils

             float2x2 _mat()
             {
                return float2x2(_ma1.x,_ma1.y,_ma2.x,_ma2.y);
             }

             float3 frac3(float3 f)
             {
                return float3(frac(f.x),frac(f.y),frac(f.z));
             }

             float2 frac2(float2 f)
             {
                return float2(frac(f.x),frac(f.y));
             }

             //hashes

             float2 hash21( float2 p ) 
             {
	            p = float2(dot(p,float2(127.1,311.7)), dot(p,float2(269.5,183.3)));
	            return -1.0 + 2.0*frac(sin(p)*43758.5453123);
             }

            float2 hash22(float2 p) 
            {
                   const float3 HASHSCALE3 = float3(0.1031, 0.1030, 0.0973);
	               float3 p3 = frac3( float3(p.xyx) * HASHSCALE3 );
                   p3 += dot(p3, p3.yzx+19.19);
                   return frac2((p3.xx+p3.yz)*p3.zy);
            }

            float2 randomVec2(in float i, in float j) {
                return float2(i, j) + hash22(float2(i, j));   
            }

            //noises

            float fastNoise(in float2 p)
            {
                  return tex2D(_PerlinNoise,p/12).r / 2;
            }

            float noise( in float2 p ) 
            {
                const float K1 = 0.366025404;
                const float K2 = 0.211324865; 
	            float2 i = floor(p + (p.x+p.y)*K1);	
                float2 a = p - i + (i.x+i.y)*K2;
                float2 o = (a.x>a.y) ? float2(1.0,0.0) : float2(0.0,1.0); 
                float2 b = a - o + K2;
	            float2 c = a - 1.0 + 2.0*K2;
                float3 h = max(0.5-float3(dot(a,a), dot(b,b), dot(c,c) ), 0.0 );
	            float3 n = h*h*h*h*float3( dot(a,hash21(i+0.0)), dot(b,hash21(i+o)), dot(c,hash21(i+1.0)));
                return dot(n, float3(70.0,70.0,70.0));	
            }

            float fbm(float2 n) 
            {
                float2x2 m = _mat();;
	            float total = 0.0, amplitude = 0.1;
	            for (int i = 0; i < 7; i++) {
		            total += noise(n) * amplitude;
		            n =  mul(m , n);
		            amplitude *= 0.4;
	            }
	            return total;
            }

            float fastWorley(in float2 p, float scale)
            {
                  return tex2D(_WorleyNoise,p/24/scale).r;
            }


            float worley(in float2 uv, float scale)
            {
                float2 ij = floor(uv / scale);
    
                float minDist = 2.0;
    
                for(float x=-1.0;x<=1.0;x+=1.0) {
                    for(float y=-1.0;y<=1.0;y+=1.0) {
        	            float d = length(randomVec2(ij.x+x, ij.y+y)*scale - uv);
                        minDist = min(minDist, d);
                    }
	            }

                return 1.0 - minDist / scale;
            }

            //time utils

            float cosTime(float a, float p) 
            {
	            return (cos(_Time.y*6.283/p) * 0.5 + 0.5) * a;
            }

            float cosTimeOffset(float a,float b, float p) 
            {
	            return (cos(_Time.y*6.283/p) * 0.5 + 0.5) * a;
            }

            float2 randDir(float2 id,float time)
            {
                id+=float2(3,181);
                float t=time+PI*2.*frac(sin(83.*id.x)*(id.y)*(414.-id.y*5.));
                return float2(cos(t),sin(t));
            }

            float pnoise(float2 uv, float t)
            {
                float2 id = floor(uv);uv = frac2(uv);
                float2 of = float2(1,0);
                float a=dot(uv-of.yy,randDir(id+of.yy,t)),
                      b=dot(uv-of.xy,randDir(id+of.xy,t)),
                      c=dot(uv-of.yx,randDir(id+of.yx,t)),
                      d=dot(uv-of.xx,randDir(id+of.xx,t));
                uv=uv*uv*(float2(3.0,3.0)-2.*uv);
                a=lerp(a,b,uv.x),b=lerp(c,d,uv.x);      
                return lerp(a,b,uv.y)/1.4+.5;
            }

            // cloud functions

            float gradientCloud(in float2 uv, float scale , float2 dir, float2 dir2, float speed1, float speed2)
            {
                float t1 = _Time.y * speed1 ;
                float t2 = _Time.y * speed2 ;

                //normalize directions of movement
                dir = normalize(dir);
                dir2 = normalize(dir2);

                //uvs with speed and direction
	            float2 uv1 = uv + float2(dir.x * 0.01 * t1,dir.y * 0.01 * t1);
	            float2 uv2 = uv + float2(dir2.x * 0.01 * t2,dir2.y * 0.01 * t2);    
                uv1 /= scale / 3;
                uv2 /= scale / 3;
                float p1 = pnoise( uv1 , t1/20);
                float p2 = pnoise( uv2 , t1/20);

                return  (p1+p2)*0.5; 
            }

            float lightCloudBrownian(in float2 uv, float scale , float2 dir, float2 dir2, float speed1, float speed2)
            {
                float2x2 m =  _mat();
                float t1 = _Time.y * speed1 ;
                float t2 = _Time.y * speed2 ;

                //normalize directions of movement
                dir = normalize(dir);
                dir2 = normalize(dir2);

                //uvs with speed and direction
	            float2 uv1 = uv + float2(dir.x * 0.01 * t1,dir.y * 0.01 * t1);
	            float2 uv2 = uv + float2(dir2.x * 0.01 * t2,dir2.y * 0.01 * t2);    
                float q = fbm(uv1 / scale * 0.5);

                return  q*100; 
            }

            float cloudBrownian(in float2 uv, float scale , float2 dir, float2 dir2, float speed1, float speed2)
            {
                float2x2 m = _mat();
                float t1 = _Time.y * speed1 ;
                float t2 = _Time.y * speed2 ;

                //normalize directions of movement
                dir = normalize(dir);
                dir2 = normalize(dir2);

                //uvs with speed and direction
	            float2 uv1 = uv + float2(dir.x * 0.01 * t1,dir.y * 0.01 * t1);
	            float2 uv2 = uv + float2(dir2.x * 0.01 * t2,dir2.y * 0.01 * t2);    

                float q = fbm(uv1 / scale * 0.5);
    
	            float f = 0.0;
                float w = 0.0;
                float r = 0.0;

                const float timeDiv = 0.0005;
                t1 *= timeDiv;
                t2 *= timeDiv;

                //uvs
	            uv2 /= scale;
                uv2 -= q - t2;

	            uv1 /= scale;
                uv1 -= q - t1;

                //sub-noise movement
                w = 0.8;

                for (int i=0; i<8; i++){
		            r += abs(w * (!_fastMode ? noise( uv2 ) : fastNoise(uv2)) );
                    uv2 = mul(m,uv2) + t2;
		            w *= 0.75;
                }

                //noise movement
                w = 0.7;

                for (int i=0; i<8; i++){
		            f += w*(!_fastMode ? noise( uv1 ) : fastNoise(uv1));
                    uv1 = mul(m,uv1) + t1;
		            w *= 0.6;
                }

                return  f = saturate(f*r*f*10) ; //f = r* f; //combining function
            }

            float cloudWorley(in float2 uv, float scale , float2 dir, float2 dir2, float speed1, float speed2)
            {
                float t1 = _Time.y * speed1;
                float t2 = _Time.y * speed2;

                //normalize directions of movement
                dir = normalize(dir);
                dir2 = normalize(dir2);

                //uvs with speed and direction
	            float2 uv1 = uv + float2(dir.x * 0.01 * t1,dir.y * 0.01 * t1);
	            float2 uv2 = uv + float2(dir2.x * 0.01 * t2,dir2.y * 0.01 * t2);    

                //constants for fine-tuning
                const float sw1 = 0.2;
                const float sw2 = 0.72;
                const float sw3 = 0.3;
                const float sw4 = 1.0;
  
                const float aw1 = 0.15;
                const float aw2 = 0.8;
                const float aw3 = 0.3;
                const float aw4 = 0.65;

                //worley functions, you can add more for bigger variety/more details, but it's preformance expensive
                const float col1 = (!_fastMode ? worley( uv1, sw1 * scale  ) : fastWorley(uv1, sw1 * scale ))  * aw1 +  cosTime(0.10, 9.0);
                const float col2 = (!_fastMode ? worley( uv1, sw2 * scale  ) : fastWorley(uv1, sw2 * scale ))  * aw2 +  cosTime(0.20, 9.0);
                const float col3 = (!_fastMode ? worley( uv2, sw3 * scale  ) : fastWorley(uv2, sw3 * scale ))  * aw3;
                const float col4 = (!_fastMode ? worley( uv2, sw4 * scale  ) : fastWorley(uv2, sw4 * scale ))  * aw4;

                const float layer1 = (col1 + col2) / (aw1 + aw2);
                const float layer2 = (col3 + col4) / (aw3 + aw4);    

                //average values
                return saturate( (layer1 + layer2) *0.5);
            }

            //clouds lighting functions


            float4 lightCloudBrownianNorm(in float2 uv, float scale, float3 sunDirection, float3 cloudCol, float cloudAlpha, float speed1, float speed2, float2 cloudsDir1, float2 cloudsDir2)
            {
                //base cloud val
                float result = lightCloudBrownian(uv,scale,cloudsDir1,cloudsDir2, speed1, speed2);   //  to work with different cloud function, replace cloud Worley.

                //pseudo light direction
                sunDirection = normalize(sunDirection); 
    
                //pseudo slope
                float slopeDelta = 0.15 / scale * 0.6; 
                float3 slope = float3(1, -1, 0) * slopeDelta;
    
                //dx dy normal
                float px2 = lightCloudBrownian(uv + slope.xz,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.
                float py2 = lightCloudBrownian(uv + slope.zx,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.

                //normal vector
                float3 pseudo_normal = normalize(float3(px2 - result, py2 - result, -0.1));

                //lighting
                float lighting = dot(pseudo_normal, -sunDirection); 
    
                //lerp
                return  lerp( float4(0,0,0,0), float4(cloudCol.xyz,cloudAlpha) * lighting, result);  
            }

            float4 gradientCloudNorm(in float2 uv, float scale, float3 sunDirection, float3 cloudCol, float cloudAlpha, float speed1, float speed2, float2 cloudsDir1, float2 cloudsDir2)
            {
                //base cloud val
                float result = gradientCloud(uv,scale,cloudsDir1,cloudsDir2, speed1, speed2);   //  to work with different cloud function, replace cloud Worley.

                //pseudo light direction
                sunDirection = normalize(sunDirection); 
    
                //pseudo slope
                float slopeDelta = 0.15 / scale * 0.6; 
                float3 slope = float3(1, -1, 0) * slopeDelta;
    
                //dx dy normal
                float px2 = gradientCloud(uv + slope.xz,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.
                float py2 = gradientCloud(uv + slope.zx,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.

                //normal vector
                float3 pseudo_normal = normalize(float3(px2 - result, py2 - result, -0.1));

                //lighting
                float lighting = dot(pseudo_normal, -sunDirection); 
    
                //lerp
                return  lerp( float4(0,0,0,0), float4(cloudCol.xyz,cloudAlpha) * lighting, result);    
            }

            float4 cloudWorleyNorm(in float2 uv, float scale, float3 sunDirection, float3 cloudCol, float cloudAlpha, float speed1, float speed2, float2 cloudsDir1, float2 cloudsDir2)
            {
                //base cloud val
                float result = cloudWorley(uv,scale,cloudsDir1,cloudsDir2, speed1, speed2);   //  to work with different cloud function, replace cloud Worley.

                //pseudo light direction
                sunDirection = normalize(sunDirection); 
    
                //pseudo slope
                float slopeDelta = 0.15 / scale * 0.6; 
                float3 slope = float3(1, -1, 0) * slopeDelta;
    
                //dx dy normal
                float px2 = cloudWorley(uv + slope.xz,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.
                float py2 = cloudWorley(uv + slope.zx,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.

                //normal vector
                float3 pseudo_normal = normalize(float3(px2 - result, py2 - result, -0.1));

                //lighting
                float lighting = dot(pseudo_normal, -sunDirection); 
    
                //lerp
                return  lerp( float4(0,0,0,0), float4(cloudCol.xyz,cloudAlpha) * lighting, result);    
            }
          
            //clouds lighting functions

            float4 cloudBrownianNorm(in float2 uv, float scale, float3 sunDirection, float3 cloudCol, float cloudAlpha, float speed1, float speed2, float2 cloudsDir1, float2 cloudsDir2)
            {
                //base cloud val
                float result = cloudBrownian(uv,scale,cloudsDir1,cloudsDir2, speed1, speed2);   //  to work with different cloud function, replace cloud Worley.

                //pseudo light direction
                sunDirection = normalize(sunDirection); 
    
                //pseudo slope
                float slopeDelta = 0.15 / scale * 0.6; 
                float3 slope = float3(1, -1, 0) * slopeDelta;
    
                //dx dy normal
                float px2 = cloudBrownian(uv + slope.xz,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.
                float py2 = cloudBrownian(uv + slope.zx,scale,cloudsDir1,cloudsDir2, speed1, speed2);     //  to work with different cloud function, replace cloud Worley.

                //normal vector
                float3 pseudo_normal = normalize(float3(px2 - result, py2 - result, -1));

                //lighting
                float lighting = dot(pseudo_normal, -sunDirection); 
    
                //lerp
                return  lerp( float4(0,0,0,0), float4(cloudCol.xyz,cloudAlpha) * lighting, result);    
            }

             #pragma vertex CombinedShapeLightVertex
             #pragma fragment CombinedShapeLightFragment
 
             #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
             #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
             #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
             #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
             #pragma multi_compile _ DEBUG_DISPLAY
 
             struct Attributes
             {
                 float3 positionOS   : POSITION;
                 float4 color        : COLOR;
                 float2  uv          : TEXCOORD0;
                 UNITY_VERTEX_INPUT_INSTANCE_ID
             };
 
             struct Varyings
             {
                 float4  positionCS  : SV_POSITION;
                 half4   color       : COLOR;
                 float2  uv          : TEXCOORD0;
                 half2   lightingUV  : TEXCOORD1;
                 #if defined(DEBUG_DISPLAY)
                 float3  positionWS  : TEXCOORD2;
                 #endif
                 float3 scale : TEXCOORD3;
                 UNITY_VERTEX_OUTPUT_STEREO
             };
 
             #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
 
             TEXTURE2D(_MainTex);
             SAMPLER(sampler_MainTex);
             TEXTURE2D(_MaskTex);
             SAMPLER(sampler_MaskTex);
             half4 _MainTex_ST;
 
             #if USE_SHAPE_LIGHT_TYPE_0
             SHAPE_LIGHT(0)
             #endif
 
             #if USE_SHAPE_LIGHT_TYPE_1
             SHAPE_LIGHT(1)
             #endif
 
             #if USE_SHAPE_LIGHT_TYPE_2
             SHAPE_LIGHT(2)
             #endif
 
             #if USE_SHAPE_LIGHT_TYPE_3
             SHAPE_LIGHT(3)
             #endif
 
             Varyings CombinedShapeLightVertex(Attributes v)
             {
                 Varyings o = (Varyings)0;
                 UNITY_SETUP_INSTANCE_ID(v);
                 UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 
                 o.positionCS = TransformObjectToHClip(v.positionOS);
                 #if defined(DEBUG_DISPLAY)
                 o.positionWS = TransformObjectToWorld(v.positionOS);
                 #endif
                 o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                 o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                 o.scale = float3(
                 length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
                 length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)), 
                 length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z))  
                 );

                 o.color = v.color;

                //URP Lighting ends here

                 return o;
             }
 
             #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
             half4 CombinedShapeLightFragment(Varyings i) : SV_Target
             {
                 i.uv *=_tiling;
                 i.uv += _pos * _scrollSpeed;
                 //get clouds
                 float4 x = float4(0,0,0,0);

                 if(_cloudMode==3) x = cloudBrownianNorm(i.uv,_scale,_sunDirection,_cloudColor,_alpha,_speed1,_speed2,_cloudsDir1,_cloudsDir2) ;
                 if(_cloudMode==2) x = cloudWorleyNorm(i.uv,_scale,_sunDirection,_cloudColor,_alpha,_speed1,_speed2,_cloudsDir1,_cloudsDir2) ;
                 if(_cloudMode==1) x = lightCloudBrownianNorm(i.uv,_scale,_sunDirection,_cloudColor,_alpha,_speed1,_speed2,_cloudsDir1,_cloudsDir2);
                 if(_cloudMode==0) x = gradientCloudNorm(i.uv,_scale,_sunDirection,_cloudColor,_alpha,_speed1,_speed2,_cloudsDir1,_cloudsDir2);

                 //toon shading
                 if(_step!=0)
                 {
                     x.w = smoothstep(0,min(_alpha,_cloudAlphaMax),x.w);
                 }

                 //clamp output
                 x.a = clamp(x.a,0,_cloudAlphaMax);

                 //set
                 const half4 main = x;
                 
                 //URP Lighting starts here
                 
                 const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                 SurfaceData2D surfaceData;
                 InputData2D inputData;
 
                 InitializeSurfaceData(main.rgb, main.a, mask, surfaceData);
                 InitializeInputData(i.uv, i.lightingUV, inputData);
 
                 return CombinedShapeLightShared(surfaceData, inputData);
             }


             ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}

//  todo in new asset : 
//  - per - sprite masks
//  - per sprite normal illusion
//  - placeable shadow blocks