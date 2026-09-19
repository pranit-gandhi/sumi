using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Sumi
{
    // Original low-poly geometry. Shared mesh/material caches keep runtime allocations bounded.
    public static class SumiArt
    {
        public static readonly Color Paper=new Color(.91f,.91f,.88f), Ink=new Color(.075f,.079f,.080f), Gold=new Color(1f,.68f,.12f), Red=new Color(.72f,.028f,.065f);
        public static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        public static readonly Dictionary<string,Mesh> Meshes=new Dictionary<string,Mesh>();
        public static Material Mat(string name,Color color,bool unlit=false)
        {
            if(Materials.TryGetValue(name,out var m)&&m) return m;
            m=Resources.Load<Material>("Sumi/"+name);
            if(!m){m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Sumi/Ink Wash"));m.name=name;m.SetColor("_BaseColor",color);m.enableInstancing=true;}
            Materials[name]=m;return m;
        }
        public static Material Black=>Mat("Ink",Ink);
        public static Material White=>Mat("Paper",Paper);
        public static Material Stone=>Mat("Stone",new Color(.67f,.68f,.65f));
        public static Material Gilt=>Mat("Gold",Gold,true);
        public static Material Crimson=>Mat("Crimson",Red,true);
        public static Material Moon=>Mat("Moon",new Color(.93f,.90f,.78f),true);
        public static Transform Node(string name,Transform parent,Vector3 pos)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
        public static GameObject Shape(string name,Transform parent,Mesh mesh,Vector3 pos,Vector3 scale,Material material,bool collider=false)
        {
            var t=Node(name,parent,pos); t.localScale=scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;
            if(material.shader.name=="Sumi/Ink Wash")
            {
                var contour=Resources.Load<Material>("Sumi/Contour");
                if(contour)r.sharedMaterials=new[]{material,contour};
            }
            if(collider){t.gameObject.AddComponent<BoxCollider>();t.gameObject.layer=8;}
            return t.gameObject;
        }
        public static GameObject Box(string name,Transform p,Vector3 pos,Vector3 scale,Material mat,bool collision=false)=>Shape(name,p,Cube(),pos,scale,mat,collision);
        public static Mesh Cube()
        {
            if(Meshes.TryGetValue("Cube",out var m)&&m)return m;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);m=Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);m.name="Cube";Object.DestroyImmediate(go);Meshes["Cube"]=m;return m;
        }
        public static Mesh Lathe(string name,float[] radii,float[] heights,int segments=12)
        {
            if(Meshes.TryGetValue(name,out var cached)&&cached)return cached;
            var v=new List<Vector3>();var tri=new List<int>();var uv=new List<Vector2>();
            for(int y=0;y<radii.Length;y++) for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;v.Add(new Vector3(Mathf.Cos(a)*radii[y],heights[y],Mathf.Sin(a)*radii[y]));uv.Add(new Vector2((float)i/segments,(float)y/(radii.Length-1)));}
            for(int y=0;y<radii.Length-1;y++)for(int i=0;i<segments;i++){int a=y*(segments+1)+i,b=a+segments+1;tri.Add(a);tri.Add(b);tri.Add(a+1);tri.Add(a+1);tri.Add(b);tri.Add(b+1);}
            var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(tri,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();Meshes[name]=m;return m;
        }
        public static Mesh Cone=>Lathe("Cone",new[]{0f,1f,0f},new[]{-.5f,-.5f,.5f},16);
        public static Mesh Cylinder=>Lathe("Column",new[]{0f,.5f,.5f,0f},new[]{-.5f,-.5f,.5f,.5f},12);
        public static Mesh Torso=>Lathe("Haori",new[]{0f,.38f,.35f,.47f,.26f,0f},new[]{0f,0f,.36f,.67f,.83f,.83f},10);
        public static Mesh Sleeve=>Lathe("Sleeve",new[]{0f,.16f,.26f,.23f,0f},new[]{0f,0f,-.40f,-.50f,-.50f},8);
        public static Mesh Hat=>Lathe("Kasa",new[]{0f,.70f,.71f,.14f,0f},new[]{-.055f,-.055f,0f,.26f,.27f},32);
        public static Mesh Roof()
        {
            if(Meshes.TryGetValue("SweptRoof",out var existing)&&existing)return existing;
            var v=new List<Vector3>();var indices=new List<int>();
            for(int s=-1;s<=1;s+=2)for(int j=0;j<7;j++)
            {
                float t=j/6f;float x=s*t;float y=.9f*(1-t)*(1-t)+.12f*Mathf.Pow(t,8);
                v.Add(new Vector3(x,y,-1f-.10f*t));v.Add(new Vector3(x,y,1f+.10f*t));
            }
            for(int s=0;s<2;s++)for(int j=0;j<6;j++){int a=s*14+j*2;indices.Add(a);indices.Add(a+2);indices.Add(a+1);indices.Add(a+1);indices.Add(a+2);indices.Add(a+3);indices.Add(a+1);indices.Add(a+2);indices.Add(a);indices.Add(a+3);indices.Add(a+2);indices.Add(a+1);}
            var m=new Mesh{name="SweptRoof"};m.SetVertices(v);m.SetTriangles(indices,0);m.RecalculateNormals();m.RecalculateBounds();Meshes[m.name]=m;return m;
        }
        public static Transform Beam(string name,Transform parent,Vector3 a,Vector3 b,float width,Material material)
        {var t=Box(name,parent,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),material).transform;t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;}
    }
}
