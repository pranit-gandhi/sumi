using System.Collections.Generic;
using UnityEngine;

namespace Sumi
{
    public enum SumiCombatState { Free,Attack1,Attack2,Attack3,GuardStartup,GuardHeld,GuardRecovery,Dash,DashStrike,HitStun,Dead,HeavyAttack }
    public enum SumiHitKind { ShoulderCut,DashCut,HeavyCut,Finisher,InkDart }
    public enum SumiEnemyState { Observe,Approach,Windup,Strike,Recovery,Recoil,Dead }

    public sealed class SumiInkDart : MonoBehaviour
    {
        Vector3 direction;float damage,expires;
        TrailRenderer stroke;
        public void Init(Vector3 origin,Vector3 flight,float hitDamage)
        {
            transform.position=origin;direction=flight.normalized;damage=hitDamage;expires=Time.time+1.15f;
            var body=GameObject.CreatePrimitive(PrimitiveType.Sphere);body.name="Ink tip";body.transform.SetParent(transform,false);body.transform.localScale=Vector3.one*.16f;
            Destroy(body.GetComponent<Collider>());body.GetComponent<Renderer>().sharedMaterial=SumiArt.Black;
            stroke=gameObject.AddComponent<TrailRenderer>();stroke.sharedMaterial=SumiArt.Black;stroke.time=.16f;stroke.startWidth=.13f;stroke.endWidth=.005f;stroke.minVertexDistance=.025f;
        }
        void Update()
        {
            Vector3 from=transform.position;transform.position+=direction*18f*Time.deltaTime;
            foreach(var enemy in SumiEnemy.Active)
            {
                if(!enemy||enemy.dead)continue;
                Vector3 center=enemy.transform.position+Vector3.up*1.2f;
                if(Vector3.SqrMagnitude(Vector3.Project(center-from, direction)+from-center)<.75f*.75f&&Vector3.Dot(center-from,direction)>=-.3f&&Vector3.Dot(center-transform.position,direction)<=.3f)
                {enemy.TakeHit(damage,direction,SumiHitKind.InkDart);SumiCombatFeedback.Hit(.035f,.12f,center);Destroy(gameObject);return;}
            }
            if(Time.time>=expires)Destroy(gameObject);
        }
    }

