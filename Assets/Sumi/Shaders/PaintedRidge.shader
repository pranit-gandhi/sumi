Shader "Sumi/Painted Ridge"
{
 Properties {[MainColor] _BaseColor("Ridge pigment",Color)=(.2,.18,.15,1)}
 SubShader{
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {
 Tags {"LightMode"="UniversalForwardOnly"} Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "WorldAtmosphere.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;
 CBUFFER_END
 struct A{float4 p:POSITION;float4 c:COLOR;};
 struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float4 c:COLOR;};
 V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.c=i.c;return o;}
 half4 frag(V i):SV_Target{
  float along=atan2(i.world.x,i.world.z)*length(i.world.xz);
  float2 uv=float2(along,i.world.y);
  float wash=SumiWash(uv*float2(.34,.65));
  float folds=SumiWash(float2(along*.8+i.world.y*.62,i.world.y*.21));
  float brush=SumiWash(uv*float2(3.2,2.7)+wash*1.7);
  float pigmentValue=.45+wash*.7+smoothstep(.42,.64,folds)*.28;
  pigmentValue*=.88+brush*.24;
  float3 pigment=_BaseColor.rgb*pigmentValue*i.c.rgb;
  float3 hazed=SumiFog(pigment,i.world);
  float foot=exp(-max(0,i.world.y)*.43);
  return half4(lerp(hazed,SumiHorizon(),foot*.25),1);
 }
 ENDHLSL
 }
 }
}
