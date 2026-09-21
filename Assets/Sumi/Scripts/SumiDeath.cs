using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Sumi
{
    public enum SumiDeathArc { Horizontal, Downward, Upward, Rear, Split }

    public struct SumiFatalHit
    {
        public Vector3 hitPoint, hitDirection, attackDirection, attackerPosition, victimPosition;
        public float damage, impactForce;
        public SumiHitKind attackType;
        public SumiSwordArc bladeArc;
        public SumiDeathArc deathArc;
        public bool wasCritical, isPlayer, isBoss, split;
        public int seed;

        public static SumiFatalHit Make(Transform victim, Vector3 attacker, Vector3 point, Vector3 knock, Vector3 cut, float damage, SumiHitKind kind, SumiSwordArc arc, bool player, bool boss, bool split)
        {
            Vector3 dir=knock.sqrMagnitude>.0001f?knock.normalized:Vector3.back;
            Vector3 blade=cut.sqrMagnitude>.0001f?cut.normalized:dir;
            var hit=new SumiFatalHit
            {
                hitPoint=point==Vector3.zero?victim.position+Vector3.up*1.15f:point,
                hitDirection=dir,attackDirection=blade,attackerPosition=attacker,victimPosition=victim.position,
                damage=damage,impactForce=kind==SumiHitKind.HeavyCut||kind==SumiHitKind.Finisher?1.35f:kind==SumiHitKind.DashCut?1.2f:1f,
                attackType=kind,bladeArc=arc,wasCritical=kind==SumiHitKind.Finisher||kind==SumiHitKind.HeavyCut||kind==SumiHitKind.DashCut,
                isPlayer=player,isBoss=boss,split=split,seed=victim.GetInstanceID()^Time.frameCount
            };
            if(split)hit.deathArc=SumiDeathArc.Split;
            else if(Vector3.Dot(victim.forward,dir)>.42f)hit.deathArc=SumiDeathArc.Rear;
            else if(arc==SumiSwordArc.Overhead||dir.y<-.42f)hit.deathArc=SumiDeathArc.Downward;
            else if(arc==SumiSwordArc.Returning||dir.y>.32f)hit.deathArc=SumiDeathArc.Upward;
            else hit.deathArc=SumiDeathArc.Horizontal;
            return hit;
        }
    }

    // Fatal hits share one beat sheet: freeze, readable pose, follow-through, aftermath.
    public static class SumiDeath
    {
        public static bool CanSkip { get; private set; }
        public static float UiReveal { get; private set; }
        public static float MusicGain { get; private set; }=1;
        public static float MusicCutoff { get; private set; }=22000;
        static SumiDeathActor playerActor;
        static readonly SumiDeathTuning Fallback=new SumiDeathTuning();
        public static SumiDeathTuning Feel=>SumiGame.I&&SumiGame.I.config&&SumiGame.I.config.death!=null?SumiGame.I.config.death:Fallback;
        public static float Hash(int seed,int salt){float n=Mathf.Sin(seed*12.9898f+salt*78.233f)*43758.5453f;return n-Mathf.Floor(n);}

        public static void BeginEnemy(SumiEnemy enemy,in SumiFatalHit hit)
        {
            if(!enemy||enemy.GetComponent<SumiDeathActor>())return;
            Impact(hit,false);
            var actor=enemy.gameObject.AddComponent<SumiDeathActor>();
            actor.Begin(hit,enemy,null);
        }
        public static void BeginPlayer(SumiPlayer player,in SumiFatalHit hit)
        {
            if(!player)return;
            if(playerActor)return;
            Impact(hit,true);
            CanSkip=false;UiReveal=0;MusicGain=.28f;MusicCutoff=720;
            if(SumiGame.I&&SumiGame.I.view)
            {
                SumiGame.I.view.Kick(Feel.playerDeathCameraImpulse,hit.attackDirection);
                SumiGame.I.view.Frame(player.transform,20f);
            }
            SumiTime.SlowMotion(Feel.playerDeathSlowmo,Feel.playerSlowmoHold,Feel.playerSlowmoFade);
            playerActor=player.gameObject.AddComponent<SumiDeathActor>();
            playerActor.Begin(hit,null,player);
        }
        static void Impact(in SumiFatalHit hit,bool player)
        {
            var feel=Feel;float extra=hit.wasCritical?feel.heavyFatalHitstopBonus:0;
            float stop=player?feel.playerFatalHitstop:hit.isBoss?feel.bossFatalHitstop:feel.enemyFatalHitstop;
            SumiTime.HitStop(stop+extra);
            float kick=player?feel.playerDeathCameraImpulse:feel.enemyDeathCameraImpulse*(hit.isBoss?1.25f:1f);
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(kick,hit.attackDirection);
            SumiDeathFx.Slash(hit);SumiDeathFx.PlayImpact(hit);
            if(!player)SumiDeathFx.WorldTick();
        }
        public static void Skip()
        {
            if(!CanSkip)return;
            if(playerActor)playerActor.Rush();
            else RestartNow();
        }
        public static void Clear()
        {
            playerActor=null;CanSkip=false;UiReveal=0;MusicGain=1;MusicCutoff=22000;SumiDeathFx.Clear();
        }
        internal static void SetSkip(bool value){CanSkip=value;}
        internal static void SetReveal(float value){UiReveal=Mathf.Clamp01(value);if(UiReveal>.2f){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
        internal static void SetMusic(float gain,float cutoff){MusicGain=gain;MusicCutoff=cutoff;}
        internal static void RestartNow()
        {
            Clear();SumiTime.Reset();
            if(SumiGame.I&&SumiGame.I.run)SumiGame.I.run.DebugRestart();
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public sealed class SumiDeathActor:MonoBehaviour
    {
        SumiFatalHit hit;SumiEnemy enemy;SumiPlayer player;SumiHumanoidRonin human;
        Animator animator;CharacterController body;Transform visual,hips;
        Vector3 velocity,poseEuler,visualHome;Quaternion visualRot;
        float born,poseAt,deathAt,dropAt,landAt,inkAt,vaporAt,rushAt;bool deathPlayed,dropped,landed,rushed,split,vaporizing;
        Volume volume;int seed;

        public void Begin(in SumiFatalHit fatal,SumiEnemy e,SumiPlayer p)
        {
            hit=fatal;enemy=e;player=p;split=fatal.split;seed=fatal.seed;
            born=Time.unscaledTime;var feel=SumiDeath.Feel;float v=Mathf.Lerp(.88f,1.12f,SumiDeath.Hash(seed,3));
            body=GetComponent<CharacterController>();
            if(e){animator=e.animator;visual=e.visual;foreach(var blade in GetComponentsInChildren<SumiEnemyBlade>())blade.enabled=false;}
            else
            {
                human=GetComponent<SumiHumanoidRonin>();animator=human?human.animator:null;visual=human?human.visual:null;
            }
            if(visual){visualHome=visual.localPosition;visualRot=visual.localRotation;}
            if(animator){hips=animator.GetBoneTransform(HumanBodyBones.Hips);animator.speed=1;animator.CrossFadeInFixedTime("Hit",.03f,0,0);}
            if(player&&visual)
            {
                var redBlock=new MaterialPropertyBlock();
                foreach(var render in visual.GetComponentsInChildren<Renderer>(true))
                {render.GetPropertyBlock(redBlock);redBlock.SetFloat("_Red",.75f);redBlock.SetVector("_DeathWound",new Vector4(fatal.hitPoint.x,fatal.hitPoint.y,fatal.hitPoint.z,1));render.SetPropertyBlock(redBlock);}
            }
            foreach(var trail in GetComponentsInChildren<TrailRenderer>()){trail.emitting=false;trail.Clear();}
            foreach(var cloth in GetComponentsInChildren<SumiClothMotion>())cloth.Shock(fatal.hitDirection*4.5f+Vector3.up*(fatal.deathArc==SumiDeathArc.Upward?2.4f:0.6f));
            foreach(var scarf in GetComponentsInChildren<SumiEnemyScarf>())scarf.Shock(fatal.hitDirection*2.8f+Vector3.up);
            poseEuler=PoseOf(fatal.deathArc,fatal.attackDirection,transform);
            Vector3 knock=fatal.hitDirection;knock.y=0;if(knock.sqrMagnitude<.001f)knock=-transform.forward;knock.Normalize();
            float force=feel.bodyImpactStrength*fatal.impactForce*v*(fatal.isBoss?1.18f:1f);
            if(fatal.deathArc==SumiDeathArc.Rear)force*=.55f;
            if(fatal.deathArc==SumiDeathArc.Downward)force*=.72f;
            if(player)force*=.62f;
            velocity=knock*force*2.15f;
            if(fatal.deathArc==SumiDeathArc.Upward)velocity+=Vector3.up*(player?1.05f:1.55f)*v;
            if(fatal.deathArc==SumiDeathArc.Downward)velocity+=Vector3.down*.35f;
            velocity=Vector3.ClampMagnitude(velocity,player?2.6f:4.2f);
            float hitstop=player?feel.playerFatalHitstop:fatal.isBoss?feel.bossFatalHitstop:feel.enemyFatalHitstop;
            poseAt=born+hitstop;
            deathAt=poseAt+(player?feel.playerRealization:feel.deathPoseHold)*v;
            dropAt=poseAt+(player?feel.playerWeaponDropDelay:feel.enemyWeaponDropDelay)*v;
            landAt=poseAt+(player?feel.playerLandDelay:feel.enemyLandDelay)*v;
            inkAt=landAt+.04f;
            vaporAt=float.PositiveInfinity;
            if(split){dropAt=born;landAt=poseAt+.18f;deathPlayed=true;}
        }
        static Vector3 PoseOf(SumiDeathArc arc,Vector3 cut,Transform root)
        {
            float side=Mathf.Sign(Vector3.Dot(Vector3.Cross(root.forward,Vector3.up),cut));if(side==0)side=1;
            if(arc==SumiDeathArc.Downward)return new Vector3(16f,side*6f,side*4f);
            if(arc==SumiDeathArc.Upward)return new Vector3(-14f,side*8f,side*-6f);
            if(arc==SumiDeathArc.Rear)return new Vector3(7f,side*4f,0);
            return new Vector3(8f,side*14f,side*18f);
        }
        void Update()
        {
            float now=Time.unscaledTime,dt=Time.deltaTime,age=now-born;
            var feel=SumiDeath.Feel;
            if(player)
            {
                if(age>=feel.restartInputDelay)SumiDeath.SetSkip(true);
                if(rushed)
                {
                    SumiDeath.SetReveal(Mathf.MoveTowards(SumiDeath.UiReveal,1,Time.unscaledDeltaTime*8f));
                    if(SumiDeath.UiReveal>=.98f&&now-rushAt>=feel.skipRestartHold){SumiDeath.RestartNow();enabled=false;return;}
                }
            }
            if(dt>0)
            {
                if(!deathPlayed&&now>=deathAt)
                {
                    deathPlayed=true;if(animator){animator.speed=player?1f:1.85f;animator.CrossFadeInFixedTime("Death",player?.12f:.07f,0,0);}
                }
                if(!dropped&&now>=dropAt)Drop();
                if(landed&&animator&&animator.speed>0)
                {
                    var deathState=animator.GetCurrentAnimatorStateInfo(0);
                    if(deathState.IsName("Death")&&deathState.normalizedTime>=.96f)animator.speed=0;
                }
                if(landed)
                {
                    velocity=Vector3.zero;
                    if(transform.position.y<0f)SnapGround();
                }
                else
                {
                    velocity=Vector3.Lerp(velocity,new Vector3(0,velocity.y,0),1-Mathf.Exp(-3.4f*dt));
                    if(body&&body.enabled){if(!body.isGrounded)velocity.y-=16f*dt;else if(velocity.y<0)velocity.y=0;}
                    else velocity.y-=16f*dt;
                    velocity=Vector3.ClampMagnitude(velocity,player?3.2f:4.6f);
                    Vector3 move=velocity*dt;
                    if(body&&body.enabled)body.Move(move);
                    else transform.position+=move;
                    if(player)
                    {
                        if(now>=landAt)Land();
                    }
                    else if(now>=landAt&&GroundContact(now))Land();
                }
                if(visual)
                {
                    float lean=landed?.35f:now<deathAt?1f:.7f;
                    float lift=vaporizing?Mathf.SmoothStep(0,1,Mathf.Clamp01((now-vaporAt)/Mathf.Max(.05f,feel.enemyVaporDuration)))*.22f:0;
                    visual.localRotation=Quaternion.Slerp(visual.localRotation,visualRot*Quaternion.Euler(poseEuler*lean),1-Mathf.Exp(-9f*dt));
                    visual.localPosition=Vector3.Lerp(visual.localPosition,visualHome+transform.InverseTransformDirection(hit.hitDirection)*.06f*(1-lean)+Vector3.up*lift,1-Mathf.Exp(-7f*dt));
                }
            }
            if(player&&landed&&!rushed)
            {
                float ink=Mathf.Clamp01((now-inkAt)/Mathf.Max(.05f,feel.inkSpreadDuration));
                SumiDeath.SetReveal(ink);
                SumiDeath.SetMusic(Mathf.Lerp(.18f,.08f,ink),Mathf.Lerp(520,280,ink));
            }
            if(!player&&landed&&now>=vaporAt)
            {
                if(!vaporizing)
                {
                    vaporizing=true;
                    Vector3 at=transform.position;if(hips)at=new Vector3(hips.position.x,transform.position.y,hips.position.z);
                    SumiDeathFx.VaporStart(at,hit);
                    SumiDeathFx.InkMark(at,hit,false);
                }
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((now-vaporAt)/Mathf.Max(.05f,feel.enemyVaporDuration)));
                SumiDeathFx.Dissolve(visual?visual:transform,t,seed,hit.hitPoint);
                if(t>=.995f){Destroy(gameObject);return;}
            }
        }
        bool GroundContact(float now)
        {
            bool rootGrounded=(body&&body.enabled&&body.isGrounded)||Physics.Raycast(transform.position+Vector3.up*.15f,Vector3.down,.3f,1<<8,QueryTriggerInteraction.Ignore);
            if(!rootGrounded)return false;
            if(split)return true;
            if(hips&&Physics.Raycast(transform.position+Vector3.up*1.5f,Vector3.down,out var ground,3f,1<<8,QueryTriggerInteraction.Ignore)
                &&hips.position.y<=ground.point.y+.48f)return true;
            if(animator&&animator.GetCurrentAnimatorStateInfo(0).IsName("Death")&&animator.GetCurrentAnimatorStateInfo(0).normalizedTime>=.62f)return true;
            return now>=deathAt+.9f;
        }
        void Drop()
        {
            dropped=true;
            Vector3 impulse=(hit.hitDirection+Vector3.down*.45f+transform.right*(SumiDeath.Hash(seed,9)*2-1)*.35f)*SumiDeath.Feel.weaponDropForce*(hit.isBoss?1.2f:1f);
            if(player&&human)human.ReleaseSword(impulse);
            else foreach(var blade in GetComponentsInChildren<SumiEnemyBlade>(true))blade.Release(impulse);
            SumiDeathFx.PlayWeaponDrop(transform.position+Vector3.up*.4f,hit.isBoss);
        }
        void SnapGround()
        {
            Vector3 p=transform.position;
            if(Physics.Raycast(p+Vector3.up*2.2f,Vector3.down,out var ground,6f,1<<8,QueryTriggerInteraction.Ignore))p.y=ground.point.y+.02f;
            else p.y=Mathf.Max(.02f,p.y);
            transform.position=p;
        }
        void Land()
        {
            if(landed)return;landed=true;velocity=Vector3.zero;
            if(!player)vaporAt=Time.unscaledTime+(split?.12f:SumiDeath.Feel.enemyVaporDelay);
            if(body)body.enabled=false;SnapGround();
            Vector3 at=transform.position;if(hips)at=new Vector3(hips.position.x,transform.position.y,hips.position.z);
            SumiDeathFx.BodyLand(at,hit,player);
            if(player)
            {
                SumiDeathFx.PlayDeathCue();
                SumiDeath.SetMusic(.16f,420);
                if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(SumiDeath.Feel.bodyLandImpulse,Vector3.down);
                SumiDeathFx.InkMark(at,hit,true);
                SumiDeathFx.InkTableau(at,hit);
                volume=SumiDeathFx.DeathVolume();
            }
            else if(SumiGame.I&&SumiGame.I.view&&hit.isBoss)SumiGame.I.view.Kick(SumiDeath.Feel.bodyLandImpulse*.7f,Vector3.down);
        }
        public void Rush()
        {
            if(rushed)return;rushed=true;rushAt=Time.unscaledTime;SumiDeath.SetSkip(true);
            if(!dropped)Drop();if(!landed)Land();
            SumiDeathFx.FadeDeathCue();SumiTime.Reset();SumiDeath.SetMusic(.06f,240);
            DestroyVolume();
        }
        void DestroyVolume()
        {
            if(!volume)return;
            if(volume.sharedProfile)Object.Destroy(volume.sharedProfile);
            Object.Destroy(volume.gameObject);volume=null;
        }
        void OnDestroy(){DestroyVolume();if(player)SumiDeath.Clear();}
    }

    public static class SumiDeathFx
    {
        const int Strokes=64,Marks=24;
        static LineRenderer[] lines;static float[] expire;static int cursor;
        static MeshRenderer[] marks;static float[] markLife,markBorn,markDry;static Color[] markTint;static int markCursor;
        static Material ink,crimson,markMat;
        static MaterialPropertyBlock block;
        static AudioSource source,cueSource;static AudioClip[] deathHits;static AudioClip metal,thud,cue;
        static int lastEnemyHit=-1;static float cueFadeAt=-1;
        static Volume tickVolume;static float tickUntil;
        static Texture2D brush;
        static PhysicsMaterial katanaMat;

        public static void Slash(in SumiFatalHit hit)
        {
            Ensure();float scale=SumiDeath.Feel.deathParticleScale*(hit.isPlayer?1.35f:hit.isBoss?1.28f:1f);
            Vector3 axis=hit.attackDirection.sqrMagnitude>.001f?hit.attackDirection:hit.hitDirection;
            if(hit.deathArc==SumiDeathArc.Horizontal)axis=Vector3.ProjectOnPlane(axis,Vector3.up).normalized;
            if(axis.sqrMagnitude<.001f)axis=Vector3.right;
            Vector3 at=hit.hitPoint;float len=(hit.isPlayer?.95f:.72f)*scale*(hit.wasCritical?1.18f:1f);
            Vector3 lift=Vector3.up*(hit.deathArc==SumiDeathArc.Downward?.02f:hit.deathArc==SumiDeathArc.Upward?.16f:.08f);
            Stroke(at-axis*len+lift*.2f,at+lift,at+axis*len*1.15f+lift*.4f,.055f*scale,.006f,.42f,false);
            Stroke(at-axis*len*.72f+lift*.45f+Vector3.up*.04f,at+Vector3.up*.05f,at+axis*len*.8f,.022f*scale,.004f,.28f,true);
            // Broken fibers travel with the cut; the short separate marks read as torn paper.
            for(int i=0;i<4;i++)
            {
                float u=SumiDeath.Hash(hit.seed,120+i),side=(i%2==0?1f:-1f);
                Vector3 p=at+axis*(u-.5f)*len+Vector3.up*(u-.5f)*.25f;
                Vector3 fiber=axis*(.12f+u*.12f)+Vector3.Cross(axis,Vector3.up).normalized*side*.055f;
                Stroke(p,p+fiber*.5f,p+fiber,.009f,.001f,.22f,false);
            }
            if(hit.wasCritical||hit.isBoss)
            {
                Vector3 p=at+axis*len*.35f+Vector3.up*.09f;
                Stroke(p,p+Vector3.up*.035f,p+Vector3.up*.065f,.014f,.002f,.23f,true);
            }
            int drops=hit.isPlayer?7:5;
            for(int i=0;i<drops;i++)
            {
                float u=SumiDeath.Hash(hit.seed,20+i);Vector3 d=(axis*(.6f+u)+Vector3.down*(.15f+u*.4f)+Vector3.Cross(axis,Vector3.up)*(u-.5f)*.55f).normalized;
                Vector3 p=at+d*(.12f+u*.2f);
                Stroke(p,p+d*(.18f+u*.35f)*scale,p+d*(.46f+u*.5f)*scale+Vector3.down*.08f,.028f,.003f,.22f+u*.1f,i==0||i==3);
            }
        }
        public static void BodyLand(Vector3 at,in SumiFatalHit hit,bool player)
        {
            Ensure();Play(thud,player?.56f:.34f);
            Vector3 origin=new Vector3(at.x,at.y+.03f,at.z);float scale=SumiDeath.Feel.deathParticleScale*(player?1.2f:1f);
            int n=player?10:6;
            for(int i=0;i<n;i++)
            {
                float u=SumiDeath.Hash(hit.seed,40+i),ang=u*360f;Vector3 dir=Quaternion.Euler(0,ang,0)*Vector3.forward;
                float reach=(.45f+u*.9f)*scale*(player?1.4f:1f);
                Stroke(origin,origin+dir*reach*.45f+Vector3.up*.04f,origin+dir*reach,.028f*scale,.004f,.38f,false);
                if(i<4)Stroke(origin+dir*.1f,origin+dir*.18f+Vector3.up*(.12f+u*.1f),origin+dir*.22f+Vector3.up*.02f,.016f,.003f,.2f,false);
            }
        }
        public static void InkTableau(Vector3 at,in SumiFatalHit hit)
        {
            Ensure();float scale=SumiDeath.Feel.deathParticleScale;
            Vector3 origin=new Vector3(at.x,at.y+.02f,at.z);
            for(int i=0;i<14;i++)
            {
                float u=SumiDeath.Hash(hit.seed,70+i),ang=u*360f+i*17f;
                Vector3 dir=Quaternion.Euler(0,ang,0)*Vector3.forward;
                float reach=(1.1f+u*2.1f)*scale;bool red=i==2||i==8;
                Vector3 a=origin+dir*(.08f+u*.2f);Vector3 b=origin+dir*reach*.55f+Vector3.up*(.02f+u*.03f);
                Vector3 c=origin+dir*reach+Vector3.up*.01f+Vector3.Cross(dir,Vector3.up)*(u-.5f)*.35f;
                Stroke(a,b,c,red?.03f:.05f*scale,.005f,.9f,red);
            }
            Vector3 seal=origin+Vector3.up*.05f+Vector3.ProjectOnPlane(hit.attackDirection,Vector3.up).normalized*.22f;
            Stroke(seal+Vector3.left*.04f,seal,seal+Vector3.right*.04f,.026f,.026f,20f,true);
            Stroke(seal+Vector3.back*.04f,seal,seal+Vector3.forward*.04f,.026f,.026f,20f,true);
        }
        public static void VaporStart(Vector3 at,in SumiFatalHit hit)
        {
            Ensure();Vector3 origin=new Vector3(at.x,at.y+.08f,at.z);
            for(int i=0;i<9;i++)
            {
                float u=SumiDeath.Hash(hit.seed,90+i);Vector3 dir=Quaternion.Euler(0,u*360f,0)*Vector3.forward;
                Vector3 a=origin+dir*(.04f+u*.08f)+Vector3.up*(.2f+u*.5f);
                Vector3 b=a+dir*(.08f+u*.12f)+Vector3.up*(.35f+u*.25f);
                Vector3 c=b+dir*(.04f-u*.08f)+Vector3.up*(.55f+u*.4f);
                Stroke(a,b,c,.022f,.002f,.55f+u*.2f,i==2);
            }
        }
        public static void Dissolve(Transform root,float amount,int seed,Vector3 wound)
        {
            if(!root)return;Ensure();
            if(block==null)block=new MaterialPropertyBlock();
            float t=Mathf.Clamp01(amount);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                float local=renderer.name.IndexOf("scarf",System.StringComparison.OrdinalIgnoreCase)>=0||renderer.name.IndexOf("cloth",System.StringComparison.OrdinalIgnoreCase)>=0?Mathf.Clamp01((t-.22f)/.78f):t;
                block.SetFloat("_Dissolve",local);
                block.SetFloat("_DissolveSeed",seed*.013f);
                block.SetVector("_DissolveWound",wound);
                renderer.SetPropertyBlock(block);
                if(renderer is LineRenderer line)
                {
                    line.widthMultiplier=Mathf.Max(.001f,1f-t);
                    if(t>.92f)line.enabled=false;
                }
            }
        }
        public static void InkMark(Vector3 at,in SumiFatalHit hit,bool player)
        {
            Ensure();if(!markMat||marks==null)return;
            if(Physics.Raycast(at+Vector3.up*1.6f,Vector3.down,out var ground,5f,1<<8,QueryTriggerInteraction.Ignore))
                at=new Vector3(at.x,ground.point.y+.018f,at.z);
            else at=new Vector3(at.x,Mathf.Max(.018f,at.y)+.018f,at.z);
            float yaw=SumiDeath.Hash(hit.seed,11)*360f;
            int count=player?4:2;
            for(int j=0;j<count;j++)
            {
            int i=markCursor;markCursor=(markCursor+1)%marks.Length;
            var mark=marks[i];if(!mark)continue;
            float u=SumiDeath.Hash(hit.seed,160+j),v=SumiDeath.Hash(hit.seed,170+j);
            Vector3 offset=Quaternion.Euler(0,yaw,0)*new Vector3((u-.5f)*(player?2.1f:.35f),.002f*j,(v-.5f)*(player?1.5f:.25f));
            mark.transform.SetPositionAndRotation(at+offset,Quaternion.Euler(90f,yaw+j*39f,0));
            mark.transform.localScale=new Vector3((player?3.8f:1.08f)*(j==0?1f:.55f),(player?2f:.65f)*(j==0?1f:.6f),1f);
            if(block==null)block=new MaterialPropertyBlock();
            Color inkColor=player?new Color(.028f,.024f,.022f,j==0?1f:.78f):new Color(.04f,.037f,.033f,j==0?.72f:.42f);
            markTint[i]=inkColor;
            mark.GetPropertyBlock(block);
            block.SetColor("_BaseColor",inkColor);
            mark.SetPropertyBlock(block);
            mark.enabled=true;
            markBorn[i]=Time.unscaledTime;markDry[i]=.4f;
            markLife[i]=player?Time.unscaledTime+240f:Time.unscaledTime+8f;
            }
        }
        public static void PlayImpact(in SumiFatalHit hit)
        {
            Ensure();
            SumiCombatFeedback.PlayContact(hit.wasCritical||hit.isPlayer||hit.isBoss);
            bool weighty=hit.isBoss||hit.attackType==SumiHitKind.HeavyCut||hit.attackType==SumiHitKind.Finisher;
            int[] pool=hit.isPlayer?new[]{6,11,5}:hit.split?new[]{3,7}:weighty?new[]{5,6,11}:new[]{3,7,4,8,9,10};
            int pick=pool[Mathf.Abs(hit.seed%pool.Length)];
            if(!hit.isPlayer&&pick==lastEnemyHit)pick=pool[(System.Array.IndexOf(pool,pick)+1)%pool.Length];
            if(!hit.isPlayer)lastEnemyHit=pick;
            AudioClip clip=deathHits[pick];
            if(clip)Play(clip,hit.isPlayer?.86f:hit.isBoss||hit.wasCritical?.68f:.61f);
        }
        public static void PlayDeathCue()
        {
            Ensure();if(!cue||!cueSource)return;
            cueFadeAt=-1;cueSource.Stop();cueSource.clip=cue;cueSource.volume=.78f;cueSource.Play();
        }
        public static void FadeDeathCue(){if(cueSource&&cueSource.isPlaying)cueFadeAt=Time.unscaledTime;}
        public static void PlayWeaponDrop(Vector3 at,bool heavy){Ensure();source.transform.position=at;Play(metal,heavy?.8f:.62f);}
        public static void DropWeapon(Transform sword,Vector3 impulse)
        {
            if(!sword)return;Ensure();
            sword.SetParent(null,true);
            var rb=sword.GetComponent<Rigidbody>();if(!rb)rb=sword.gameObject.AddComponent<Rigidbody>();
            rb.mass=1.15f;rb.linearDamping=.35f;rb.angularDamping=.55f;rb.interpolation=RigidbodyInterpolation.Interpolate;
            var col=sword.GetComponent<BoxCollider>();if(!col)col=sword.gameObject.AddComponent<BoxCollider>();
            col.size=new Vector3(.07f,1.12f,.03f);col.center=new Vector3(0,.46f,0);
            if(!katanaMat){katanaMat=new PhysicsMaterial("Dropped katana"){bounciness=.22f,dynamicFriction=.48f,staticFriction=.6f,bounceCombine=PhysicsMaterialCombine.Minimum};}
            col.sharedMaterial=katanaMat;
            var root=sword;while(root.parent)root=root.parent;
            var body=root.GetComponent<CharacterController>();if(body)Physics.IgnoreCollision(body,col,true);
            rb.linearVelocity=impulse;rb.angularVelocity=new Vector3(impulse.z,impulse.x,.8f);
            Object.Destroy(sword.gameObject,6f);
        }
        public static Volume DeathVolume()
        {
            var go=new GameObject("Ink death atmosphere");var vol=go.AddComponent<Volume>();vol.isGlobal=true;vol.priority=40;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var vig=profile.Add<Vignette>(true);vig.intensity.Override(.26f);vig.smoothness.Override(.62f);vig.color.Override(new Color(.02f,.015f,.02f));
            var color=profile.Add<ColorAdjustments>(true);color.saturation.Override(-8);color.contrast.Override(4);color.postExposure.Override(0);
            vol.sharedProfile=profile;return vol;
        }
        public static void WorldTick()
        {
            Ensure();
            if(!tickVolume)
            {
                var go=new GameObject("Fatal ink exposure");tickVolume=go.AddComponent<Volume>();
                tickVolume.isGlobal=true;tickVolume.priority=39;
                var profile=ScriptableObject.CreateInstance<VolumeProfile>();
                var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(-.32f);
                tickVolume.sharedProfile=profile;
            }
            tickVolume.weight=1;tickUntil=Time.unscaledTime+.034f;
        }
        public static void Tick()
        {
            if(lines==null)return;float now=Time.unscaledTime;
            if(tickVolume&&now>=tickUntil)tickVolume.weight=0;
            for(int i=0;i<lines.Length;i++)if(lines[i]&&lines[i].enabled&&now>=expire[i])lines[i].enabled=false;
            if(marks==null)return;
            if(block==null)block=new MaterialPropertyBlock();
            for(int i=0;i<marks.Length;i++)
            {
                if(!marks[i]||!marks[i].enabled)continue;
                float left=markLife[i]-now;
                if(left<=0){marks[i].enabled=false;continue;}
                float fade=left<2.4f?Mathf.SmoothStep(0,1,left/2.4f):1f;
                Color c=markTint[i];c.a*=fade;
                marks[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor",c);
                block.SetFloat("_Wet",Mathf.Clamp01((now-markBorn[i])/markDry[i]));
                block.SetFloat("_Dry",left<2.4f?1f-Mathf.Clamp01(left/2.4f):0f);
                marks[i].SetPropertyBlock(block);
            }
            if(cueFadeAt>=0&&cueSource)
            {
                cueSource.volume=.78f*(1f-Mathf.Clamp01((now-cueFadeAt)/.12f));
                if(now-cueFadeAt>=.12f){cueSource.Stop();cueFadeAt=-1;}
            }
        }
        public static void Clear()
        {
            if(lines!=null)for(int i=0;i<lines.Length;i++)if(lines[i])Object.Destroy(lines[i].gameObject);lines=null;
            if(marks!=null)for(int i=0;i<marks.Length;i++)if(marks[i])Object.Destroy(marks[i].gameObject);marks=null;
            if(ink)Object.Destroy(ink);if(crimson)Object.Destroy(crimson);if(markMat)Object.Destroy(markMat);ink=crimson=markMat=null;
            if(source)Object.Destroy(source.gameObject);source=null;
            if(cueSource)Object.Destroy(cueSource.gameObject);cueSource=null;
            if(tickVolume){if(tickVolume.sharedProfile)Object.Destroy(tickVolume.sharedProfile);Object.Destroy(tickVolume.gameObject);tickVolume=null;}
            deathHits=null;metal=thud=cue=null;cueFadeAt=-1;lastEnemyHit=-1;
            if(brush)Object.Destroy(brush);brush=null;katanaMat=null;block=null;
        }
        static void Stroke(Vector3 a,Vector3 b,Vector3 c,float start,float end,float life,bool red)
        {
            Ensure();int i=cursor;cursor=(cursor+1)%lines.Length;var line=lines[i];
            line.sharedMaterial=red?crimson:ink;line.startWidth=start;line.endWidth=end;line.widthMultiplier=1f;
            line.SetPosition(0,a);line.SetPosition(1,b);line.SetPosition(2,c);line.enabled=true;expire[i]=Time.unscaledTime+life;
        }
        static void Play(AudioClip clip,float volume){if(clip&&source)source.PlayOneShot(clip,volume);}
        static void Ensure()
        {
            if(lines!=null)return;
            ink=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ink.color=new Color(.04f,.038f,.034f);
            crimson=new Material(Shader.Find("Universal Render Pipeline/Unlit"));crimson.color=new Color(.55f,.04f,.07f);
            lines=new LineRenderer[Strokes];expire=new float[Strokes];
            for(int i=0;i<Strokes;i++)
            {
                var go=new GameObject("Pooled death stroke");lines[i]=go.AddComponent<LineRenderer>();
                lines[i].useWorldSpace=true;lines[i].positionCount=3;lines[i].enabled=false;lines[i].textureMode=LineTextureMode.Stretch;
                lines[i].alignment=LineAlignment.View;lines[i].numCapVertices=2;lines[i].numCornerVertices=2;
                lines[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var shader=Shader.Find("Sumi/Ink Mark");
            if(shader)
            {
                brush=DryBrush();
                markMat=new Material(shader);markMat.SetTexture("_BaseMap",brush);markMat.SetColor("_BaseColor",new Color(.035f,.032f,.028f,1f));
                marks=new MeshRenderer[Marks];markLife=new float[Marks];markBorn=new float[Marks];markDry=new float[Marks];markTint=new Color[Marks];
                for(int i=0;i<Marks;i++)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name="Pooled ink mark";Object.Destroy(go.GetComponent<Collider>());
                    marks[i]=go.GetComponent<MeshRenderer>();
                    marks[i].sharedMaterial=markMat;marks[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    marks[i].enabled=false;
                }
            }
            var audio=new GameObject("Sumi death sound");source=audio.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=1f;
            deathHits=new AudioClip[12];for(int i=3;i<=11;i++)deathHits[i]=Resources.Load<AudioClip>("Sumi/Audio/DeathHit"+i);
            var music=new GameObject("Sumi game over music");cueSource=music.AddComponent<AudioSource>();cueSource.playOnAwake=false;cueSource.loop=false;cueSource.spatialBlend=0;
            cue=Resources.Load<AudioClip>("Sumi/Audio/DeathOfANinja");
            metal=Tone("Katana drop",4200,t=>Mathf.Sin(t*1860*Mathf.PI*2)*Mathf.Exp(-t*6)*.3f+Mathf.Sin(t*2740*Mathf.PI*2)*Mathf.Exp(-t*9)*.18f+Mathf.Sin(t*4120*Mathf.PI*2)*Mathf.Exp(-t*14)*.08f);
            thud=Tone("Body land",3200,t=>Mathf.Sin(t*52*Mathf.PI*2)*Mathf.Exp(-t*8)*.7f+Mathf.Sin(t*18*Mathf.PI*2)*Mathf.Exp(-t*11)*.18f);
        }
        static AudioClip Tone(string name,int samples,System.Func<float,float> wave)
        {
            var data=new float[samples];int hz=22050;
            for(int i=0;i<samples;i++)data[i]=Mathf.Clamp(wave(i/(float)hz),-1,1);
            var clip=AudioClip.Create(name,samples,1,hz,false);clip.SetData(data,0);return clip;
        }
        static Texture2D DryBrush()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var a=new float[S*S];
            Stamp(a,S,.50f,.50f, 18f,.82f,.11f,1.1f);
            Stamp(a,S,.46f,.54f,-12f,.70f,.07f,2.4f);
            Stamp(a,S,.58f,.44f, 38f,.48f,.05f,3.7f);
            Stamp(a,S,.40f,.47f, 64f,.36f,.035f,5.2f);
            Stamp(a,S,.53f,.58f,-48f,.28f,.03f,6.8f);
            var pixels=new Color[S*S];
            for(int i=0;i<a.Length;i++)pixels[i]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(a[i]),1.25f));
            tex.SetPixels(pixels);tex.Apply();return tex;
        }
        static void Stamp(float[] a,int s,float cx,float cy,float ang,float length,float thick,float seed)
        {
            float r=ang*Mathf.Deg2Rad,cs=Mathf.Cos(r),sn=Mathf.Sin(r);
            for(int y=0;y<s;y++)for(int x=0;x<s;x++)
            {
                float u=(x+.5f)/s-cx,v=(y+.5f)/s-cy;
                float along=u*cs+v*sn,across=-u*sn+v*cs;
                float t=along/(length*.5f);if(t*t>1f)continue;
                float taper=1f-t*t;
                float n=Frac(along*47.3f+across*13.1f+seed);
                float m=Frac(along*91.7f-seed*2.1f);
                float width=thick*(.5f+.5f*taper)*( .72f+n*.4f);
                float mark=(1f-Mathf.SmoothStep(width*.12f,width,Mathf.Abs(across)))*taper;
                if(m>.78f)mark*=.12f;
                mark*=.4f+m*.6f;
                int i=y*s+x;if(mark>a[i])a[i]=mark;
            }
        }
        static float Frac(float v){return v-Mathf.Floor(v);}
    }
}
