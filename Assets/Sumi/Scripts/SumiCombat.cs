using System.Collections.Generic;
using UnityEngine;

namespace Sumi
{
    public enum SumiCombatState { Free,Attack1,Attack2,Attack3,GuardStartup,GuardHeld,GuardRecovery,Dash,DashStrike,HitStun,Dead,HeavyAttack,ShurikenThrow }
    public enum SumiHitKind { ShoulderCut,DashCut,HeavyCut,Finisher,Shuriken }
    public enum SumiEnemyState { Observe,Approach,Windup,Strike,Recovery,Recoil,Dead }

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
        readonly Vector3[][] points={new Vector3[6],new Vector3[6]};LineRenderer[] lines;bool ready;Vector3 shock;
        void Start()
        {
            lines=new LineRenderer[2];
            for(int k=0;k<2;k++)
            {
                var go=new GameObject(k==0?"Long ink scarf":"Broken ink scarf");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();lines[k]=line;line.useWorldSpace=true;line.positionCount=6;line.sharedMaterial=Resources.Load<Material>("Ronin/Soot silhouette");line.startWidth=k==0?.14f:.075f;line.endWidth=.012f;line.numCornerVertices=1;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        public void Shock(Vector3 dir){shock+=dir;}
        void LateUpdate()
        {
            if(!anchor||lines==null)return;Vector3 root=anchor.position+transform.up*.07f-transform.forward*.035f;shock=Vector3.Lerp(shock,Vector3.zero,1-Mathf.Exp(-4f*Mathf.Max(Time.deltaTime,Time.unscaledDeltaTime*.15f)));
            if(!ready){for(int k=0;k<2;k++)for(int i=0;i<6;i++)points[k][i]=root-transform.forward*i*.13f-transform.up*i*.025f;ready=true;}
            for(int k=0;k<2;k++)
            {
                points[k][0]=root+transform.right*(k==0?-.035f:.035f);
                for(int i=1;i<6;i++)
                {
                    float t=i/5f;Vector3 wanted=points[k][i-1]-transform.forward*(.12f+t*.045f)-transform.up*(.018f+t*.018f)+transform.right*Mathf.Sin(Time.time*2.1f+i*1.7f+k)*.018f*t+shock*(.04f+t*.11f);
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
        static float until;static LineRenderer[] marks;static float[] expire;static int cursor;static Material ink,gold,red;
        static Vector3[] dustVelocity;static float[] dustBorn,dustWidth;
        static AudioSource audioSource;static AudioClip swing,fastSwing,impact,heavyImpact,parry;
        public static float ComboShakeScale(int step)=>step<=1?1f:step==2?.55f:.36f;
        public static void Hit(float stop,float kick){Hit(stop,kick,Vector3.zero,Vector3.zero);}
        public static void Hit(float stop,float kick,Vector3 at){Hit(stop,kick,at,Vector3.zero);}
        public static void Hit(float stop,float kick,Vector3 at,Vector3 axis,int comboStep=0)
        {
            Freeze(stop);Play(kick>.18f?4:1);
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(kick*ComboShakeScale(comboStep),axis);
            if(at!=Vector3.zero)Mark(at,kick>.18f,false,axis);
        }
        public static void Block(Vector3 at){Freeze(.025f);Play(2);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.14f);Mark(at,false,false);}
        public static void ParryReady(Vector3 at,Vector3 axis){if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.11f,axis);Mark(at,false,true,axis);}
        public static void Parry(Vector3 at,Vector3 axis){Freeze(.085f);Play(2);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(.46f,axis);Mark(at,true,true,axis);}
        public static void WaistCut(SumiEnemy enemy,Vector3 at){Mark(new Vector3(enemy.transform.position.x,enemy.transform.position.y+.94f,enemy.transform.position.z),true,false,enemy.transform.right);}
        public static void PlayContact(bool heavy){EnsureAudio();Play(heavy?4:1);}
        public static void Swing(bool fast,Vector3 direction=default,int comboStep=0){Play(fast?3:0);if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick((fast?.20f:.13f)*ComboShakeScale(comboStep),direction);}
        public static void Dash(Vector3 direction){if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Sway(.29f,direction);}
        public static void EnemySwing(bool fast){Play(fast?3:0);}
        // Called at accepted damage, independently of combo strength, recoil and audio throttling.
        public static void ContactDust(Vector3 at,Vector3 direction,bool enemyHit)
        {
            Ensure();if(direction.sqrMagnitude<.001f)direction=Vector3.right;direction.Normalize();
            Vector3 side=Vector3.Cross(direction,Vector3.up);if(side.sqrMagnitude<.001f)side=Vector3.forward;side.Normalize();
            for(int k=0;k<7;k++){
                int index=cursor;cursor=(cursor+1)%marks.Length;var line=marks[index];
                Vector3 spray=(direction*.45f+side*Mathf.Sin(k*2.4f)*.8f+Vector3.up*Mathf.Cos(k*1.7f)*.65f).normalized;
                Vector3 p=at+spray*.035f;float length=k==0?.42f:.10f+(k%3)*.045f;
                line.sharedMaterial=enemyHit?red:gold;line.startWidth=k==0?.048f:.024f;line.endWidth=.003f;
                line.SetPosition(0,p-spray*length);line.SetPosition(1,p);line.SetPosition(2,p+spray*length*.4f);line.enabled=true;
                dustVelocity[index]=spray*(1.2f+k*.17f);dustBorn[index]=Time.unscaledTime;dustWidth[index]=line.startWidth;
                expire[index]=Time.unscaledTime+.27f;
            }
        }
        public static void DashStroke(Vector3 from,Vector3 to)
        {
            Ensure();int index=cursor;cursor=(cursor+1)%marks.Length;var line=marks[index];Vector3 side=Vector3.Cross(Vector3.up,(to-from).normalized)*.08f;
            dustVelocity[index]=Vector3.zero;
            line.sharedMaterial=ink;line.startWidth=.18f;line.endWidth=.006f;line.SetPosition(0,from+Vector3.up*.055f-side);line.SetPosition(1,Vector3.Lerp(from,to,.53f)+Vector3.up*.045f+side);line.SetPosition(2,to+Vector3.up*.035f);line.enabled=true;expire[index]=Time.unscaledTime+.55f;
            int second=cursor;cursor=(cursor+1)%marks.Length;dustVelocity[second]=Vector3.zero;var echo=marks[second];echo.sharedMaterial=gold;echo.startWidth=.045f;echo.endWidth=.002f;Vector3 spread=side*4f;echo.SetPosition(0,from+Vector3.up*.08f+spread);echo.SetPosition(1,Vector3.Lerp(from,to,.57f)+Vector3.up*.06f+spread);echo.SetPosition(2,to+Vector3.up*.04f+spread);echo.enabled=true;expire[second]=Time.unscaledTime+.42f;
        }
        static void Freeze(float stop){if(stop<=0)return;until=Mathf.Max(until,Time.unscaledTime+stop);SumiTime.HitStop(stop);}
        static void Ensure()
        {
            if(marks!=null)return;marks=new LineRenderer[96];expire=new float[96];dustVelocity=new Vector3[96];dustBorn=new float[96];dustWidth=new float[96];ink=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ink.color=new Color(.055f,.052f,.045f);gold=new Material(Shader.Find("Universal Render Pipeline/Unlit"));gold.color=new Color(1f,.72f,.16f);red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));red.color=new Color(.95f,.07f,.025f);
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
            dustVelocity[index]=Vector3.zero;
            line.sharedMaterial=gilded?gold:ink;line.startWidth=strong?.045f:.025f;line.endWidth=.006f;line.SetPosition(0,at-axis*d);line.SetPosition(1,at+Vector3.up*(strong?.12f:.07f));line.SetPosition(2,at+axis*d);line.enabled=true;expire[index]=Time.unscaledTime+(strong?.20f:.11f);
        }
        public static void Tick(){
            SumiTime.Tick();if(marks==null)return;
            for(int i=0;i<marks.Length;i++){
                if(!marks[i].enabled)continue;
                if(Time.unscaledTime>=expire[i]){marks[i].enabled=false;dustVelocity[i]=Vector3.zero;continue;}
                if(dustVelocity[i].sqrMagnitude>.001f){
                    float t=Mathf.Clamp01((Time.unscaledTime-dustBorn[i])/.27f);
                    Vector3 shift=dustVelocity[i]*Time.unscaledDeltaTime;
                    for(int p=0;p<3;p++)marks[i].SetPosition(p,marks[i].GetPosition(p)+shift);
                    marks[i].startWidth=dustWidth[i]*(1-t);marks[i].endWidth=.003f*(1-t);
                }
            }
        }
        public static void Clear(){SumiTime.Reset();until=0;if(marks!=null)for(int i=0;i<marks.Length;i++)if(marks[i])Object.Destroy(marks[i].gameObject);marks=null;if(ink)Object.Destroy(ink);if(gold)Object.Destroy(gold);if(red)Object.Destroy(red);if(audioSource)Object.Destroy(audioSource.gameObject);audioSource=null;swing=fastSwing=impact=heavyImpact=parry=null;SumiDeath.Clear();}
    }
}
