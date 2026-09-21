Shader "Sumi/Ink Contour"
{
 Properties { [MainColor] _BaseColor("Line ink",Color)=(.055,.06,.06,1) _Width("Stroke width",Float)=.009 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
 Pass {Cull Front ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "WorldAtmosphere.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _Width;
 CBUFFER_END
 struct A {float4 pos:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 pos:SV_POSITION;float fog:TEXCOORD0;float3 world:TEXCOORD1;};
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 V vert(A i){UNITY_SETUP_INSTANCE_ID(i);V o;float3 p=TransformObjectToWorld(i.pos.xyz);float3 normal=TransformObjectToWorldNormal(i.normal);float jitter=1+.15*sin(p.y*31+p.x*28);p+=normal*_Width*jitter;o.world=p;o.pos=TransformWorldToHClip(p);o.fog=ComputeFogFactor(o.pos.z);return o;}
 half4 frag(V i):SV_Target{float broken=hash(floor(i.world*8));clip(broken-.105);return half4(SumiFog(_BaseColor.rgb,i.world),1);}
 ENDHLSL
 }}
}
