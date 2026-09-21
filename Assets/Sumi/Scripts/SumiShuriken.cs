using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sumi
{
    // Runtime presentation for the supplied CC0 shuriken model. All motion is procedural so the
    // anticipation, release and flight remain readable at any frame rate without an animation clip.
    public static class SumiShurikenArt
    {
        const string ResourcePath="Sumi/Projectiles/shuriken_star";
        public static Transform Create(Transform parent,string name,float diameter=.42f)
        {
            var pivot=new GameObject(name).transform;pivot.SetParent(parent,false);
            var source=Resources.Load<GameObject>(ResourcePath);
            if(source)
            {
                var art=Object.Instantiate(source,pivot,false);art.name="Clint Bellanger CC0 shuriken art";
                foreach(var camera in art.GetComponentsInChildren<Camera>(true))camera.enabled=false;
                foreach(var light in art.GetComponentsInChildren<Light>(true))light.enabled=false;
                foreach(var collider in art.GetComponentsInChildren<Collider>(true))Object.Destroy(collider);
                Normalize(art.transform,diameter);
                var steel=Resources.Load<Material>("Ronin/Drawn steel");
                var contour=Resources.Load<Material>("Ronin/Brush contour");
                foreach(var renderer in art.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                    if(steel)renderer.sharedMaterials=contour?new[]{steel,contour}:new[]{steel};
                }
            }
            else ProceduralFallback(pivot,diameter);
            return pivot;
        }
        static void Normalize(Transform art,float diameter)
        {
            var renderers=art.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
            Bounds bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            float span=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));if(span<.0001f)return;
            float scale=diameter/span;art.localScale*=scale;
            art.localPosition-=art.parent.InverseTransformVector(bounds.center-art.parent.position)*scale;
        }
        static void ProceduralFallback(Transform pivot,float diameter)
        {
            for(int i=0;i<4;i++)
            {
                var blade=SumiArt.Shape("Fallback forged point",pivot,SumiArt.Cone,Vector3.zero,new Vector3(diameter*.18f,diameter*.45f,diameter*.055f),Resources.Load<Material>("Ronin/Drawn steel"));
                blade.transform.localRotation=Quaternion.Euler(90,0,i*90);blade.transform.localPosition=Quaternion.Euler(0,0,i*90)*Vector3.up*diameter*.18f;
            }
            var hub=SumiArt.Shape("Shuriken hub",pivot,SumiArt.Cylinder,Vector3.zero,new Vector3(diameter*.18f,diameter*.045f,diameter*.18f),Resources.Load<Material>("Ronin/Drawn steel"));hub.transform.localRotation=Quaternion.Euler(90,0,0);
        }
    }

    public sealed class SumiShurikenCharge:MonoBehaviour
    {
        LineRenderer seal;float born;
        void Start()
        {
            born=Time.unscaledTime;seal=Create("Gathering ink seal",SumiArt.Black,.016f,41);seal.loop=true;
        }
        LineRenderer Create(string name,Material material,float width,int points)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points;line.sharedMaterial=material;line.startWidth=width;line.endWidth=.001f;line.shadowCastingMode=ShadowCastingMode.Off;return line;
        }
        void Update()
        {
            float age=Time.unscaledTime-born,gather=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.48f,age));Draw(seal,.25f,age*1.15f,gather);transform.localScale=Vector3.one*(.94f+gather*.06f);
        }
        static void Draw(LineRenderer line,float radius,float phase,float gather)
        {
            for(int i=0;i<line.positionCount;i++)
            {
                float t=(float)i/(line.positionCount-1),a=(t+phase)*Mathf.PI*2,r=radius*Mathf.Lerp(1.08f,.78f,gather);
                line.SetPosition(i,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));
            }
            line.widthMultiplier=Mathf.Lerp(.15f,1f,gather);
        }
    }

    public sealed class SumiShuriken : MonoBehaviour
    {
        const float Speed=19.5f,Life=1.18f;
        Vector3 direction;float damage,born;Transform art;TrailRenderer inkTrail,paperEdge;
        public void Init(Vector3 origin,Vector3 flight,float hitDamage)
        {
            transform.position=origin;direction=flight.normalized;damage=hitDamage;born=Time.time;
            transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            art=SumiShurikenArt.Create(transform,"Flying shuriken",.46f);art.localRotation=Quaternion.Euler(0,0,17);
            inkTrail=Trail("Torn ink wake",SumiArt.Black,.19f,.004f,.27f);
            paperEdge=Trail("Paper edge wake",Resources.Load<Material>("Ronin/Pale blade edge"),.038f,.001f,.17f);
        }
        TrailRenderer Trail(string name,Material material,float start,float end,float time)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=material;trail.time=time;trail.startWidth=start;trail.endWidth=end;trail.minVertexDistance=.018f;trail.numCornerVertices=2;trail.shadowCastingMode=ShadowCastingMode.Off;return trail;
        }
        void Update()
        {
            float dt=Time.deltaTime;if(dt<=0)return;Vector3 from=transform.position;transform.position+=direction*Speed*dt;
            float age=Time.time-born,arrival=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,age));
            art.localRotation*=Quaternion.Euler(0,0,1440f*dt);art.localScale=Vector3.one*Mathf.Lerp(.70f,1f,arrival);
            foreach(var enemy in SumiEnemy.Active)
            {
                if(!enemy||enemy.dead)continue;Vector3 center=enemy.transform.position+Vector3.up*1.2f;
                if(Vector3.SqrMagnitude(Vector3.Project(center-from,direction)+from-center)<.75f*.75f&&Vector3.Dot(center-from,direction)>=-.3f&&Vector3.Dot(center-transform.position,direction)<=.3f)
                {Impact(enemy,center);return;}
            }
            if(age>=Life){SumiShurikenImpact.Spawn(transform.position,direction,false);Destroy(gameObject);}
        }
        void Impact(SumiEnemy enemy,Vector3 center)
        {
            enemy.TakeHit(damage,direction,SumiHitKind.Shuriken,center,SumiSwordArc.Descending,direction);
            SumiShurikenImpact.Spawn(center,direction,true);SumiTime.HitStop(.035f);
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.13f,direction);
            Destroy(gameObject);
        }
    }

    public sealed class SumiShurikenImpact:MonoBehaviour
    {
        sealed class Stroke{public LineRenderer line;public Vector3 origin,velocity;public float spin,length;}
        readonly List<Stroke> strokes=new List<Stroke>();LineRenderer ring;float born;bool strong;
        public static void Spawn(Vector3 point,Vector3 direction,bool hit)
        {var go=new GameObject(hit?"Shuriken impact blossom":"Shuriken fading flourish");go.transform.position=point;go.AddComponent<SumiShurikenImpact>().Init(direction,hit);}
        void Init(Vector3 direction,bool hit)
        {
            born=Time.unscaledTime;strong=hit;Vector3 normal=direction.sqrMagnitude>.01f?direction.normalized:Vector3.forward;
            ring=Line("Gold impact seal",SumiArt.Gilt,33,hit?.026f:.012f,true);
            Vector3 side=Vector3.Cross(normal,Vector3.up);if(side.sqrMagnitude<.01f)side=Vector3.right;side.Normalize();Vector3 up=Vector3.Cross(side,normal).normalized;
            for(int i=0;i<(hit?8:4);i++)
            {
                float a=i*Mathf.PI*2/(hit?8:4)+Random.Range(-.07f,.07f);Vector3 radial=(side*Mathf.Cos(a)+up*Mathf.Sin(a));
                var stroke=new Stroke{line=Line("Ink splinter",SumiArt.Black,2,.016f,false),origin=transform.position,velocity=radial*Random.Range(1.8f,3.5f)-normal*Random.Range(.1f,.5f),spin=Random.Range(-3f,3f),length=Random.Range(.09f,.24f)};strokes.Add(stroke);
            }
        }
        LineRenderer Line(string name,Material material,int points,float width,bool loop)
        {var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=points;line.loop=loop;line.sharedMaterial=material;line.startWidth=width;line.endWidth=loop?width*.45f:0;line.shadowCastingMode=ShadowCastingMode.Off;return line;}
        void Update()
        {
            float age=Time.unscaledTime-born,t=Mathf.Clamp01(age/(strong?.58f:.34f));Vector3 normal=Camera.main?Camera.main.transform.forward:Vector3.forward;Vector3 right=Camera.main?Camera.main.transform.right:Vector3.right;Vector3 up=Camera.main?Camera.main.transform.up:Vector3.up;
            float radius=Mathf.Lerp(.05f,strong?.72f:.30f,1-Mathf.Pow(1-t,3));for(int i=0;i<ring.positionCount;i++){float a=i*Mathf.PI*2/(ring.positionCount-1);ring.SetPosition(i,transform.position+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius);}ring.widthMultiplier=1-t;
            foreach(var stroke in strokes){Vector3 p=stroke.origin+stroke.velocity*age+Vector3.down*1.8f*age*age;Vector3 tangent=(stroke.velocity+Vector3.Cross(normal,stroke.velocity)*stroke.spin*age).normalized;stroke.line.SetPosition(0,p-tangent*stroke.length*(1-t));stroke.line.SetPosition(1,p+tangent*stroke.length);stroke.line.widthMultiplier=1-t;}
            if(t>=1)Destroy(gameObject);
        }
    }
}
