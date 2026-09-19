using UnityEngine;
using UnityEngine.Rendering;
namespace Sumi
{
    [ExecuteAlways]
    public class SumiDrawing : MonoBehaviour
    {
        public Texture2D atlas;
        [Range(0,7)] public int frame;
        public bool billboard=true;
        public float width=3,height=3,wind=.5f,strength=1,pivot=.045f;
        public Color ink=new Color(.025f,.025f,.025f,1);
        [Range(10,64)] public int subdivisions=10;
        MeshRenderer surface;MaterialPropertyBlock block;
        Mesh ownedMesh;
        public void Setup(Texture2D texture,int index,float w,float h)
        {
            atlas=texture;frame=index;width=w;height=h;
            var filter=GetComponent<MeshFilter>();if(!filter)filter=gameObject.AddComponent<MeshFilter>();
            var mesh=new Mesh{name="Paper drawing grid"};
            if(Application.isPlaying)ownedMesh=mesh;
            int n=subdivisions;var v=new Vector3[(n+1)*(n+1)];var uv=new Vector2[v.Length];var tri=new int[n*n*6];
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++){int a=y*(n+1)+x;uv[a]=new Vector2(x/(float)n,y/(float)n);v[a]=new Vector3((uv[a].x-.5f)*width,(uv[a].y-pivot)*height,0);}
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=y*(n+1)+x,b=(y*n+x)*6;tri[b]=a;tri[b+1]=a+n+1;tri[b+2]=a+1;tri[b+3]=a+1;tri[b+4]=a+n+1;tri[b+5]=a+n+2;}
            mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateBounds();mesh.RecalculateNormals();filter.sharedMesh=mesh;
            surface=GetComponent<MeshRenderer>();if(!surface)surface=gameObject.AddComponent<MeshRenderer>();
            surface.sharedMaterial=Resources.Load<Material>("Sumi/DrawnInk");surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
            Apply();
        }
        public void Apply()
        {
            if(!surface)surface=GetComponent<MeshRenderer>();if(!surface)return;if(block==null)block=new MaterialPropertyBlock();
            block.SetTexture("_BaseMap",atlas);block.SetColor("_BaseColor",ink);block.SetFloat("_Strength",strength);block.SetFloat("_Wind",wind);
            block.SetVector("_Frame",new Vector4(.25f,.5f,(frame%4)*.25f,frame<4?.5f:0));surface.SetPropertyBlock(block);
        }
        void OnEnable(){Apply();}
        void OnDestroy(){if(ownedMesh)Destroy(ownedMesh);}
        void LateUpdate()
        {
            if(!billboard&&wind==0)return;
            if(billboard&&Camera.main){var direction=Camera.main.transform.forward;direction.y=0;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);}
            Apply();
        }
    }
}
