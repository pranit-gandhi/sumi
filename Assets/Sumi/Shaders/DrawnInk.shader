Shader "Sumi/Drawn Ink"
{
 Properties {
  [MainTexture] _BaseMap("Original drawing atlas",2D)="white"{}
  [MainColor] _BaseColor("Ink",Color)=(.025,.025,.025,1)
  _Frame("Atlas scale and offset",Vector)=(.25,.5,0,0)
  _Strength("Ink strength",Range(0,1))=1
  _Wind("Paper motion",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite On Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "WorldAtmosphere.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_Frame;float _Strength,_Wind;
   CBUFFER_END
   struct A{float4 pos:POSITION;float2 uv:TEXCOORD0;};struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
   V vert(A i){V o;float phase=floor(_Time.y*12)/12;float sway=sin(phase*2.1+i.uv.y*5)*.018*_Wind*i.uv.y*i.uv.y;i.pos.x+=sway;o.world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(o.world);o.uv=clamp(i.uv,.001,.999)*_Frame.xy+_Frame.zw;return o;}
   half4 frag(V i):SV_Target {
    float3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    float luminance=dot(original,float3(.299,.587,.114));
    // Original image is preserved; the drawing material deposits only ink-dark marks.
    // All light pixels, including the generated checker background, deposit no pigment.
    float coverage=(1-smoothstep(.04,.70,luminance))*_Strength*_BaseColor.a;
    clip(coverage-.015);
    return half4(SumiFog(_BaseColor.rgb,i.world),coverage);
   }
   ENDHLSL
  }
 }
}