    static class SumiEnemyAppearance
    {
        public static void Apply(Transform visual,SumiEnemyKind kind)
        {
            visual.localScale=Vector3.one*(kind==SumiEnemyKind.Oni?1.32f:kind==SumiEnemyKind.Shade?.96f:1.04f);
            foreach(var cloth in visual.GetComponents<SumiClothMotion>())cloth.enabled=false;
            foreach(var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                string n=r.name.ToLowerInvariant();if(n.Contains("kasa")||n.Contains("hat")||n.Contains("haori panels")||n.Contains("loose sleeve")){r.enabled=false;continue;}
                var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);Color baseInk=kind==SumiEnemyKind.Oni?new Color(.20f,.055f,.045f):kind==SumiEnemyKind.Shade?new Color(.055f,.06f,.06f):new Color(.19f,.19f,.18f);block.SetColor("_BaseColor",baseInk);block.SetColor("_Ink",new Color(.012f,.013f,.014f));r.SetPropertyBlock(block);
            }
            var head=visual.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);if(!head)return;
            var mask=new GameObject("Torn paper mask",typeof(MeshFilter),typeof(MeshRenderer));mask.transform.SetParent(head,false);mask.transform.localPosition=new Vector3(0,.015f,.105f);mask.transform.localRotation=Quaternion.Euler(0,0,0);mask.transform.localScale=new Vector3(.82f,1,1);
            mask.GetComponent<MeshFilter>().sharedMesh=MaskMesh();var maskRenderer=mask.GetComponent<MeshRenderer>();maskRenderer.sharedMaterial=Resources.Load<Material>("Ronin/Brush cotton");var maskBlock=new MaterialPropertyBlock();maskBlock.SetColor("_BaseColor",new Color(.63f,.62f,.58f));maskRenderer.SetPropertyBlock(maskBlock);
            EyeStroke(mask.transform,new Vector3(-.065f,.035f,-.006f),new Vector3(-.018f,.018f,-.006f));EyeStroke(mask.transform,new Vector3(.018f,.018f,-.006f),new Vector3(.065f,.035f,-.006f));
            if(kind==SumiEnemyKind.Oni)
            {
                Horn(head,new Vector3(-.10f,.17f,.015f),-24);Horn(head,new Vector3(.10f,.17f,.015f),24);
                var shoulder=SumiArt.Box("Oni ink mantle",visual,new Vector3(0,1.43f,-.04f),new Vector3(.92f,.12f,.28f),Resources.Load<Material>("Ronin/Soot silhouette"));shoulder.transform.localRotation=Quaternion.Euler(4,0,0);
            }
            var scarf=visual.gameObject.AddComponent<SumiEnemyScarf>();scarf.anchor=visual.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftShoulder);
        }
        static void Horn(Transform head,Vector3 pos,float roll){var go=SumiArt.Shape("Oni brush horn",head,SumiArt.Lathe("OniHorn",new[]{.10f,.075f,.02f,0f},new[]{-.5f,-.12f,.39f,.5f},6),pos,new Vector3(1,.72f,1),Resources.Load<Material>("Ronin/Soot silhouette"));go.transform.localRotation=Quaternion.Euler(0,0,roll);}
        static Mesh MaskMesh()
        {
            var m=new Mesh{name="Irregular retainer mask"};m.vertices=new[]{new Vector3(0,.20f,0),new Vector3(-.12f,.14f,0),new Vector3(-.14f,.01f,0),new Vector3(-.09f,-.17f,0),new Vector3(0,-.22f,0),new Vector3(.10f,-.16f,0),new Vector3(.135f,.02f,0),new Vector3(.11f,.15f,0),new Vector3(0,0,-.012f)};m.triangles=new[]{8,0,1,8,1,2,8,2,3,8,3,4,8,4,5,8,5,6,8,6,7,8,7,0};m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static void EyeStroke(Transform parent,Vector3 a,Vector3 b)
        {
            var go=new GameObject("Mask eye stroke");go.transform.SetParent(parent,false);var l=go.AddComponent<LineRenderer>();l.useWorldSpace=false;l.sharedMaterial=Resources.Load<Material>("Ronin/Soot silhouette");l.positionCount=2;l.SetPosition(0,a);l.SetPosition(1,b);l.startWidth=.012f;l.endWidth=.006f;
        }
        public static void PrepareSplit(GameObject go,float cutY,float side)
        {
            foreach(var pose in go.GetComponentsInChildren<SumiEnemyBlade>())pose.enabled=false;
            foreach(var trail in go.GetComponentsInChildren<TrailRenderer>()){trail.emitting=false;trail.Clear();}
            foreach(var a in go.GetComponentsInChildren<Animator>())a.enabled=false;foreach(var c in go.GetComponentsInChildren<Collider>())Object.Destroy(c);foreach(var cloth in go.GetComponentsInChildren<SumiClothMotion>())cloth.enabled=false;foreach(var scarf in go.GetComponentsInChildren<SumiEnemyScarf>())scarf.enabled=false;
            foreach(var r in go.GetComponentsInChildren<Renderer>()){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetFloat("_CutEnabled",1);block.SetFloat("_CutY",cutY);block.SetFloat("_CutSide",side);r.SetPropertyBlock(block);}
        }
    }

    // A restrained pair of calligraphic scarf strokes gives the retainer a narrow,
    // directional silhouette while remaining cheaper and more stable than Unity Cloth.
    public sealed class SumiEnemyScarf:MonoBehaviour
    {
        public Transform anchor;
        readonly Vector3[][] points={new Vector3[6],new Vector3[6]};LineRenderer[] lines;bool ready;
        void Start()
        {
            lines=new LineRenderer[2];
            for(int k=0;k<2;k++)
            {
                var go=new GameObject(k==0?"Long ink scarf":"Broken ink scarf");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();lines[k]=line;line.useWorldSpace=true;line.positionCount=6;line.sharedMaterial=Resources.Load<Material>("Ronin/Soot silhouette");line.startWidth=k==0?.14f:.075f;line.endWidth=.012f;line.numCornerVertices=1;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        void LateUpdate()
        {
            if(!anchor||lines==null)return;Vector3 root=anchor.position+transform.up*.07f-transform.forward*.035f;
            if(!ready){for(int k=0;k<2;k++)for(int i=0;i<6;i++)points[k][i]=root-transform.forward*i*.13f-transform.up*i*.025f;ready=true;}
            for(int k=0;k<2;k++)
            {
                points[k][0]=root+transform.right*(k==0?-.035f:.035f);
                for(int i=1;i<6;i++)
                {
                    float t=i/5f;Vector3 wanted=points[k][i-1]-transform.forward*(.12f+t*.045f)-transform.up*(.018f+t*.018f)+transform.right*Mathf.Sin(Time.time*2.1f+i*1.7f+k)*.018f*t;
                    points[k][i]=Vector3.Lerp(points[k][i],wanted,1-Mathf.Exp(-(13f-i)*Time.deltaTime));
                    points[k][i]=points[k][i-1]+Vector3.ClampMagnitude(points[k][i]-points[k][i-1],.14f+t*.065f);
                }
                lines[k].SetPositions(points[k]);
            }
        }
    }

    public sealed class SumiInkSplitPiece:MonoBehaviour
    {
        Vector3 velocity,startScale;float duration,age;
        public void Setup(Vector3 v,float d){velocity=v;duration=d;startScale=transform.localScale;}
        void Update(){age+=Time.deltaTime;transform.position+=velocity*Time.deltaTime;transform.rotation*=Quaternion.Euler(0,0,velocity.x*32*Time.deltaTime);transform.localScale=startScale*Mathf.Lerp(1,.82f,age/duration);}
    }

    public static class SumiCombatFeedback
    {
        static float until;static LineRenderer[] marks;static float[] expire;static int cursor;static Material ink,gold;
        static AudioSource audioSource;static AudioClip swing,fastSwing,impact,heavyImpact,parry;
        public static void Hit(float stop,float kick){Hit(stop,kick,Vector3.zero);}
        public static void Hit(float stop,float kick,Vector3 at){Freeze(stop);Play(kick>.18f?4:1);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(kick);if(at!=Vector3.zero)Mark(at,kick>.18f,false);}
        public static void Block(Vector3 at){Freeze(.025f);Play(2);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.08f);Mark(at,false,false);}
        public static void Parry(Vector3 at,Vector3 axis){Freeze(.075f);Play(2);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.30f);Mark(at,true,true,axis);}
        public static void WaistCut(SumiEnemy enemy,Vector3 at){Mark(new Vector3(enemy.transform.position.x,enemy.transform.position.y+.94f,enemy.transform.position.z),true,false,enemy.transform.right);}
        public static void Swing(bool fast){Play(fast?3:0);}
        public static void Dash(Vector3 direction){if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Sway(.17f,direction);}
        public static void EnemySwing(bool fast){Play(fast?3:0);}
        public static void DashStroke(Vector3 from,Vector3 to)
        {
            Ensure();int index=cursor;cursor=(cursor+1)%marks.Length;var line=marks[index];Vector3 side=Vector3.Cross(Vector3.up,(to-from).normalized)*.08f;
            line.sharedMaterial=ink;line.startWidth=.12f;line.endWidth=.006f;line.SetPosition(0,from+Vector3.up*.055f-side);line.SetPosition(1,Vector3.Lerp(from,to,.53f)+Vector3.up*.045f+side);line.SetPosition(2,to+Vector3.up*.035f);line.enabled=true;expire[index]=Time.unscaledTime+.45f;
            int second=cursor;cursor=(cursor+1)%marks.Length;var echo=marks[second];echo.sharedMaterial=ink;echo.startWidth=.025f;echo.endWidth=.003f;Vector3 spread=side*4f;echo.SetPosition(0,from+Vector3.up*.08f+spread);echo.SetPosition(1,Vector3.Lerp(from,to,.57f)+Vector3.up*.06f+spread);echo.SetPosition(2,to+Vector3.up*.04f+spread);echo.enabled=true;expire[second]=Time.unscaledTime+.34f;
        }
        static void Freeze(float stop){if(stop<=0)return;until=Mathf.Max(until,Time.unscaledTime+stop);SumiTime.HitStop(stop);}
        static void Ensure()
        {
            if(marks!=null)return;marks=new LineRenderer[12];expire=new float[12];ink=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ink.color=new Color(.055f,.052f,.045f);gold=new Material(Shader.Find("Universal Render Pipeline/Unlit"));gold.color=new Color(.78f,.58f,.18f);
            for(int i=0;i<marks.Length;i++){var go=new GameObject("Pooled calligraphy contact");marks[i]=go.AddComponent<LineRenderer>();marks[i].useWorldSpace=true;marks[i].positionCount=3;marks[i].enabled=false;marks[i].textureMode=LineTextureMode.Stretch;}
        }
        static void EnsureAudio()
        {
            if(audioSource)return;var go=new GameObject("Sumi combat sound");audioSource=go.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;audioSource.volume=.65f;
            swing=Resources.Load<AudioClip>("Sumi/Audio/SwordSwing");fastSwing=Resources.Load<AudioClip>("Sumi/Audio/FastSwing");
            impact=Resources.Load<AudioClip>("Sumi/Audio/SwordHit");heavyImpact=Resources.Load<AudioClip>("Sumi/Audio/HeavyHit");
            parry=Resources.Load<AudioClip>("Sumi/Audio/SwordParry");
        }
        static void Play(int kind){EnsureAudio();var clip=kind==0?swing:kind==1?impact:kind==2?parry:kind==3?fastSwing:heavyImpact;if(clip)audioSource.PlayOneShot(clip,kind==2?.85f:(kind==1||kind==4)?.9f:.75f);}
        static void Mark(Vector3 at,bool strong,bool gilded,Vector3 axis=default)
        {
            Ensure();int index=cursor;cursor=(cursor+1)%marks.Length;var line=marks[index];float d=strong?.62f:.27f;if(axis.sqrMagnitude<.01f)axis=Vector3.right;axis.Normalize();
            line.sharedMaterial=gilded?gold:ink;line.startWidth=strong?.045f:.025f;line.endWidth=.006f;line.SetPosition(0,at-axis*d);line.SetPosition(1,at+Vector3.up*(strong?.12f:.07f));line.SetPosition(2,at+axis*d);line.enabled=true;expire[index]=Time.unscaledTime+(strong?.20f:.11f);
        }
        public static void Tick(){SumiTime.Tick();if(marks!=null)for(int i=0;i<marks.Length;i++)if(marks[i].enabled&&Time.unscaledTime>=expire[i])marks[i].enabled=false;}
        public static void Clear(){SumiTime.Reset();until=0;if(marks!=null)for(int i=0;i<marks.Length;i++)if(marks[i])Object.Destroy(marks[i].gameObject);marks=null;if(ink)Object.Destroy(ink);if(gold)Object.Destroy(gold);if(audioSource)Object.Destroy(audioSource.gameObject);audioSource=null;swing=fastSwing=impact=heavyImpact=parry=null;}
    }
}
