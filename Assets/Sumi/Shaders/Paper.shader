Shader "Sumi/Paper"
{
    Properties { [MainColor] _BaseColor("Paper", Color) = (.75,.74,.69,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A {float4 p:POSITION;};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.p.z);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(V i):SV_Target
            {
                float grain=hash(i.p.xy)*.009;
                float wash=noise(i.world.xz*.19)*.016+noise(i.world.xz*1.7)*.007;
                float3 paper=_BaseColor.rgb+grain-wash;
                return half4(MixFog(paper,i.fog),1);
            }
            ENDHLSL
        }
    }
}
