using System.Collections.Generic;
using UnityEngine;

namespace Sumi
{
    public sealed class SumiEnemy : MonoBehaviour
    {
        public static readonly List<SumiEnemy> Active=new List<SumiEnemy>();
        public float health=90,maxHealth=90;
        public bool dead;
        public SumiEnemyState state;
        public SumiEnemyKind kind;
        public Transform visual;
        public Animator animator;
        public SumiEnemyAttack CurrentAttack { get; private set; }
        public float StateTime=>elapsed;
        public bool Enraged { get; private set; }
        public bool CanExecute=>!dead&&health<=maxHealth*(kind==SumiEnemyKind.Oni?.12f:.35f);
        public bool Braced=>CurrentAttack!=null&&CurrentAttack.braced&&(state==SumiEnemyState.Windup||state==SumiEnemyState.Strike);
        public bool Attacking=>state==SumiEnemyState.Windup||state==SumiEnemyState.Strike;
        public bool ReadyToAttack=>!dead&&state==SumiEnemyState.Approach&&Time.time>=nextAttackAt;
        public float EngagementRange=>kind==SumiEnemyKind.Shade?3.1f:kind==SumiEnemyKind.Oni?3.15f:2.7f;
        public float PoseWeight=>Attacking?Mathf.Clamp01(elapsed/.10f):state==SumiEnemyState.Recovery?1-Mathf.Clamp01(elapsed/.18f):0;
        public float WindupDuration=>CurrentAttack==null?0:CurrentAttack.windup;

        SumiPlayer player;
        SumiRunDirector director;
        CharacterController body;
        Vector3 velocity,smoothVelocity,attackDirection,retreatDirection;
        float elapsed,nextAttackAt,lastLunge,recoilDuration=.3f,staggerReadyAt,clipLength=1.2f;
        int attackCount;
        bool struck,hasToken,phasePending;
        LineRenderer warning;
        readonly Vector3[] warningPoints=new Vector3[27];
        static readonly int Speed=Animator.StringToHash("Speed"),AttackRate=Animator.StringToHash("AttackRate");

        void OnEnable(){if(!Active.Contains(this))Active.Add(this);}
        void OnDisable(){Active.Remove(this);ReleaseToken();if(warning)warning.enabled=false;}
        public void Init(SumiPlayer p){Init(p,SumiEnemyKind.Retainer,SumiGame.I?SumiGame.I.run:null);}
        public void Init(SumiPlayer p,SumiEnemyKind enemyKind,SumiRunDirector run)
        {
            player=p;director=run;kind=enemyKind;
            name=kind==SumiEnemyKind.Oni?"Painted Oni":kind==SumiEnemyKind.Shade?"Ink Shade":"Ashen masked retainer";
            maxHealth=health=kind==SumiEnemyKind.Oni?340:kind==SumiEnemyKind.Shade?64:90;
            body=gameObject.AddComponent<CharacterController>();body.height=kind==SumiEnemyKind.Oni?2.35f:1.85f;
            body.radius=kind==SumiEnemyKind.Oni?.48f:.36f;body.center=new Vector3(0,body.height*.51f,0);body.stepOffset=.18f;
            visual=Instantiate(Resources.Load<GameObject>("Ronin/Humanoid"),transform,false).transform;
            animator=visual.GetComponent<Animator>();animator.applyRootMotion=false;SumiEnemyAppearance.Apply(visual,kind);
            var clip=Resources.Load<AnimationClip>("Ronin/Sword_Attack");if(clip)clipLength=clip.length;
            visual.gameObject.AddComponent<SumiEnemyBlade>().Init(this);
            warning=new GameObject("Attack brush warning").AddComponent<LineRenderer>();warning.transform.SetParent(transform,false);
            warning.useWorldSpace=true;warning.positionCount=warningPoints.Length;warning.loop=false;
            warning.sharedMaterial=SumiArt.Black;warning.enabled=false;
            warning.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;warning.receiveShadows=false;
            nextAttackAt=Time.time+.65f;Enter(SumiEnemyState.Observe);
        }

