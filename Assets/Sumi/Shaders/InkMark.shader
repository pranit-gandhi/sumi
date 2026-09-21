Shader "Sumi/Ink Mark"
{
 Properties {
  [MainTexture] _BaseMap("Dry brush",2D)="white"{}
  [MainColor] _BaseColor("Ink",Color)=(.035,.032,.028,1)
  _Wet("Soak progress",Range(0,1))=1
  _Dry("Drying",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;float4 _BaseColor;float _Wet,_Dry;
   CBUFFER_END
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
   half4 frag(V i):SV_Target
   {
    float brush=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;
    float2 q=i.uv-.5;
    float along=abs(q.x+q.y*.19);
    float across=abs(q.y+sin(q.x*9)*.025);
    float taper=1-smoothstep(.32,.49,along);
    float fiber=1-smoothstep(.08,.17*(1-along*.65),across);
    float silhouette=taper*fiber;
    float edge=along*.65+across*1.8;
    float wet=saturate((_Wet-edge+.22)*4);
    float dry=saturate((_Dry+edge-.65)*3);
    float a=saturate(brush*4)*silhouette*_BaseColor.a*wet*(1-dry);
    clip(a-.065);
    return half4(_BaseColor.rgb,a);
   }
   ENDHLSL
  }
 }
}
