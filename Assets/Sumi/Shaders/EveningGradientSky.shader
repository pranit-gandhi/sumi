Shader "Sumi/Evening Gradient Sky"
{
    Properties
    {
        _HorizonColor("Hazy ember horizon", Color) = (.58,.245,.10,1)
        _MiddleColor("Burnt orange wash", Color) = (.29,.09,.036,1)
        _ZenithColor("Brown-black zenith", Color) = (.068,.028,.021,1)
        _MoonColor("Veiled moon", Color) = (1.15,.58,.22,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WorldAtmosphere.hlsl"

            float4 _HorizonColor, _MiddleColor, _ZenithColor, _MoonColor;
            float4 _SumiSunDirection;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION;float3 direction:TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.direction=input.positionOS.xyz;
                return output;
            }

            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            float cloudNoise(float3 p){
                float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
                float2 lo=a.xz+a.y*float2(37,71),hi=lo+float2(37,71);
                float lower=lerp(lerp(hash(lo),hash(lo+float2(1,0)),f.x),lerp(hash(lo+float2(0,1)),hash(lo+1),f.x),f.z);
                float upper=lerp(lerp(hash(hi),hash(hi+float2(1,0)),f.x),lerp(hash(hi+float2(0,1)),hash(hi+1),f.x),f.z);
                return lerp(lower,upper,f.y);
            }
            float cloudWash(float3 p){return cloudNoise(p)*.54+cloudNoise(p*2.13+4.7)*.28+cloudNoise(p*4.73-9.1)*.18;}

            half4 frag(Varyings input) : SV_Target
            {
                float3 d=normalize(input.direction);float rise=max(0,d.y);
                float3 color=lerp(SumiHorizon(),float3(.205,.077,.025),smoothstep(.0,.22,rise));
                color=lerp(color,float3(.035,.015,.010),smoothstep(.16,.78,rise));
                // Sample a continuous direction volume, avoiding the +/-pi longitude seam.
                float3 cloudUV=d*float3(4,8.25,4);
                float cloud=cloudWash(cloudUV+4);
                float wisps=cloudWash(cloudUV*float3(2.7,1.8,2.7)+11);
                float veil=smoothstep(.36,.64,cloud+wisps*.18);
                float horizonClear=smoothstep(.01,.12,rise);
                color*=1-veil*.38*horizonClear;
                float3 moonDir=normalize(_SumiSunDirection.xyz+float3(0,0,.00001));
                float moonDot=dot(d,moonDir);
                float disc=smoothstep(.99805,.99845,moonDot);
                float halo=exp(-(1-moonDot)*250)*.045;
                float cloudCover=smoothstep(.40,.71,cloudWash(cloudUV*float3(2.4,2.6,2.4)+16));
                float moonTexture=.65+.35*SumiWash(d.xz*140);
                color+=float3(.78,.40,.145)*(disc*moonTexture*(1-cloudCover*.52)+halo)*horizonClear;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
