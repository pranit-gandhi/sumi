Shader "Sumi/Ronin Contour"
{
 Properties {
  _Width("Ink edge width",Float)=.003
  _Color("Ink",Color)=(.013,.014,.015,1)
  _CutEnabled("Stylized split enabled",Range(0,1))=0
  _CutY("Stylized split world height",Float)=0
  _CutSide("Stylized split side",Float)=1
  _Dissolve("Ink vapor",Range(0,1))=0
  _DissolveSeed("Ink vapor seed",Float)=0
  _DissolveWound("Wound origin",Vector)=(0,0,0,0)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Cull Front ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Width;float4 _Color;float _CutEnabled,_CutY,_CutSide,_Dissolve,_DissolveSeed;float4 _DissolveWound;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float fog:TEXCOORD0;float3 world:TEXCOORD1;float2 uv:TEXCOORD2;};
   V vert(A i){V o;float variation=.70+.22*sin(i.uv.y*71+i.uv.x*39)+.08*sin(i.uv.x*133);float3 p=TransformObjectToWorld(i.p.xyz)+TransformObjectToWorldNormal(i.n)*_Width*variation;o.p=TransformWorldToHClip(p);o.world=p;o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   half4 frag(V i):SV_Target{
    if(_CutEnabled>.5)clip((i.world.y-_CutY)*_CutSide);
    if(_Dissolve>.001){float n=noise(i.uv*17+_DissolveSeed)*.22+saturate((distance(i.world,_DissolveWound.xyz)+.15)*.58)*.68;clip(n-_Dissolve*1.12);}
    return half4(MixFog(_Color.rgb,i.fog),1);
   }
   ENDHLSL
  }
 }
}
