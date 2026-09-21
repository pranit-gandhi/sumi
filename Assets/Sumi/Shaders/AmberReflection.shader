Shader "Sumi/Amber Reflection"
{
    Properties { [MainColor] _BaseColor("Broken amber",Color)=(1.45,.48,.10,.28) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" } Blend SrcAlpha One ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;};
            V vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(41.7,289.1)))*45758.5453);}
            half4 frag(V i):SV_Target
            {
                float bands=.45+.55*smoothstep(.38,.73,hash(float2(floor(i.w.x*7.3),floor(i.w.z*2.1))));
                float fade=.72+.28*hash(floor(i.w.xz*4.7));return half4(_BaseColor.rgb,_BaseColor.a*bands*fade);
            }
            ENDHLSL
        }
    }
}
