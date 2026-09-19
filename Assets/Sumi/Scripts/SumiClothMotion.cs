using System.Collections.Generic;
using UnityEngine;

namespace Sumi
{
    // Small position-based fabric solver. Anchors follow the animated skeleton;
    // the free hem retains world-space momentum through steps, turns and stops.
    [DefaultExecutionOrder(80)]
    public class SumiClothMotion:MonoBehaviour
    {
        public Material material;
        public float maxDisplacement {get;private set;}
        const int Panels=6,Rows=16,Cols=9,Count=Rows*Cols;
        Mesh mesh;
        Vector3[] current,previous,rest,vertices;
        Vector2[] uv;
        Transform[] anchors;
        Transform hips,leftThigh,rightThigh;
        readonly List<Vector2Int> links=new List<Vector2Int>();
        float[] lengths;
        Vector3 lastRoot;
        float accumulator;
        void Start()
        {
            var animator=GetComponent<Animator>();
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            leftThigh=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);rightThigh=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
            current=new Vector3[Panels*Count];previous=new Vector3[current.Length];rest=new Vector3[current.Length];vertices=new Vector3[current.Length];uv=new Vector2[current.Length];anchors=new Transform[Panels];
            var triangles=new List<int>();
            for(int p=0;p<Panels;p++)
            {
                bool front=p==5;anchors[p]=front?hips:chest;
                float angle=front?2.6f:Mathf.Lerp(-1.67f,1.67f,p/4f);
                float length=front?.61f:1.04f+.075f*Mathf.Sin(p*2.7f);
                for(int row=0;row<Rows;row++)for(int col=0;col<Cols;col++)
                {
                    float t=row/(Rows-1f),u=col/(Cols-1f);int i=p*Count+row*Cols+col;
                    float a=angle+(u-.5f)*(front?.72f:.87f);
                    float radius=(front?.25f:.28f)+t*t*.14f+Mathf.Sin(t*6+u*8+p)*.017f*t;
                    float y=(front?1.0f:1.48f-Mathf.Abs(Mathf.Sin(a))*.07f)-length*t;
                    y-=Mathf.Pow(t,6)*(.048f*Mathf.Sin(col*2.3f+p*13)+.026f*Mathf.Sin(col*5.7f+p));
                    Vector3 local=new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius*.88f+.035f);
                    Vector3 world=transform.TransformPoint(local);
                    current[i]=previous[i]=world;rest[i]=anchors[p].InverseTransformPoint(world);
                    uv[i]=new Vector2((u+p)*.37f,t);
                    if(row<Rows-1&&col<Cols-1)triangles.AddRange(new[]{i,i+Cols,i+1,i+1,i+Cols,i+Cols+1});
                    if(row>0)links.Add(new Vector2Int(i-Cols,i));
                    if(col>0)links.Add(new Vector2Int(i-1,i));
                    if(row>0&&col>0)links.Add(new Vector2Int(i-Cols-1,i));
                    if(row>1)links.Add(new Vector2Int(i-Cols*2,i));
                }
            }
            // Join the coat across the shoulder/back; only the lower hem separates
            // into ragged panels. This retains a garment silhouette during motion.
            for(int p=0;p<4;p++)for(int row=0;row<Rows-5;row++)
            {
                int a=p*Count+row*Cols+Cols-1,b=(p+1)*Count+row*Cols;
                links.Add(new Vector2Int(a,b));
                if(row<Rows-6)triangles.AddRange(new[]{a,a+Cols,b,b,a+Cols,b+Cols});
            }
            lengths=new float[links.Count];for(int i=0;i<links.Count;i++)lengths[i]=Vector3.Distance(current[links[i].x],current[links[i].y]);
            var go=new GameObject("Simulated haori panels",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            mesh=new Mesh{name="Flexible layered cotton"};mesh.MarkDynamic();mesh.vertices=new Vector3[current.Length];mesh.uv=uv;mesh.SetTriangles(triangles,0);
            var colors=new Color[current.Length];for(int i=0;i<colors.Length;i++)colors[i]=Color.Lerp(new Color(.72f,.72f,.72f),Color.white,(i%Cols)/(Cols-1f));mesh.colors=colors;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            lastRoot=transform.position;
        }
        Vector3 Target(int i)=>anchors[i/Count].TransformPoint(rest[i]);
        bool Pinned(int i)=>i%Count<Cols;
        void LateUpdate()
        {
            if(mesh==null||Time.deltaTime<=0)return;
            if((transform.position-lastRoot).sqrMagnitude>4)for(int i=0;i<current.Length;i++)current[i]=previous[i]=Target(i);
            lastRoot=transform.position;
            accumulator=Mathf.Min(accumulator+Time.deltaTime,1f/15);
            const float dt=1f/90;
            while(accumulator>=dt)
            {
                accumulator-=dt;
                Vector3 wind=new Vector3(.20f+Mathf.Sin(Time.time*.8f)*.12f,.015f,Mathf.Sin(Time.time*.65f)*.10f);
                for(int i=0;i<current.Length;i++)
                {
                    if(Pinned(i)){current[i]=previous[i]=Target(i);continue;}
                    var old=current[i];float t=(i%Count/Cols)/(Rows-1f);
                    Vector3 force=Vector3.down*1.8f+wind*(.5f+t*2);
                    current[i]+=(current[i]-previous[i])*.93f+force*dt*dt;
                    // A weak shape force supplies fabric bending resistance, not rigid following.
                    current[i]+=(Target(i)-current[i])*dt*(5.5f-t*3.0f);
                    previous[i]=old;
                }
                for(int pass=0;pass<5;pass++)
                {
                    for(int j=0;j<links.Count;j++)
                    {
                        int a=links[j].x,b=links[j].y;Vector3 d=current[b]-current[a];float len=d.magnitude;if(len<.00001f)continue;
                        Vector3 correction=d*(1-lengths[j]/len);
                        if(Pinned(a)){if(!Pinned(b))current[b]-=correction;}
                        else if(Pinned(b))current[a]+=correction;
                        else {current[a]+=correction*.5f;current[b]-=correction*.5f;}
                    }
                    for(int i=0;i<current.Length;i++)if(!Pinned(i))
                    {
                        Collide(ref current[i],hips.position+transform.up*.12f,.235f);
                        Collide(ref current[i],leftThigh.position-transform.up*.16f,.155f);
                        Collide(ref current[i],rightThigh.position-transform.up*.16f,.155f);
                        if(current[i].y<transform.position.y+.045f)current[i].y=transform.position.y+.045f;
                        float t=(i%Count/Cols)/(Rows-1f);
                        Vector3 goal=Target(i);float limit=(.025f+t*t*.34f)*transform.lossyScale.x;
                        var offset=current[i]-goal;
                        if(offset.sqrMagnitude>limit*limit){current[i]=goal+offset.normalized*limit;previous[i]=Vector3.Lerp(previous[i],current[i],.35f);}
                    }
                }
            }
            maxDisplacement=0;
            for(int i=0;i<current.Length;i++){vertices[i]=transform.InverseTransformPoint(current[i]);maxDisplacement=Mathf.Max(maxDisplacement,Vector3.Distance(current[i],Target(i)));}
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        void Collide(ref Vector3 p,Vector3 center,float radius)
        {radius*=transform.lossyScale.x;Vector3 d=p-center;float length=d.magnitude;if(length<radius&&length>.001f)p=center+d*(radius/length);}
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
