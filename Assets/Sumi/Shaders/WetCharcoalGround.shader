Shader "Sumi/Wet Charcoal Ground"
{
    Properties
    {
        [MainColor] _BaseColor("Charcoal",Color)=(.13,.125,.118,1)
        _Wetness("Broken wetness",Range(0,1))=.58
        _Smoothness("Peak smoothness",Range(0,1))=.62
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WorldAtmosphere.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _Wetness,_Smoothness;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);return o;}
            float hash(float2 p){p=frac(p*float2(.1031,.1030));p+=dot(p,p.yx+33.33);return frac((p.x+p.y)*p.x);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            float3 SpecularLight(Light l,float3 n,float3 v,float wet)
            {
                float3 h=normalize(l.direction+v);
                float exponent=lerp(10,74,_Smoothness*wet);float spec=pow(saturate(dot(n,h)),exponent)*lerp(.015,.42,wet);
                return l.color*spec*l.distanceAttenuation*l.shadowAttenuation;
            }
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;
                float broad=SumiWash(p*.24),stone=SumiWash(p*1.6+7),grain=SumiNoise(p*21);
                float wet=smoothstep(.32,.66,broad)*_Wetness;
                float fragments=SumiNoise(p*float2(5.5,16))+SumiNoise(p*float2(15,5))*.45;
                float pores=.18+.82*smoothstep(.34,.86,fragments);
                float fissure=(1-smoothstep(.006,.022,abs(SumiNoise(p*2.6+SumiNoise(p*6))-.49)))*smoothstep(.53,.7,broad);
                float value=lerp(.50,1.18,stone)*lerp(1,.67,wet);
                value*=1-fissure*.19;value*=.91+grain*.18;
                float3 n=normalize(i.normal+float3((SumiNoise(p*9)-.5)*.09,0,(SumiNoise(p*9+6)-.5)*.09));
                Light key=GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 color=_BaseColor.rgb*value*lerp(.62,1,key.shadowAttenuation)+float3(.012,.010,.008);
                color+=SumiLocalLight(i.world,n)*float3(.24,.21,.18)*(value*.65+.35);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                // Virtual light below the damp surface: streaks follow the viewer.
                for(int k=0;k<_SumiLampCount;k++){
                    float3 lamp=_SumiLampPositions[k].xyz;
                    float height=max(.6,lamp.y-i.world.y);
                    float3 reflected=normalize(float3(lamp.x-i.world.x,-height,lamp.z-i.world.z));
                    float3 ray=-view;
                    float angular=dot(reflected.xz,normalize(float2(-ray.z,ray.x))),vertical=abs(reflected.y-ray.y);
                    float align=smoothstep(.65,.95,dot(normalize(reflected.xz),normalize(ray.xz)));
                    float streak=exp(-angular*angular/lerp(.0028,.0009,_Smoothness)-vertical*vertical/.008)*align;
                    float d=length(lamp.xz-p),fade=1/(1+d*d*.045);
                    color+=_SumiLampColors[k].rgb*streak*fade*wet*pores*.72;
                }
                return half4(SumiFog(color,i.world),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