        void Update()
        {
            if(dead||!player||!director||!director.CombatActive||Time.deltaTime<=0||Time.timeScale<=0)return;
            float dt=Time.deltaTime,before=elapsed;elapsed+=dt;
            Vector3 to=player.transform.position-transform.position;to.y=0;
            float range=to.magnitude;Vector3 dir=range>.001f?to/range:transform.forward;
            switch(state)
            {
                case SumiEnemyState.Observe:
                    Face(dir,dt,240);SmoothMove(Vector3.zero,dt);if(elapsed>=.22f)Enter(SumiEnemyState.Approach);break;
                case SumiEnemyState.Approach:
                    Face(dir,dt,240);Approach(dir,range,dt);
                    if(phasePending){BeginPhase();break;}
                    if(ReadyToAttack&&range<=EngagementRange&&director.RequestAttack(this))
                    {hasToken=true;BeginAttack(SelectAttack(range));}
                    break;
                case SumiEnemyState.Windup:
                    // Tracking stops before release: a committed attack can be sidestepped.
                    if(elapsed<WindupDuration-.18f)Face(dir,dt,kind==SumiEnemyKind.Shade?190:135);
                    attackDirection=transform.forward;SmoothMove(Vector3.zero,dt);
                    if(elapsed>=WindupDuration)Enter(SumiEnemyState.Strike);
                    break;
                case SumiEnemyState.Strike:
                    float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,CurrentAttack.ContactEnd,elapsed));
                    body.Move(attackDirection*Mathf.Max(0,travel-lastLunge)*CurrentAttack.lunge);lastLunge=travel;
                    if(!struck&&elapsed>=CurrentAttack.ContactStart&&before<=CurrentAttack.ContactEnd)TryContact();
                    // A deflection can change state during TryContact. Never overwrite its recoil.
                    if(state!=SumiEnemyState.Strike)break;
                    if(elapsed>=CurrentAttack.strike)
                    {
                        if(CurrentAttack.followUp!=null&&range<CurrentAttack.reach+1.4f)BeginAttack(CurrentAttack.followUp);
                        else BeginRecovery();
                    }
                    break;
                case SumiEnemyState.Recovery:
                    SmoothMove(kind==SumiEnemyKind.Shade?retreatDirection*1.65f:Vector3.zero,dt);
                    if(elapsed>=CurrentAttack.recovery){CurrentAttack=null;Enter(SumiEnemyState.Approach);}
                    break;
                case SumiEnemyState.Recoil:
                    SmoothMove(-dir*.32f,dt);
                    if(elapsed>=recoilDuration){CurrentAttack=null;Enter(SumiEnemyState.Approach);}
                    break;
            }
            animator.SetFloat(Speed,Mathf.Min(velocity.magnitude,2.8f),.14f,dt);
            UpdateWarning();
        }

        void Approach(Vector3 dir,float range,float dt)
        {
            int slot=director.OrbitIndex(this);float desired=kind==SumiEnemyKind.Shade?2.8f:kind==SumiEnemyKind.Oni?2.65f:2.35f;
            float speed=kind==SumiEnemyKind.Shade?3.25f:kind==SumiEnemyKind.Oni?2.3f:2.65f;
            Vector3 tangent=Vector3.Cross(Vector3.up,dir)*((slot&1)==0?1:-1);
            Vector3 wanted=range>desired+.12f?dir*speed:range<desired-.4f?-dir*.9f:tangent*(kind==SumiEnemyKind.Shade?1.4f:.7f);
            // Move flankers into the camera's view before they are allowed to attack.
            var cam=Camera.main;
            if(cam&&range<5&&Vector3.Dot(cam.transform.forward,transform.position-player.transform.position)<-.5f)
                wanted+=tangent*1.6f;
            foreach(var other in Active)
            {
                if(!other||other==this||other.dead)continue;
                Vector3 away=transform.position-other.transform.position;away.y=0;
                if(away.sqrMagnitude<1.4f&&away.sqrMagnitude>.001f)wanted+=away.normalized*(1.2f-away.magnitude)*2;
            }
            SmoothMove(wanted,dt);
        }

        SumiEnemyAttack SelectAttack(float range)
        {
            int index=attackCount++;
            if(kind==SumiEnemyKind.Shade)return SumiEnemyAttack.ShadeLunge;
            if(kind==SumiEnemyKind.Retainer)return index%3==2?SumiEnemyAttack.RetainerHeavy:SumiEnemyAttack.RetainerCut;
            if(index%3==1)return SumiEnemyAttack.OniSweep;
            if(index%3==2)return SumiEnemyAttack.OniHeavy;
            return Enraged?SumiEnemyAttack.OniTwin:SumiEnemyAttack.OniCut;
        }

        void BeginAttack(SumiEnemyAttack attack)
        {
            CurrentAttack=attack;attackDirection=transform.forward;Enter(SumiEnemyState.Windup);
            animator.SetFloat(AttackRate,clipLength*.25f/attack.windup);
            animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer."+(attack.arc==SumiSwordArc.Overhead?"HeavyAttack":"Attack1")),.08f,0,0);
            if(attack.unblockable)director.CombatHint("CRIMSON SWEEP  —  SPACE TO EVADE",1.8f);
            else if(attack.braced)director.CombatHint("BRACED  —  HEAVY CUT OR DEFLECT",1.5f);
        }

        void TryContact()
        {
            Vector3 to=player.transform.position-transform.position;to.y=0;
            if(to.magnitude>CurrentAttack.reach||Vector3.Angle(attackDirection,to)>CurrentAttack.halfAngle)return;
            if(Physics.Linecast(transform.position+Vector3.up,player.transform.position+Vector3.up,1<<8,QueryTriggerInteraction.Ignore))return;
            struck=true;
            player.combat.ReceiveEnemyHit(CurrentAttack.damage,0,this,player.transform.position+Vector3.up*1.1f,CurrentAttack.unblockable);
        }

        void BeginRecovery()
        {
            ReleaseToken();nextAttackAt=Time.time+CurrentAttack.recovery+(kind==SumiEnemyKind.Oni?(Enraged?.14f:.32f):.18f);
            Vector3 away=transform.position-player.transform.position;away.y=0;
            retreatDirection=(away.normalized+transform.right*((attackCount&1)==0?.8f:-.8f)).normalized;
            Enter(SumiEnemyState.Recovery);
        }

        void BeginPhase()
        {
            phasePending=false;Enraged=true;recoilDuration=.85f;nextAttackAt=Time.time+1;
            ReleaseToken();CurrentAttack=null;Enter(SumiEnemyState.Recoil);
            director.CombatHint("THE ONI AWAKENS  —  WATCH THE RETURN CUT",3);
            SumiCombatFeedback.Hit(.06f,.22f,transform.position+Vector3.up);
        }

        void Enter(SumiEnemyState next)
        {
            state=next;elapsed=0;lastLunge=0;struck=false;
            if(next==SumiEnemyState.Strike)
            {
                animator.SetFloat(AttackRate,clipLength*.75f/CurrentAttack.strike);
                SumiCombatFeedback.EnemySwing(kind==SumiEnemyKind.Shade);
            }
            else if(next==SumiEnemyState.Recoil)animator.CrossFadeInFixedTime("Hit",.055f);
            else if(next==SumiEnemyState.Approach||next==SumiEnemyState.Observe||next==SumiEnemyState.Recovery)
                animator.CrossFadeInFixedTime("Locomotion",.12f);
        }

        void SmoothMove(Vector3 wanted,float dt)
        {
            velocity=Vector3.SmoothDamp(velocity,wanted,ref smoothVelocity,.13f,5,dt);
            body.Move((velocity+Vector3.down*2)*dt);
        }
        void Face(Vector3 dir,float dt,float degrees){if(dir.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(dir),degrees*dt);}

        public void TakeHit(float damage,Vector3 direction,SumiHitKind hitKind)
        {
            if(dead)return;
            health-=damage;
            if(health<=0){Die(hitKind==SumiHitKind.DashCut);return;}
            if(kind==SumiEnemyKind.Oni&&!Enraged&&health<=maxHealth*.55f)phasePending=true;
            bool strong=hitKind==SumiHitKind.HeavyCut||hitKind==SumiHitKind.Finisher;
            // Bracing resists light hits, but takes full damage. Heavy cuts break the windup.
            if(Braced&&!(hitKind==SumiHitKind.HeavyCut&&state==SumiEnemyState.Windup))
            {SumiCombatFeedback.Block(transform.position+Vector3.up*1.3f);return;}
            // Do not restart a punish window with each hit; repeated light attacks cannot freeze an enemy.
            if(state==SumiEnemyState.Recovery||state==SumiEnemyState.Recoil)return;
            if(!strong&&Time.time<staggerReadyAt)return;
            staggerReadyAt=Time.time+(kind==SumiEnemyKind.Oni?1.25f:.85f);
            Recoil(strong?.58f:.28f);
        }

        public void Parried(bool perfect)
        {
            if(!perfect||dead)return;
            health-=12;if(health<=0){Die(false);return;}
            if(kind==SumiEnemyKind.Oni&&!Enraged&&health<=maxHealth*.55f)phasePending=true;
            Recoil(.95f);director.CombatHint("OPENING  —  STRIKE",.85f);
        }

        void Recoil(float duration)
        {
            ReleaseToken();recoilDuration=duration;nextAttackAt=Time.time+duration+.18f;
            velocity=Vector3.zero;smoothVelocity=Vector3.zero;CurrentAttack=null;
            Enter(SumiEnemyState.Recoil);if(warning)warning.enabled=false;
        }

        public void GetSwordPose(out Vector3 grip,out Vector3 line)
        {
            if(CurrentAttack==null){grip=transform.position+Vector3.up*1.3f;line=transform.forward;return;}
            float t=state==SumiEnemyState.Windup?0:state==SumiEnemyState.Recovery?1:Mathf.InverseLerp(0,CurrentAttack.strike,elapsed);
            CurrentAttack.LocalPose(t,out grip,out line);
            float scale=kind==SumiEnemyKind.Oni?1.32f:kind==SumiEnemyKind.Shade?.96f:1.04f;
            grip=transform.TransformPoint(grip*scale);line=transform.TransformDirection(line);
        }

        void UpdateWarning()
        {
            if(!warning)return;
            warning.enabled=state==SumiEnemyState.Windup;
            if(!warning.enabled||CurrentAttack==null)return;
            warning.sharedMaterial=CurrentAttack.unblockable?SumiArt.Crimson:Braced?SumiArt.Gilt:SumiArt.Black;
            float width=Mathf.Lerp(.022f,.065f,Mathf.Clamp01(elapsed/WindupDuration));warning.startWidth=width;warning.endWidth=width*.6f;
            // The fan marks contact range after the lunge; it stops turning with the attack.
            Vector3 origin=transform.position+attackDirection*CurrentAttack.lunge+Vector3.up*.045f;
            warningPoints[0]=origin;
            for(int i=1;i<warningPoints.Length-1;i++)
            {
                float angle=Mathf.Lerp(-CurrentAttack.halfAngle,CurrentAttack.halfAngle,(i-1f)/(warningPoints.Length-3));
                warningPoints[i]=origin+Quaternion.AngleAxis(angle,Vector3.up)*attackDirection*CurrentAttack.reach;
            }
            warningPoints[warningPoints.Length-1]=origin;warning.SetPositions(warningPoints);
        }

        public void Execute(){if(dead)return;health=0;Die(false);}
        void Die(bool split)
        {
            dead=true;ReleaseToken();state=SumiEnemyState.Dead;CurrentAttack=null;if(warning)warning.enabled=false;
            if(body)body.enabled=false;
            if(split&&kind!=SumiEnemyKind.Oni)SplitAtWaist();else animator.CrossFadeInFixedTime("Death",.08f);
            if(player.target==transform){player.target=null;player.locked=false;}
            if(director)director.EnemyDied(this);Destroy(gameObject,2.4f);
        }
        void ReleaseToken(){if(hasToken&&director)director.ReleaseAttack(this);hasToken=false;}
        void SplitAtWaist()
        {
            if(!visual)return;float cutY=transform.position.y+.94f;
            var upper=Instantiate(visual.gameObject,visual.position,visual.rotation);upper.name="Upper ink half";
            var lower=Instantiate(visual.gameObject,visual.position,visual.rotation);lower.name="Lower ink half";
            SumiEnemyAppearance.PrepareSplit(upper,cutY,1);SumiEnemyAppearance.PrepareSplit(lower,cutY,-1);
            upper.AddComponent<SumiInkSplitPiece>().Setup(new Vector3(.35f,.72f,.08f),.75f);
            lower.AddComponent<SumiInkSplitPiece>().Setup(new Vector3(-.10f,-.12f,-.04f),.75f);
            visual.gameObject.SetActive(false);Destroy(upper,1);Destroy(lower,1);
        }
    }
}
