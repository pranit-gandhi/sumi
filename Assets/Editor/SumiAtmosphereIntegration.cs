using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Sumi;
using Object=UnityEngine.Object;

// Additive, repeatable integration of the existing authored world.
// Existing hut/player meshes, drawing textures, transforms and gameplay are retained.
public static class SumiAtmosphereIntegration
{
 const string Folder="Assets/Sumi/Art/Atmosphere";
 const string RootName="Atmosphere integration";
 static System.Random rng;
 static float R(float a,float b){return a+(b-a)*(float)rng.NextDouble();}
 static Material Mat(string name,string shader,Color color){
  string path="Assets/Sumi/Resources/Sumi/"+name+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
  m.shader=Shader.Find(shader);m.SetColor("_BaseColor",color);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
 }
 static Mesh SaveMesh(string name,List<Vector3> v,List<int> tri,List<Color> colors=null){
  var m=new Mesh{name=name};m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(tri,0);
  if(colors!=null)m.SetColors(colors);m.RecalculateNormals();m.RecalculateBounds();
  string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(saved){EditorUtility.CopySerialized(m,saved);Object.DestroyImmediate(m);EditorUtility.SetDirty(saved);return saved;}
  AssetDatabase.CreateAsset(m,path);return m;
 }
 static GameObject Shape(string name,Transform parent,Mesh mesh,Material mat){
  var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
  go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;
  renderer.shadowCastingMode=ShadowCastingMode.Off;return go;
 }
 [MenuItem("Sumi/Integrate Atmosphere and Damp Ground")]
 public static void Apply(){
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
  var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SumiShrine.unity")throw new InvalidOperationException("Open SumiShrine first.");
  var previous=GameObject.Find(RootName);if(previous)Object.DestroyImmediate(previous);
  var world=scene.GetRootGameObjects().First(go=>go.name.Contains("dimensional ink village"));
  var root=new GameObject(RootName).transform;root.SetParent(world.transform,false);rng=new System.Random(92171);
  Directory.CreateDirectory(Folder);
  var ground=Mat("WetCharcoalGround","Sumi/Wet Charcoal Ground",new Color(.31f,.28f,.245f));
  ground.SetFloat("_Wetness",.85f);ground.SetFloat("_Smoothness",.55f);
  // Keep the original gameplay collider exactly as authored. Only its rendering is superseded.
  var original=world.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Damp charcoal courtyard");original.enabled=false;
  Shape("Continuous earth into haze",root,SaveMesh("ContinuousEarth",
   new List<Vector3>{new Vector3(-400,-.006f,-400),new Vector3(-400,-.006f,400),new Vector3(400,-.006f,400),new Vector3(400,-.006f,-400)},
   new List<int>{0,1,2,0,2,3}),ground);
  // Supersede the old baked-looking polygons with view-dependent surface shading.
  foreach(var r in world.GetComponentsInChildren<Renderer>(true)){
   if(r.name=="Broken amber ground reflection"||r.name=="Gateward amber reflection"||r.name=="Soft lantern illumination pool"||r.name=="Shallow damp ink patch")r.enabled=false;
  }
  foreach(var drawing in world.GetComponentsInChildren<SumiDrawing>()){
   if(drawing.name.StartsWith("Painted mountain layer")){drawing.strength=.028f;drawing.Apply();EditorUtility.SetDirty(drawing);}
  }
  var ridge=Mat("AtmosphericRidge","Sumi/Painted Ridge",new Color(.22f,.205f,.18f));
  BuildRidges(root,ridge);
  BuildMargins(root,ground);
  var grass=Mat("IntegratedSusuki","Sumi/Ink Wash",new Color(.56f,.535f,.475f));grass.SetFloat("_Porosity",.09f);
  var tufts=world.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Pale susuki cluster")).ToArray();
  foreach(var t in tufts)foreach(var renderer in t.GetComponentsInChildren<Renderer>())renderer.enabled=false;
  BuildGrass(root,tufts.Select(t=>t.position).ToArray(),grass);
  var lamps=world.GetComponentsInChildren<Light>().Where(l=>l.type==LightType.Point).ToArray();
  foreach(var lamp in lamps){lamp.color=new Color(1,.70f,.36f);lamp.intensity=5.5f;lamp.range=6.4f;lamp.shadows=LightShadows.None;EditorUtility.SetDirty(lamp);}
  var lighting=root.gameObject.AddComponent<SumiEnvironmentLighting>();lighting.practicals=lamps;lighting.Publish();
  RenderSettings.fogColor=new Color(.43f,.335f,.255f);RenderSettings.fogDensity=.019f;
  RenderSettings.ambientLight=new Color(.22f,.20f,.18f);
  if(RenderSettings.sun){RenderSettings.sun.shadowStrength=.55f;RenderSettings.sun.color=new Color(.78f,.69f,.57f);RenderSettings.sun.intensity=.65f;}
  var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Sumi/Resources/Sumi/Atmosphere.asset");
  if(profile.TryGet<ColorAdjustments>(out var grade)){grade.contrast.Override(5);grade.postExposure.Override(.18f);grade.saturation.Override(-8);grade.colorFilter.Override(Color.white);EditorUtility.SetDirty(grade);}
  if(profile.TryGet<Bloom>(out var bloom)){bloom.intensity.Override(.15f);bloom.threshold.Override(1.1f);EditorUtility.SetDirty(bloom);}
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  Debug.Log("SUMI_INTEGRATION_APPLIED lamps="+lamps.Length+" original gameplay colliders retained");
 }
 static void BuildRidges(Transform root,Material mat){
  // Three closed, irregular ridgelines surround the world. They have solid mass,
  // uneven peaks and different parallax, not repeated transparent atlas wallpaper.
  for(int layer=0;layer<3;layer++){
   var v=new List<Vector3>();var tr=new List<int>();var colors=new List<Color>();int n=256;
   float radius=54+layer*14;
   for(int i=0;i<=n;i++){
    float a=i*Mathf.PI*2/n;
    float peak=Mathf.Pow(Mathf.PerlinNoise(Mathf.Cos(a)*3.6f+layer*11+7,Mathf.Sin(a)*3.6f+17),2);
    float rough=Mathf.PerlinNoise(Mathf.Cos(a)*22+layer*7,Mathf.Sin(a)*22+9);
    float height=1.2f+layer*1.5f+peak*(12+layer*8)+rough*.85f;
    float r=radius+Mathf.Sin(a*7+layer)*2.2f;
    v.Add(new Vector3(Mathf.Sin(a)*r,-1.4f,Mathf.Cos(a)*r));v.Add(new Vector3(Mathf.Sin(a)*r,height,Mathf.Cos(a)*r));
    float wash=R(.7f,1.2f);colors.Add(new Color(.6f,.6f,.6f));colors.Add(new Color(wash,wash,wash));
    if(i<n){int b=i*2;tr.AddRange(new[]{b,b+1,b+2,b+1,b+3,b+2});}
   }
   Shape("Overlapping mountain mass "+layer,root,SaveMesh("Ridge"+layer,v,tr,colors),mat);
  }
 }
 static void BuildMargins(Transform root,Material mat){
  var v=new List<Vector3>();var tr=new List<int>();const int n=192,rows=9;
  for(int row=0;row<rows;row++)for(int i=0;i<=n;i++){
   float a=i*Mathf.PI*2/n,r=27+row*6.2f,x=Mathf.Sin(a)*r,z=Mathf.Cos(a)*r;
   float y=(Mathf.PerlinNoise(x*.095f+9,z*.095f+11)-.35f)*Mathf.Min(row*.7f,2.8f)-.055f;
   v.Add(new Vector3(x,y,z));
   if(row<rows-1&&i<n){int b=row*(n+1)+i,c=b+n+1;tr.AddRange(new[]{b,c,b+1,b+1,c,c+1});}
  }
  Shape("Uneven earth beyond courtyard",root,SaveMesh("EarthMargins",v,tr),mat);
 }
 static void Ribbon(List<Vector3> v,List<int> tri,Vector3 a,Vector3 b,float width,Vector3 side){
  int k=v.Count;v.Add(a-side*width);v.Add(a+side*width);v.Add(b-side*width*.65f);v.Add(b+side*width*.65f);
  tri.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3,k+1,k+2,k,k+3,k+2,k+1});
 }
 static void BuildGrass(Transform root,Vector3[] origins,Material mat){
  for(int cluster=0;cluster<origins.Length;cluster++){
   var v=new List<Vector3>();var tri=new List<int>();Vector3 center=origins[cluster];
   int count=(int)R(64,92);
   for(int s=0;s<count;s++){
    float a=R(0,Mathf.PI*2),r=R(.05f,1.65f);
    Vector3 start=center+new Vector3(Mathf.Cos(a)*r,.01f,Mathf.Sin(a)*r);
    float height=R(.28f,1.2f);Vector3 lean=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*R(.25f,.72f);
    Vector3 side=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a)),last=start;
    for(int segment=1;segment<=5;segment++){
     float t=segment/5f;Vector3 tip=start+Vector3.up*(height*(t-t*t*.12f))+lean*t*t;
     Ribbon(v,tri,last,tip,.019f*(1-t*.85f),side);last=tip;
    }
    if(s%3==0){
     for(int branch=0;branch<7;branch++){
      float t=branch/7f;Vector3 basePoint=last-Vector3.up*(.23f*(1-t));
      Vector3 tip=basePoint+side*(branch%2==0?1:-1)*(.09f*(1-t))+lean*.12f+Vector3.up*.1f;
      Ribbon(v,tri,basePoint,tip,.017f*(1-t*.7f),side);
     }
    }
   }
   var mesh=SaveMesh("SusukiBed"+cluster,v,tri);
   mesh.normals=Enumerable.Repeat(Vector3.up,mesh.vertexCount).ToArray();EditorUtility.SetDirty(mesh);
   Shape("Rooted susuki bed "+cluster,root,mesh,mat);
  }
 }
 // Capture only the current camera without changing gameplay or camera drivers.
 public static void Capture(){
  if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required.");
  SumiAutomation.CaptureAt("Logs/atmosphere-runtime.png",1600,900);
 }
 public static void ShaderCheck(){
  foreach(string name in new[]{"Sumi/Wet Charcoal Ground","Sumi/Ink Wash","Sumi/Drawn Ink","Sumi/Ink Contour","Sumi/Evening Gradient Sky","Sumi/Painted Ridge"}){
   var shader=Shader.Find(name);if(!shader)throw new Exception("Missing "+name);
   foreach(var message in ShaderUtil.GetShaderMessages(shader))if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(name+": "+message.message);
  }
  Debug.Log("SUMI_ATMOSPHERE_SHADERS_PASS");
 }
}
