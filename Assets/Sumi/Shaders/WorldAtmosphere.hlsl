#ifndef SUMI_WORLD_ATMOSPHERE
#define SUMI_WORLD_ATMOSPHERE
// Shared linear radiance and per-pixel distance haze. Combat foreground stays clear.
float4 _SumiLampPositions[20],_SumiLampColors[20];
int _SumiLampCount;
float SumiHash(float2 p){p=frac(p*float2(.1031,.1030));p+=dot(p,p.yx+33.33);return frac((p.x+p.y)*p.x);}
float SumiNoise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(SumiHash(a),SumiHash(a+float2(1,0)),f.x),lerp(SumiHash(a+float2(0,1)),SumiHash(a+1),f.x),f.y);}
float SumiWash(float2 p){return SumiNoise(p)*.54+SumiNoise(p*2.13+4.7)*.28+SumiNoise(p*4.73-9.1)*.18;}
float3 SumiHorizon(){return float3(.155,.092,.053);}
float3 SumiFog(float3 color,float3 world)
{
 float d=max(0,length(world-_WorldSpaceCameraPos)-12);
 float density=lerp(.00064,.00020,saturate(max(world.y,0)/22));
 float air=1-exp(-d*d*density);
 float lowMist=(1-exp(-d*.018))*exp(-max(world.y,0)*.32);
 air=saturate(air+lowMist*(1-air)*.24);
 return lerp(color,SumiHorizon(),air);
}
float3 SumiLocalLight(float3 world,float3 n)
{
 float3 result=0;
 for(int k=0;k<_SumiLampCount;k++){
  float3 delta=_SumiLampPositions[k].xyz-world;float d2=dot(delta,delta),range=_SumiLampPositions[k].w;
  float falloff=pow(saturate(1-d2/(range*range)),2)/(1+d2*.42);
  result+=_SumiLampColors[k].rgb*falloff*(.2+.8*saturate(dot(n,normalize(delta))));
 }
 return result;
}
#endif
