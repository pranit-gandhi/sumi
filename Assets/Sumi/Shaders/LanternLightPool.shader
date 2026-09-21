Shader "Sumi/Lantern Light Pool"
{
    Properties { [MainColor] _BaseColor("Warm pool",Color)=(.72,.26,.065,.16) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" } Blend SrcAlpha One ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;};
            V vert(A i){V o;o.local=i.p.xyz;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            half4 frag(V i):SV_Target
            {
                float radial=saturate(1-length(i.local.xz)*1.88);radial=radial*radial*(3-2*radial);
                float breakup=lerp(.70,1,hash(floor(i.world.xz*3.2)));float alpha=_BaseColor.a*radial*breakup;
                return half4(_BaseColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
