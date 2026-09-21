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
  float wash=SumiWash(i.world.xz*.5+i.world.y*.17);
  float brush=SumiNoise(float2(i.world.x+i.world.z+i.world.y*.8,i.world.y*.27)*2.1);
  float3 pigment=_BaseColor.rgb*(.63+wash*.64+brush*.14)*i.c.rgb;
  return half4(SumiFog(pigment,i.world),1);
 }
 ENDHLSL
 }
 }
}
