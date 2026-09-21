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

            half4 frag(Varyings input) : SV_Target
            {
                float3 d=normalize(input.direction);float rise=max(0,d.y);
                float3 color=lerp(SumiHorizon(),float3(.125,.048,.016),smoothstep(.0,.24,rise));
                color=lerp(color,float3(.026,.012,.009),smoothstep(.12,.78,rise));
                float2 cloudUV=float2(atan2(d.x,d.z)*3.8,d.y*15);
                float cloud=SumiWash(cloudUV*float2(1,.55)+4);
                float wisps=SumiWash(cloudUV*float2(2.7,1.8)+11);
                float veil=smoothstep(.36,.64,cloud+wisps*.18);
                float horizonClear=smoothstep(.01,.12,rise);
                color*=1-veil*.38*horizonClear;
                float3 moonDir=normalize(float3(.34,.27,.89));
                float moonDot=dot(d,moonDir);
                float disc=smoothstep(.99935,.99965,moonDot);
                float halo=exp(-(1-moonDot)*460)*.022;
                float cloudCover=smoothstep(.38,.65,SumiWash(cloudUV*float2(1.7,4.5)+16));
                float moonTexture=.65+.35*SumiWash(d.xz*140);
                color+=float3(.64,.32,.105)*(disc*moonTexture*(1-cloudCover*.93)+halo)*horizonClear;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
