using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sumi;

public static partial class SumiHumanoidBuild
{
    static Material Pigment(string name,float value,float density=120)
    {
        string path="Assets/Sumi/Resources/Ronin/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Sumi/Ronin Ink"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",new Color(value,value*.99f,value*.96f));mat.SetFloat("_Density",density);EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        string path="Assets/Sumi/Resources/Ronin/"+name+".asset";
        mesh.name=name;var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);return saved;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void Dress(GameObject obj)
    {
        var paper=Pigment("Worn cotton",.66f);var ink=Pigment("Charcoal cloth",.34f,90);var steel=Pigment("Drawn steel",.77f,70);
        var animator=obj.GetComponent<Animator>();
        foreach(var r in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(r.name!="SuperHero_Male"){r.sharedMaterial=ink;continue;}
            var mesh=UnityEngine.Object.Instantiate(r.sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;var colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=obj.transform.InverseTransformPoint(r.transform.TransformPoint(vertices[i]));
                Vector3 n=obj.transform.InverseTransformDirection(r.transform.TransformDirection(normals[i]));
                float x=Mathf.Abs(p.x),y=p.y;
                float amount=0;float tint=.65f;
                if(y>.32f&&y<1.53f)
                {
                    // Clothing is the skinned surface, not rigid objects attached over joints.
                    if(y<.96f){amount=.022f+.018f*Mathf.Sin(Mathf.InverseLerp(.32f,.96f,y)*Mathf.PI);tint=.30f;}
                    else if(x<.27f){amount=.025f;tint=.55f;}
                    else if(x<.58f){amount=.026f;tint=.48f;}
                    else if(x<.73f){amount=.025f;tint=.55f;}
                    else {amount=.005f;tint=.24f;}
                    float folds=Mathf.Sin(Mathf.Atan2(p.z,x-.11f)*14+y*5)*.006f;
                    if(y<.96f||x>.25f)amount+=folds;
                    p+=n*amount;
                    if(x>.28f&&x<.56f&&p.y<1.45f)p.y-=.06f;
                }
                if(y>=1.53f)tint=.11f;
                if(y<.32f)tint=y>.11f?.50f:.12f;
                vertices[i]=r.transform.InverseTransformPoint(obj.transform.TransformPoint(p));colors[i]=new Color(tint,tint,tint,1);
            }
            mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();r.sharedMesh=SaveMesh(mesh,"Skinned kimono and hakama");r.sharedMaterial=paper;r.updateWhenOffscreen=true;
        }
        var head=animator.GetBoneTransform(HumanBodyBones.Head);
        var hat=Surface(obj.transform,"Woven kasa",HatMesh(),paper);
        hat.transform.localPosition=new Vector3(0,1.765f,.02f);hat.transform.SetParent(head,true);
        Surface(hat.transform,"Kasa woven ribs",HatRibs(),Pigment("Pen stroke",.055f,32));
        // Raised crossed lapels and obi remain skinned to the chest/pelvis.
        var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        Ribbon(obj.transform,chest,"Left lapel",new Vector3(-.19f,1.46f,-.155f),new Vector3(.09f,1.02f,-.18f),.06f,paper);
        Ribbon(obj.transform,chest,"Right lapel",new Vector3(.19f,1.46f,-.155f),new Vector3(-.09f,1.02f,-.18f),.045f,ink);
        var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
        var belt=Surface(obj.transform,"Obi knot and belt",Ring(.225f,.16f,.13f),ink);belt.transform.localPosition=new Vector3(0,1.0f,.035f);belt.transform.SetParent(hips,true);
        var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
        var katana=new GameObject("Katana").transform;katana.SetParent(hand,false);katana.localRotation=Quaternion.identity;
        Surface(katana,"Curved blade",Blade(),steel);
        var grip=Surface(katana,"Bound grip",Ring(.023f,.025f,.22f),ink);grip.transform.localPosition=new Vector3(0,-.1f,0);
        var guard=Surface(katana,"Tsuba",Ring(.085f,.058f,.017f),ink);guard.transform.localPosition=new Vector3(0,.02f,0);
        var fabric=obj.AddComponent<SumiClothMotion>();fabric.material=ink;
        obj.transform.localScale=Vector3.one*1.18f;
    }
    static GameObject Surface(Transform parent,string name,Mesh mesh,Material mat)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,name);go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static Mesh HatMesh()
    {
        const int sides=96,rings=14;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
        {
            float t=j/(float)rings,a=i/(float)sides*Mathf.PI*2;
            float r=.57f*t;float y=.23f*Mathf.Pow(1-t,1.3f)+Mathf.Sin(a*32)*.004f*t+Mathf.Sin(a*3)*.007f*t*t;
            v.Add(new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r));uv.Add(new Vector2(i/(float)sides,t));
            if(j<rings&&i<sides){int k=j*(sides+1)+i;tri.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
        }
        return MeshOf(v,uv,tri);
    }
    static Mesh HatRibs()
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int rib=0;rib<64;rib++)for(int j=0;j<12;j++)
        {
            float t0=.05f+j*.95f/12,t1=.05f+(j+1)*.95f/12,a=rib/64f*Mathf.PI*2;
            Vector3 P(float t,float angle){float r=.57f*t;return new Vector3(Mathf.Cos(angle)*r,.232f*Mathf.Pow(1-t,1.3f)+Mathf.Sin(angle*32)*.004f*t+Mathf.Sin(angle*3)*.007f*t*t+.0018f,Mathf.Sin(angle)*r);}
            int k=v.Count;v.Add(P(t0,a-.002f));v.Add(P(t0,a+.002f));v.Add(P(t1,a-.002f));v.Add(P(t1,a+.002f));uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});tri.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
        }
        return MeshOf(v,uv,tri);
    }
    static Mesh Ring(float rx,float rz,float height)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int y=0;y<2;y++)for(int i=0;i<=48;i++)
        {float a=i/48f*Mathf.PI*2;v.Add(new Vector3(Mathf.Cos(a)*rx,(y-.5f)*height,Mathf.Sin(a)*rz));uv.Add(new Vector2(i/48f,y));if(y==0&&i<48){tri.AddRange(new[]{i,i+49,i+1,i+1,i+49,i+50});}}
        return MeshOf(v,uv,tri);
    }
    static Mesh Blade()
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int j=0;j<=24;j++)for(int side=0;side<2;side++)
        {float t=j/24f;v.Add(new Vector3(t*t*.065f+(side-.5f)*.038f*(1-Mathf.Pow(t,12)),.03f+t*.94f,0));uv.Add(new Vector2(side,t));if(j<24&&side==0){int k=j*2;tri.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}}
        return MeshOf(v,uv,tri);
    }
    static void Ribbon(Transform root,Transform bone,string name,Vector3 a,Vector3 b,float width,Material mat)
    {
        var v=new List<Vector3>{a+Vector3.left*width*.5f,a+Vector3.right*width*.5f,b+Vector3.left*width*.5f,b+Vector3.right*width*.5f};
        var go=Surface(root,name,MeshOf(v,new List<Vector2>{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},new List<int>{0,2,1,1,2,3}),mat);go.transform.SetParent(bone,true);
    }
    static Mesh MeshOf(List<Vector3> v,List<Vector2> uv,List<int> tri)
    {var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.SetColors(Enumerable.Repeat(Color.white,v.Count).ToList());m.RecalculateNormals();m.RecalculateBounds();return m;}
}
