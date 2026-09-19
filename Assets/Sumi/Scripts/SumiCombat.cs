using System.Collections.Generic;
using UnityEngine;

namespace Sumi
{
    public enum SumiCombatState { Free,Attack1,Attack2,Attack3,GuardStartup,GuardHeld,GuardRecovery,Dash,DashStrike,HitStun,Dead,HeavyAttack }
    public enum SumiHitKind { ShoulderCut,DashCut }
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
            foreach(var enemy in FindObjectsByType<SumiEnemy>(FindObjectsSortMode.None))
            {
                if(!enemy||enemy.dead)continue;
                Vector3 center=enemy.transform.position+Vector3.up*1.2f;
                if(Vector3.SqrMagnitude(Vector3.Project(center-from, direction)+from-center)<.75f*.75f&&Vector3.Dot(center-from,direction)>=-.3f&&Vector3.Dot(center-transform.position,direction)<=.3f)
                {enemy.TakeHit(damage,direction,SumiHitKind.ShoulderCut);SumiCombatFeedback.Hit(.035f,.12f,center);Destroy(gameObject);return;}
            }
            if(Time.time>=expires)Destroy(gameObject);
        }
    }

    public sealed class SumiEnemy : MonoBehaviour
    {
        public float health=66,maxHealth=66;public bool dead;public SumiEnemyState state;public SumiEnemyKind kind;
        public Transform visual;public Animator animator;
        SumiPlayer player;SumiRunDirector director;CharacterController body;Vector3 velocity,smoothVelocity,attackDirection;float elapsed,nextAttackAt,lastLunge;bool struck,hasToken;
        Transform bladeBase,bladeTip;static readonly int Speed=Animator.StringToHash("Speed");

        public void Init(SumiPlayer p){Init(p,SumiEnemyKind.Retainer,SumiGame.I?SumiGame.I.run:null);}
        public void Init(SumiPlayer p,SumiEnemyKind enemyKind,SumiRunDirector run)
        {
            player=p;director=run;kind=enemyKind;name=kind==SumiEnemyKind.Oni?"Painted Oni":kind==SumiEnemyKind.Shade?"Ink Shade":"Ashen masked retainer";
            maxHealth=health=kind==SumiEnemyKind.Oni?340:kind==SumiEnemyKind.Shade?54:66;
            body=gameObject.AddComponent<CharacterController>();body.height=kind==SumiEnemyKind.Oni?2.35f:1.85f;body.radius=kind==SumiEnemyKind.Oni?.48f:.36f;body.center=new Vector3(0,body.height*.51f,0);body.stepOffset=.18f;
            visual=Instantiate(Resources.Load<GameObject>("Ronin/Humanoid"),transform,false).transform;visual.name="Masked retainer visual";
            animator=visual.GetComponent<Animator>();SumiEnemyAppearance.Apply(visual,kind);
            var blade=visual.GetComponentsInChildren<MeshFilter>(true);MeshFilter source=null;foreach(var f in blade)if(f.name=="Curved blade"){source=f;break;}
            var parent=source?source.transform:animator.GetBoneTransform(HumanBodyBones.RightHand);var bounds=source&&source.sharedMesh?source.sharedMesh.bounds:new Bounds(Vector3.zero,new Vector3(.04f,.95f,.04f));
            bladeBase=new GameObject("Enemy blade base").transform;bladeBase.SetParent(parent,false);bladeBase.localPosition=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            bladeTip=new GameObject("Enemy blade tip").transform;bladeTip.SetParent(parent,false);bladeTip.localPosition=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
            nextAttackAt=Time.time+(kind==SumiEnemyKind.Shade?.55f:1.05f);Enter(SumiEnemyState.Observe);
        }

        void Update()
        {
            if(dead||!player||!director||!director.CombatActive)return;float dt=Time.deltaTime;elapsed+=dt;
            Vector3 toPlayer=player.transform.position-transform.position;toPlayer.y=0;float range=toPlayer.magnitude;Vector3 dir=range>.001f?toPlayer/range:transform.forward;
            if(state!=SumiEnemyState.Dead)Face(state==SumiEnemyState.Strike?attackDirection:dir,dt,state==SumiEnemyState.Strike?15f:9f);
            switch(state)
            {
                case SumiEnemyState.Observe:
                    SmoothMove(Vector3.zero,dt);if(elapsed>.38f)Enter(SumiEnemyState.Approach);break;
                case SumiEnemyState.Approach:
                    int slot=director.OrbitIndex(this);float desiredRange=kind==SumiEnemyKind.Oni?2.45f:2.65f+(slot%2)*.34f;Vector3 tangent=Vector3.Cross(Vector3.up,dir)*((slot&1)==0?1:-1);
                    Vector3 wanted=range>desiredRange+.22f?dir*(kind==SumiEnemyKind.Shade?1.82f:1.48f):range<desiredRange-.34f?-dir*.82f:tangent*.48f;
                    foreach(var other in FindObjectsByType<SumiEnemy>(FindObjectsSortMode.None)){if(other==this||other.dead)continue;Vector3 away=transform.position-other.transform.position;away.y=0;if(away.sqrMagnitude<1.15f)wanted+=away.normalized*(1.15f-away.magnitude)*1.5f;}
                    SmoothMove(wanted,dt);
                    if(range<=2.85f&&Time.time>=nextAttackAt&&director.RequestAttack(this)){hasToken=true;Enter(SumiEnemyState.Windup);}break;
                case SumiEnemyState.Windup:
                    SmoothMove(Vector3.zero,dt);if(elapsed>=(kind==SumiEnemyKind.Oni?.68f:kind==SumiEnemyKind.Shade?.30f:.46f)){attackDirection=dir;Enter(SumiEnemyState.Strike);}break;
                case SumiEnemyState.Strike:
                    float strikeDuration=kind==SumiEnemyKind.Oni?.40f:.31f,t=Mathf.Clamp01(elapsed/strikeDuration),eased=t*t*(3-2*t),delta=Mathf.Max(0,eased-lastLunge)*(kind==SumiEnemyKind.Oni?.65f:.42f);lastLunge=eased;body.Move(attackDirection*delta);
                    if(!struck&&elapsed>=.09f){struck=true;Vector3 contact=Vector3.Lerp(bladeBase.position,bladeTip.position,.62f),to=player.transform.position-transform.position;to.y=0;if(to.magnitude<(kind==SumiEnemyKind.Oni?3f:2.35f)&&Vector3.Dot(transform.forward,to.normalized)>.42f&&!Physics.Linecast(transform.position+Vector3.up,player.transform.position+Vector3.up,1<<8))player.combat.ReceiveEnemyHit(kind==SumiEnemyKind.Oni?21:kind==SumiEnemyKind.Shade?12:15,kind==SumiEnemyKind.Oni?38:27,this,contact);}
                    if(elapsed>=strikeDuration)Enter(SumiEnemyState.Recovery);break;
                case SumiEnemyState.Recovery:
                    SmoothMove(Vector3.zero,dt);if(elapsed>=.48f){ReleaseToken();nextAttackAt=Time.time+Random.Range(kind==SumiEnemyKind.Shade?.65f:.85f,kind==SumiEnemyKind.Oni?1.15f:1.35f);Enter(SumiEnemyState.Approach);}break;
                case SumiEnemyState.Recoil:
                    SmoothMove(-dir*.42f,dt);if(elapsed>=.34f){ReleaseToken();Enter(SumiEnemyState.Approach);}break;
            }
            animator.SetFloat(Speed,Mathf.Min(velocity.magnitude,1.8f),.18f,dt);
        }

        void Enter(SumiEnemyState next)
        {
            state=next;elapsed=0;lastLunge=0;struck=false;
            if(next==SumiEnemyState.Approach||next==SumiEnemyState.Observe)animator.CrossFadeInFixedTime("Locomotion",.14f);
            else if(next==SumiEnemyState.Windup)animator.CrossFadeInFixedTime("Attack1",.07f);
            else if(next==SumiEnemyState.Recoil)animator.CrossFadeInFixedTime("Hit",.045f);
            else if(next==SumiEnemyState.Recovery)animator.CrossFadeInFixedTime("Locomotion",.11f);
        }
        void SmoothMove(Vector3 wanted,float dt)
        {
            velocity=Vector3.SmoothDamp(velocity,wanted,ref smoothVelocity,wanted.sqrMagnitude>.01f?.14f:.19f,3f,dt);
            body.Move((velocity+Vector3.down*2f)*dt);
        }
        void Face(Vector3 dir,float dt,float sharpness){if(dir.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(dir),1-Mathf.Exp(-sharpness*dt));}

        public void TakeHit(float damage,Vector3 direction,SumiHitKind kind)
        {
            if(dead)return;health-=damage;smoothVelocity=Vector3.zero;velocity=direction*.18f;ReleaseToken();
            Enter(SumiEnemyState.Recoil);
            if(health<=0)Die(kind==SumiHitKind.DashCut);
        }
        public void Parried(bool perfect){TakeHit(perfect?12:3,-transform.forward,SumiHitKind.ShoulderCut);}
        public void Execute(){if(dead)return;health=0;Die(false);}
        void Die(bool split){dead=true;ReleaseToken();state=SumiEnemyState.Dead;if(body)body.enabled=false;if(split&&kind!=SumiEnemyKind.Oni)SplitAtWaist();else animator.CrossFadeInFixedTime("Death",.08f);ClearTarget();if(director)director.EnemyDied(this);Destroy(gameObject,2.4f);}
        void ReleaseToken(){if(hasToken&&director)director.ReleaseAttack(this);hasToken=false;}
        void ClearTarget(){if(player.target==transform){player.target=null;player.locked=false;}}

        void SplitAtWaist()
        {
            if(!visual)return;float cutY=transform.position.y+.94f;
            var upper=Instantiate(visual.gameObject,visual.position,visual.rotation);upper.name="Upper ink half";
            var lower=Instantiate(visual.gameObject,visual.position,visual.rotation);lower.name="Lower ink half";
            SumiEnemyAppearance.PrepareSplit(upper,cutY,1);SumiEnemyAppearance.PrepareSplit(lower,cutY,-1);
            upper.AddComponent<SumiInkSplitPiece>().Setup(new Vector3(.35f,.72f,.08f),.75f);
            lower.AddComponent<SumiInkSplitPiece>().Setup(new Vector3(-.10f,-.12f,-.04f),.75f);
            visual.gameObject.SetActive(false);Destroy(upper,1f);Destroy(lower,1f);
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
        static AudioSource audioSource;static AudioClip swing,impact,parry,dash;
        public static void Hit(float stop,float kick){Hit(stop,kick,Vector3.zero);}
        public static void Hit(float stop,float kick,Vector3 at){Freeze(stop);Play(1);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(kick);if(at!=Vector3.zero)Mark(at,kick>.18f,false);}
        public static void Block(Vector3 at){Freeze(.025f);Play(2);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.08f);Mark(at,false,false);}
        public static void Parry(Vector3 at,Vector3 axis){Freeze(.075f);Play(2);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.30f);Mark(at,true,true,axis);}
        public static void WaistCut(SumiEnemy enemy,Vector3 at){Mark(new Vector3(enemy.transform.position.x,enemy.transform.position.y+.94f,enemy.transform.position.z),true,false,enemy.transform.right);}
        public static void Swing(bool fast){Play(fast?3:0);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(fast?.035f:.012f);}
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
            if(audioSource)return;var go=new GameObject("Sumi combat sound");audioSource=go.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.48f;
            swing=Synth("Brush sword",.20f,0);impact=Synth("Ink impact",.16f,1);parry=Synth("Gold deflection",.28f,2);dash=Synth("Ninja brush dash",.24f,3);
        }
        static AudioClip Synth(string name,float duration,int kind)
        {
            const int rate=22050;int count=Mathf.CeilToInt(duration*rate);var data=new float[count];uint seed=(uint)(kind+19)*747796405u;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate,u=t/duration,env=Mathf.Pow(1-u,kind==2?3f:2f);seed=seed*1664525u+1013904223u;float noise=((seed>>9)&0x7fffff)/4194304f-1f;float value;
                if(kind==0)value=Mathf.Sin(2*Mathf.PI*(620-430*u)*t)*.28f+noise*.16f;
                else if(kind==1)value=Mathf.Sin(2*Mathf.PI*105*t)*.48f+noise*.34f;
                else if(kind==2)value=Mathf.Sin(2*Mathf.PI*1480*t)*.38f+Mathf.Sin(2*Mathf.PI*2270*t)*.25f+noise*.08f;
                else value=Mathf.Sin(2*Mathf.PI*(310-190*u)*t)*.20f+noise*.25f;
                data[i]=value*env;
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(data,0);return clip;
        }
        static void Play(int kind){EnsureAudio();audioSource.pitch=1;audioSource.PlayOneShot(kind==0?swing:kind==1?impact:kind==2?parry:dash,kind==2?.78f:kind==1?.68f:.55f);}
        static void Mark(Vector3 at,bool strong,bool gilded,Vector3 axis=default)
        {
            Ensure();int index=cursor;cursor=(cursor+1)%marks.Length;var line=marks[index];float d=strong?.62f:.27f;if(axis.sqrMagnitude<.01f)axis=Vector3.right;axis.Normalize();
            line.sharedMaterial=gilded?gold:ink;line.startWidth=strong?.045f:.025f;line.endWidth=.006f;line.SetPosition(0,at-axis*d);line.SetPosition(1,at+Vector3.up*(strong?.12f:.07f));line.SetPosition(2,at+axis*d);line.enabled=true;expire[index]=Time.unscaledTime+(strong?.20f:.11f);
        }
        public static void Tick(){SumiTime.Tick();if(marks!=null)for(int i=0;i<marks.Length;i++)if(marks[i].enabled&&Time.unscaledTime>=expire[i])marks[i].enabled=false;}
        public static void Clear(){SumiTime.Reset();until=0;if(marks!=null)for(int i=0;i<marks.Length;i++)if(marks[i])Object.Destroy(marks[i].gameObject);marks=null;if(ink)Object.Destroy(ink);if(gold)Object.Destroy(gold);if(audioSource)Object.Destroy(audioSource.gameObject);audioSource=null;swing=impact=parry=dash=null;}
    }
}
