Shader "Sumi/Lantern Glow"
{
    Properties { [MainColor] _BaseColor("Amber",Color)=(2.2,.82,.19,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
            V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);return o;}
            half4 frag(V i):SV_Target{return half4(_BaseColor.rgb,1);}
            ENDHLSL
        }
    }
}
