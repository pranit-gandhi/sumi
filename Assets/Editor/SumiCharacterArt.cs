using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sumi;

// Edits only render assets in the existing prefab. Never rebuilds its rig or controller.
public static class SumiCharacterArt
{
    const string Root="Assets/Sumi/Resources/Ronin/";
    // Authored in the kasa's local coordinates so the mesh and its dry strokes can never drift apart.
    // .74 is a 21% radius reduction from the last visible .94 pass, while retaining a readable kasa silhouette.
    const float KasaRadius=.74f, KasaBrimDrop=.105f, KasaCrown=.23f;
    static GameObject prefab;static SkinnedMeshRenderer skin;static Vector3[] samples;static BoneWeight[] weights;
    static Material outline;
    static Mesh Persist(Mesh m,string name)
    {
        m.name=name;string path=Root+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);return old;}
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static Material Mat(string name,float value,float density=13,float accent=0)
    {
        string path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Sumi/Ronin Ink"));AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find("Sumi/Ronin Ink");m.SetColor("_BaseColor",new Color(value,value*.985f,value*.955f));m.SetFloat("_Density",density);m.SetFloat("_AccentMask",accent);m.SetFloat("_Gold",0);m.SetFloat("_Red",0);EditorUtility.SetDirty(m);return m;
    }
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode before applying character art");
        prefab=PrefabUtility.LoadPrefabContents(Root+"Humanoid.prefab");
        try
        {
            var soot=Mat("Soot silhouette",.11f);var cotton=Mat("Brush cotton",.34f);var hems=Mat("Worn ink hems",.25f);var hat=Mat("Kasa ink wash",.31f,16,1);var edge=Mat("Pale blade edge",.66f,5,1);
            string op=Root+"Brush contour.mat";outline=AssetDatabase.LoadAssetAtPath<Material>(op);
            if(!outline){outline=new Material(Shader.Find("Sumi/Ronin Contour"));AssetDatabase.CreateAsset(outline,op);}outline.SetFloat("_Width",.003f);EditorUtility.SetDirty(outline);
            skin=prefab.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="SuperHero_Male");
            samples=skin.sharedMesh.vertices.Select(v=>prefab.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();weights=skin.sharedMesh.boneWeights;
            foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if(r.name=="Eyes"||r.name=="Eyebrows"||r.name=="Left lapel"||r.name=="Right lapel"||r.name=="Kasa woven ribs"){r.enabled=false;continue;}
                r.sharedMaterials=new[]{r.name=="Woven kasa"?hat:r.name=="Curved blade"?edge:soot};
            }
            skin.sharedMaterials=new[]{soot,outline};
            var animator=prefab.GetComponent<Animator>();
            var cloth=prefab.GetComponent<SumiClothMotion>();cloth.material=hems;
            var torso=Tube("Wrapped kimono",14,40,(t,a)=>{
                float y=Mathf.Lerp(.97f,1.49f,t);float rx=Mathf.Lerp(.215f,.27f,Mathf.Sin(t*Mathf.PI*.6f));float rz=.145f+Mathf.Sin(t*Mathf.PI)*.016f;
                float fold=Mathf.Sin(a*5+t*3)*.010f+Mathf.Sin(a*9-t*2)*.004f;
                return new Vector3(Mathf.Cos(a)*(rx+fold),y,Mathf.Sin(a)*(rz+fold)+.025f);
            });Skinned("Wrapped kimono",torso,cotton);
            foreach(int sign in new[]{-1,1})
            {
                var pants=Tube("Hakama "+sign,18,40,(t,a)=>{
                    float y=Mathf.Lerp(.19f,.98f,t);float fullness=Mathf.Sin(Mathf.Clamp01(t*1.8f)*Mathf.PI*.65f);
                    float rx=.095f+fullness*.075f,rz=.12f+fullness*.065f;
                    float fold=Mathf.Sin(a*7+.5f)*(.012f+.008f*t)+Mathf.Sin(a*11+t)*.004f;
                    return new Vector3(sign*.12f+Mathf.Cos(a)*(rx+fold),y+Mathf.Pow(1-t,14)*(.013f*Mathf.Sin(a*5+sign)),.035f+Mathf.Sin(a)*(rz+fold));
                });Skinned("Hakama "+sign,pants,hems,v=>LegWeight(v,sign));
                var sleeve=Tube("Loose sleeve "+sign,12,32,(t,a)=>{
                    float x=sign*Mathf.Lerp(.23f,.62f,t);float lower=Mathf.Max(0,-Mathf.Sin(a));
                    float ry=.105f+Mathf.Sin(t*Mathf.PI)*.015f,rz=.112f-t*.025f;
                    return new Vector3(x,1.455f+Mathf.Sin(a)*ry-lower*t*.065f,.07f+Mathf.Cos(a)*rz);
                });Skinned("Loose sleeve "+sign,sleeve,cotton);
            }
            // A single overlapping collar follows the torso instead of forming a rigid X.
            var collar=new Mesh();collar.vertices=new[]{new Vector3(-.135f,1.49f,-.143f),new Vector3(-.09f,1.49f,-.151f),new Vector3(.105f,1.025f,-.158f),new Vector3(.145f,1.025f,-.154f)};
            collar.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};collar.triangles=new[]{0,2,1,1,2,3};Skinned("Overlapping collar",collar,hems);
            // Keep the existing head-bone attachment and transform; correct the visible surface in local space.
            var hatFilter=prefab.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Woven kasa");
            var original=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Woven kasa.asset");var newHat=UnityEngine.Object.Instantiate(original);var hv=newHat.vertices;
            for(int i=0;i<hv.Length;i++){float a=Mathf.Atan2(hv[i].z,hv[i].x),r=new Vector2(hv[i].x,hv[i].z).magnitude/.57f;hv[i].x*=KasaRadius;hv[i].z*=KasaRadius;hv[i].y+=r*r*(Mathf.Sin(a*5)*.009f+Mathf.Sin(a*13)*.003f)-KasaBrimDrop;}
            newHat.vertices=hv;var ht=newHat.triangles;for(int i=0;i<ht.Length;i+=3){int swap=ht[i+1];ht[i+1]=ht[i+2];ht[i+2]=swap;}newHat.triangles=ht;newHat.RecalculateNormals();newHat.RecalculateBounds();hatFilter.sharedMesh=Persist(newHat,"Uneven kasa surface");hatFilter.GetComponent<Renderer>().sharedMaterials=new[]{hat,outline};
            HatMarks(hatFilter.transform);
            var bladeFilter=prefab.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Curved blade");
            var blade=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Curved blade.asset"));var bv=blade.vertices;
            for(int i=0;i<bv.Length;i++){float t=Mathf.Clamp01((bv[i].y-.03f)/.94f);bv[i].x+=t*.23f;}
            blade.vertices=bv;blade.RecalculateNormals();blade.RecalculateBounds();bladeFilter.sharedMesh=Persist(blade,"Readable katana silhouette");bladeFilter.GetComponent<Renderer>().sharedMaterial=soot;
            var ev=new List<Vector3>();var euv=new List<Vector2>();var et=new List<int>();
            for(int j=0;j<=24;j++){int index=j*2+1;var p=bv[index];ev.Add(p+new Vector3(-.0035f,0,-.001f));ev.Add(p+new Vector3(0,0,-.001f));euv.Add(new Vector2(0,j/24f));euv.Add(new Vector2(1,j/24f));if(j<24){int k=j*2;et.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}}
            var em=new Mesh();em.SetVertices(ev);em.SetUVs(0,euv);em.SetTriangles(et,0);em.SetColors(Enumerable.Repeat(Color.white,ev.Count).ToList());em.RecalculateNormals();
            edge.SetColor("_Ink",new Color(.34f,.335f,.32f));EditorUtility.SetDirty(edge);StaticSurface(bladeFilter.transform,"Single katana pale edge",em,edge);
            PrefabUtility.SaveAsPrefabAsset(prefab,Root+"Humanoid.prefab");AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        Debug.Log("SUMI_CHARACTER_ART applied only to existing render assets and cloth");
    }
    static void StaticSurface(Transform parent,string name,Mesh mesh,Material material)
    {
        var t=parent.Find(name);var go=t?t.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=Persist(mesh,name);go.GetComponent<MeshRenderer>().sharedMaterial=material;
    }
    static void HatMarks(Transform parent)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        Vector3 Point(float r,float a)
        {return new Vector3(Mathf.Cos(a)*.57f*r*KasaRadius,KasaCrown*Mathf.Pow(1-r,1.3f)+Mathf.Sin(a*32)*.004f*r+Mathf.Sin(a*3)*.007f*r*r+r*r*(Mathf.Sin(a*5)*.009f+Mathf.Sin(a*13)*.003f)-KasaBrimDrop+.0025f,Mathf.Sin(a)*.57f*r*KasaRadius);}
        for(int j=0;j<27;j++)
        {
            float a=j/27f*Mathf.PI*2+Mathf.Sin(j*17)*.06f;
            float start=.18f+.12f*(.5f+.5f*Mathf.Sin(j*8));float end=.72f+.20f*(.5f+.5f*Mathf.Cos(j*11));
            for(int s=0;s<8;s++)
            {
                if((s+j)%7==0)continue;
                float r=Mathf.Lerp(start,end,s/8f),r1=Mathf.Lerp(start,end,(s+1)/8f),w=.002f+.0012f*Mathf.Sin(j*7+s);
                int k=v.Count;v.Add(Point(r,a-w));v.Add(Point(r,a+w));v.Add(Point(r1,a-w));v.Add(Point(r1,a+w));uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});tri.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
            }
        }
        for(int i=0;i<tri.Count;i+=3){int swap=tri[i+1];tri[i+1]=tri[i+2];tri[i+2]=swap;}
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.SetColors(Enumerable.Repeat(Color.white,v.Count).ToList());m.RecalculateNormals();
        var mat=Mat("Dry strokes on kasa",.27f,5);mat.SetColor("_Ink",new Color(.105f,.104f,.10f));EditorUtility.SetDirty(mat);StaticSurface(parent,"Broken kasa strokes",m,mat);
    }
    static Mesh Tube(string name,int rows,int cols,Func<float,float,Vector3> point)
    {
        var v=new Vector3[(rows+1)*(cols+1)];var uv=new Vector2[v.Length];var tri=new int[rows*cols*6];
        for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++){int k=j*(cols+1)+i;v[k]=point(j/(float)rows,i/(float)cols*Mathf.PI*2);uv[k]=new Vector2(i/(float)cols,j/(float)rows);if(j<rows&&i<cols){int b=(j*cols+i)*6;tri[b]=k;tri[b+1]=k+cols+1;tri[b+2]=k+1;tri[b+3]=k+1;tri[b+4]=k+cols+1;tri[b+5]=k+cols+2;}}
        var m=new Mesh{name=name};m.vertices=v;m.uv=uv;m.triangles=tri;return m;
    }
    static BoneWeight Nearest(Vector3 p)
    {
        int closest=0;float min=float.MaxValue;for(int i=0;i<samples.Length;i++){float d=(samples[i]-p).sqrMagnitude;if(d<min){min=d;closest=i;}}return weights[closest];
    }
    static BoneWeight LegWeight(Vector3 p,int sign)
    {
        // Preserve separate trouser legs even where the cloth overlaps near the crotch.
        string side=sign>0?"l":"r";int thigh=Array.FindIndex(skin.bones,b=>b.name=="thigh_"+side),calf=Array.FindIndex(skin.bones,b=>b.name=="calf_"+side),hip=Array.FindIndex(skin.bones,b=>b.name=="pelvis");
        if(p.y>.86f){float h=Mathf.InverseLerp(.86f,1.02f,p.y);return new BoneWeight{boneIndex0=thigh,weight0=1-h,boneIndex1=hip,weight1=h};}
        float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.66f,p.y));return new BoneWeight{boneIndex0=thigh,weight0=t,boneIndex1=calf,weight1=1-t};
    }
    static void Skinned(string name,Mesh mesh,Material material,Func<Vector3,BoneWeight> weight=null)
    {
        var t=prefab.transform.Find(name);GameObject go=t?t.gameObject:new GameObject(name);go.transform.SetParent(prefab.transform,false);
        var r=go.GetComponent<SkinnedMeshRenderer>();if(!r)r=go.AddComponent<SkinnedMeshRenderer>();
        mesh.boneWeights=mesh.vertices.Select(v=>(weight??Nearest)(v)).ToArray();mesh.bindposes=skin.bones.Select(b=>b.worldToLocalMatrix*go.transform.localToWorldMatrix).ToArray();
        mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        r.sharedMesh=Persist(mesh,name);r.bones=skin.bones;r.rootBone=skin.rootBone;r.sharedMaterials=new[]{material,outline};r.updateWhenOffscreen=true;
    }
}
