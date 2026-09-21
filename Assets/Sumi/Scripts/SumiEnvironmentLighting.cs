using UnityEngine;
namespace Sumi
{
 // Static practical data shared by ink and damp-earth shading; no frame searches.
 [ExecuteAlways]
 public sealed class SumiEnvironmentLighting : MonoBehaviour
 {
  public Light[] practicals;
  readonly Vector4[] positions=new Vector4[20],colors=new Vector4[20];
  void OnEnable(){Publish();}
  void OnValidate(){Publish();}
  public void Publish(){
   int count=0;
   if(practicals!=null)foreach(var lamp in practicals){
    if(!lamp||!lamp.enabled||count==20)continue;
    Vector3 p=lamp.transform.position;positions[count]=new Vector4(p.x,p.y,p.z,lamp.range);
    Color c=lamp.color.linear;float gain=lamp.intensity*.44f;
    colors[count]=new Vector4(c.r*gain,c.g*gain,c.b*gain,1);count++;
   }
   Shader.SetGlobalVectorArray("_SumiLampPositions",positions);
   Shader.SetGlobalVectorArray("_SumiLampColors",colors);
   Shader.SetGlobalInt("_SumiLampCount",count);
  }
  void OnDisable(){Shader.SetGlobalInt("_SumiLampCount",0);}
 }
}
