Shader "Sumi/Ronin Ink"
{
 Properties {
  [MainColor] _BaseColor("Dry paper highlight",Color)=(.30,.295,.28,1)
  _Ink("Black ink",Color)=(.014,.015,.016,1)
  _Density("Scattered stroke scale",Float)=12
  _Gold("Mastery accent amount",Range(0,1))=0
  _Red("Damage stain amount",Range(0,1))=0
  _AccentMask("Edge accent eligibility",Range(0,1))=0
  _CutEnabled("Stylized split enabled",Range(0,1))=0
  _CutY("Stylized split world height",Float)=0
  _CutSide("Stylized split side",Float)=1
  _Dissolve("Ink vapor",Range(0,1))=0
  _DissolveSeed("Ink vapor seed",Float)=0
  _DissolveWound("Wound origin",Vector)=(0,0,0,0)
  _DeathWound("Fatal injury origin",Vector)=(0,0,0,0)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_Ink;float _Density,_Gold,_Red,_AccentMask;
   float _CutEnabled,_CutY,_CutSide,_Dissolve,_DissolveSeed;float4 _DissolveWound,_DeathWound;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;float3 world:TEXCOORD3;float4 color:COLOR;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);o.color=i.color;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   float marks(float2 uv)
   {
    float2 q=uv*_Density;float2 cell=floor(q);float2 p=frac(q)-.5;
    float seed=hash(cell);float curve=p.y*(seed-.4)*.9+sin(p.y*7+seed*18)*.025;
    float d=abs(p.x+curve+(seed-.5)*.3);
    float aa=max(fwidth(q.x),.025);
    float stroke=1-smoothstep(.018,.018+aa,d);
    float ends=1-smoothstep(.16+seed*.12,.31+seed*.12,abs(p.y));
    return stroke*ends*step(.48,seed);
   }
   half4 frag(V i):SV_Target {
    if(_CutEnabled>.5)clip((i.world.y-_CutY)*_CutSide);
    float vapor=0,soak=0;
    if(_Dissolve>.001)
    {
     float n0=noise(i.uv*17+_DissolveSeed)+noise(i.uv*41+_DissolveSeed*.7)*.45+noise(i.world.xz*2.4+_DissolveSeed)*.2;
     float woundDistance=distance(i.world,_DissolveWound.xyz);
     float front=saturate((woundDistance+.15)*.58);
     vapor=saturate(n0*.22+front*.68);
     soak=saturate((_Dissolve-vapor+.18)*5);
     clip(vapor-_Dissolve*1.12);
    }
    float3 n=normalize(i.n),eye=normalize(_WorldSpaceCameraPos-i.world);
    float shade=saturate(dot(n,normalize(float3(-.6,.75,-.32)))*.5+.5);
    float wash=noise(i.uv*8.7+3.1)*.7+noise(i.uv*21.3)*.3;
    float threshold=shade+(wash-.5)*.21;
    float lightBand=smoothstep(.69,.72,threshold);
    float midBand=smoothstep(.43,.46,threshold)*.24;
    float3 paper=_BaseColor.rgb*lerp(.55,1,saturate(i.color.r));
    float3 c=lerp(_Ink.rgb,paper,saturate(midBand+lightBand*.72));
    // Most surfaces remain quiet. Sparse marks appear only in broken wash islands.
    float hatch=marks(i.uv)*smoothstep(.42,.7,wash)*(1-lightBand*.8);
    c=lerp(c,_Ink.rgb,hatch*.8);
    float edge=pow(1-abs(dot(n,eye)),5);
    c=lerp(c,_Ink.rgb,edge*.77);
    float dry=smoothstep(.77,.9,noise(i.uv*63))*smoothstep(.62,.82,wash);
    c+=dry*.026;
    float accent=_AccentMask*edge*smoothstep(.4,.65,wash);
    c=lerp(c,float3(.65,.38,.055),saturate(_Gold)*accent);
    // Red replaces pigment locally, never interpolates gold and red together.
    float injury=saturate(_Red)*smoothstep(.43,.65,noise(i.uv*7+8));
    if(_DeathWound.w>.5)injury*=1-smoothstep(.18,.85,distance(i.world,_DeathWound.xyz));
    c=lerp(c,float3(.33,.012,.024),injury);
    if(_Dissolve>.001)c=lerp(c,_Ink.rgb,soak);
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
 }
}
