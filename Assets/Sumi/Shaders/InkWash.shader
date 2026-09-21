Shader "Sumi/Ink Wash"
{
 Properties { [MainColor] _BaseColor("Ink pigment",Color)=(.8,.8,.8,1) [MainTexture] _BaseMap("Paper",2D)="white"{} _Cutoff("Cutoff",Float)=.5 _Porosity("Dry brush gaps",Range(0,.45))=.08 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Name "Drawn Surface"
   Tags { "LightMode"="UniversalForwardOnly" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "WorldAtmosphere.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;float4 _BaseMap_ST;float _Cutoff,_Porosity;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
   Varyings vert(Attributes v){Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float valueNoise(float3 p)
   {
    float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
   }
   half4 frag(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);
    float3 n=normalize(i.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    float shade=saturate(dot(n,light.direction)*.5+.5);shade*=lerp(.35,1,light.shadowAttenuation);
    float grain=hash(floor(i.positionWS*83));
    float broad=valueNoise(i.positionWS*1.9),fine=valueNoise(i.positionWS*8.3+7.1);
    float band=.58+.42*smoothstep(.18,.78,shade);
    float dry=smoothstep(.66,.82,broad)*smoothstep(.5,.78,fine)*(1-smoothstep(.48,.82,shade));
    float broken=step(.78,hash(floor(i.positionWS*5.2)))*smoothstep(.12,.55,1-shade);
    float pigment=broad*.62+fine*.38;
    clip(pigment-_Porosity);
    float value=band+(broad-.5)*.09+(fine-.5)*.035-dry*.12-broken*.055+(grain-.5)*.024;
    float3 ink=_BaseColor.rgb*value+float3(.004,.004,.003);
    ink+=SumiLocalLight(i.positionWS,n)*(_BaseColor.rgb*.85+.018)*value;
    ink=SumiFog(ink,i.positionWS);
    return half4(ink,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
