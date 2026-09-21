using System.Collections.Generic;
using UnityEngine;

namespace Sumi
{
    // Sole authority for action legality, time, movement and sword contact.
    public sealed class SumiPlayerCombat : MonoBehaviour
    {
        public SumiCombatState state=SumiCombatState.Free;
        public float health=100,maxHealth=100;
        public bool showCombatDebug;
        public SumiAttackDefinition opening,heavy,flash;
        public SumiAttackDefinition CurrentAttack { get; private set; }
        public int ComboStep=>IsAttacking?comboStep:0;
        public float StateTime=>elapsed;
        public bool IsAttacking=>CurrentAttack!=null;
        public bool HitConfirmed { get; private set; }
        public float SwordPoseWeight=>CurrentAttack?
            (blendPose?1:Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,CurrentAttack.activeWindow.x*.65f,elapsed)))*
            (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(CurrentAttack.duration-.10f,CurrentAttack.duration,elapsed))):IsGuarding?1:0;
        public bool IsGuarding=>state==SumiCombatState.GuardStartup||state==SumiCombatState.GuardHeld;
        public bool Perfect=>state==SumiCombatState.GuardStartup&&elapsed>=.025f&&elapsed<=.145f;
        public bool BladeActive=>CurrentAttack&&elapsed>=CurrentAttack.activeWindow.x&&elapsed<=CurrentAttack.activeWindow.y;
        public float ThrowCooldownRemaining=>Mathf.Max(0,nextThrowAt-Time.time);
        public bool Invulnerable=>state==SumiCombatState.DashStrike&&elapsed>=.055f&&elapsed<=.29f;
        public SumiEnemy ExecutionTarget=>executionTarget&&executionTarget.CanExecute?executionTarget:null;

        const float InputLifetime=.20f,DashLifetime=.18f,FlashDistance=3.95f;
        SumiPlayer player;SumiHumanoidRonin human;
        SumiCombatInputBuffer input;
        float elapsed,dashAt=-99,nextDashAt,nextThrowAt,damageGraceUntil,recovery,guardAt=-99;
        int comboStep;bool guardHeld,impactPlayed;
        Vector3 strikeDir,dashDir;float travelDistance;
        Vector3 transitionGrip,transitionLine;bool blendPose;
        SumiEnemy executionTarget,assistedTarget;
        bool secondBreath,steadyHeart,splitInk,deepInk,quickInk;
        readonly HashSet<int> hitIds=new HashSet<int>();
        readonly Collider[] hits=new Collider[64];

        public void Init(SumiPlayer p,SumiHumanoidRonin h)
        {
            player=p;human=h;
            opening=Resources.Load<SumiAttackDefinition>("Sumi/Attacks/Opening");
            heavy=Resources.Load<SumiAttackDefinition>("Sumi/Attacks/Heavy");
            flash=Resources.Load<SumiAttackDefinition>("Sumi/Attacks/Flash");
            if(!opening||!heavy||!flash){Debug.LogError("Missing Sumi sword definitions. Run Sumi/Author Combat Clips.",this);enabled=false;}
        }

        public void ClearInput(){input.Clear();dashAt=guardAt=-99;guardHeld=false;}

        public void Tick(Vector2 axis,bool attack,bool guardDown,bool guardPressed,bool dash,bool execute=false,bool throwInk=false,bool heavyPressed=false)
        {
            Advance(axis,attack,guardDown,guardPressed,dash,execute,throwInk,heavyPressed,Time.unscaledTime,Time.deltaTime);
        }

        // An explicit frame clock keeps timing reproducible in the editor regression harness.
        void Advance(Vector2 axis,bool attack,bool guardDown,bool guardPressed,bool dash,bool execute,bool throwInk,bool heavyPressed,float now,float dt)
        {
            if(!enabled||!player||state==SumiCombatState.Dead){ClearInput();return;}
            if(!player.controllable||SumiTime.MenuPaused){ClearInput();return;}
            guardHeld=guardDown;
            if(state!=SumiCombatState.HitStun)
            {
                if(attack||heavyPressed)input.Press(heavyPressed?SumiAttackInput.Heavy:SumiAttackInput.Light,now,InputLifetime);
                if(dash)dashAt=now;
                if(guardPressed)guardAt=now;
            }
            input.Peek(now);
            // Sample inputs during hit stop, but never advance states or change the frozen pose.
            if(dt<=0||Time.timeScale<=0)return;
            float before=elapsed;elapsed+=dt;
            FindExecutionTarget();
            if(execute&&ExecutionTarget&&(state==SumiCombatState.Free||IsGuarding)){Execute(ExecutionTarget);return;}
            if(throwInk&&Time.time>=nextThrowAt&&(state==SumiCombatState.Free||IsGuarding)){ThrowInk();nextThrowAt=Time.time+(quickInk?1.7f:3.5f);}

            if(IsAttacking)
            {
                var move=CurrentAttack;
                Vector3 oldPosition=transform.position;Quaternion oldRotation=transform.rotation;
                if(state==SumiCombatState.DashStrike)player.Face(dashDir,dt,720);
                else
                {
                    Vector3 wanted=axis.sqrMagnitude>.1f?player.WorldDirection(axis):strikeDir;
                    if(assistedTarget&&!assistedTarget.dead&&elapsed<move.activeWindow.x)
                    {
                        Vector3 to=assistedTarget.transform.position-transform.position;to.y=0;
                        if(to.sqrMagnitude<10&&Vector3.Angle(strikeDir,to)<40)wanted=to.normalized;
                    }
                    float steering=elapsed<move.activeWindow.x?move.turnSpeed:elapsed<move.activeWindow.y?75:move.turnSpeed*.7f;
                    strikeDir=Vector3.RotateTowards(strikeDir,wanted,steering*Mathf.Deg2Rad*dt,0);
                    player.Face(strikeDir,dt,steering);
                }
                float lunge=(move.Travel(elapsed)-move.Travel(before))*travelDistance;
                Vector3 displacement=(state==SumiCombatState.DashStrike?dashDir:strikeDir)*Mathf.Max(0,lunge);
                player.AttackMove(axis,state==SumiCombatState.DashStrike?0:move.Movement(elapsed),displacement,dt);
                player.dodgeRemaining=state==SumiCombatState.DashStrike?Mathf.Max(0,move.duration-elapsed):0;
                SweepBlade(before,elapsed,oldPosition,oldRotation);

                bool canDodge=elapsed>=move.dodgeCancel||(HitConfirmed&&elapsed>=move.hitDodgeCancel);
                if(canDodge&&now-dashAt<=DashLifetime&&Time.time>=nextDashAt){StartFlash(axis);return;}
                if(elapsed>=move.guardCancel&&guardHeld){StartGuard(now);return;}
                var request=input.Peek(now);
                // Crossing the link start still works when a slow frame skips the whole window.
                bool link=move.CanLink(elapsed)||(before<move.linkWindow.x&&elapsed>=move.linkWindow.x);
                var follow=request==SumiAttackInput.Heavy?move.heavyFollowUp:request==SumiAttackInput.Light?move.lightFollowUp:null;
                if(link&&follow){StartAttack(follow,axis,Mathf.Min(3,comboStep+1));return;}
                if(elapsed>=move.duration)
                {
                    // A terminal recovery can buffer the next opener, but never resume an old chain.
                    Enter(SumiCombatState.Free,"Locomotion",.10f);
                    if(request!=SumiAttackInput.None){StartAttack(request==SumiAttackInput.Heavy?heavy:opening,axis,1);return;}
                }
                return;
            }

            switch(state)
            {
                case SumiCombatState.Free:
                    if(now-dashAt<=DashLifetime&&Time.time>=nextDashAt){StartFlash(axis);return;}
                    if(guardHeld){StartGuard(now);return;}
                    if(TryAttack(axis,now))return;
                    player.FreeMove(axis,dt);break;
                case SumiCombatState.GuardStartup:
                case SumiCombatState.GuardHeld:
                    player.GuardMove(axis,dt);
                    if(player.locked&&player.target)player.Face(TargetDirection(),dt,380);
                    if(now-dashAt<=DashLifetime&&Time.time>=nextDashAt){StartFlash(axis);return;}
                    if(!guardHeld)
                    {
                        if(TryAttack(axis,now))return;
                        Enter(SumiCombatState.GuardRecovery,null,0);return;
                    }
                    if(state==SumiCombatState.GuardStartup&&elapsed>=.16f){state=SumiCombatState.GuardHeld;elapsed=0;}
                    break;
                case SumiCombatState.GuardRecovery:
                    player.FreeMove(axis,dt);
                    if(now-dashAt<=DashLifetime&&Time.time>=nextDashAt){StartFlash(axis);return;}
                    if(TryAttack(axis,now))return;
                    if(elapsed>=.10f){if(guardHeld)StartGuard(now);else Enter(SumiCombatState.Free,"Locomotion",.1f);}break;
                case SumiCombatState.HitStun:
                    player.CombatBrake(dt);
                    if(elapsed>=recovery)Enter(SumiCombatState.Free,"Locomotion",.12f);break;
                default:Enter(SumiCombatState.Free,"Locomotion",.1f);break;
            }
        }

        bool TryAttack(Vector2 axis,float now)
        {
            var request=input.Peek(now);if(request==SumiAttackInput.None)return false;
            StartAttack(request==SumiAttackInput.Heavy?heavy:opening,axis,1);return true;
        }
        void StartGuard(float now)
        {
            // Holding through a recovery grants a block, never a delayed perfect parry.
            Enter(now-guardAt<=.15f?SumiCombatState.GuardStartup:SumiCombatState.GuardHeld,"Locomotion",.08f);
            input.Clear();guardAt=-99;
        }
        void StartAttack(SumiAttackDefinition move,Vector2 axis,int step)
        {
            bool carry=IsAttacking||IsGuarding;
            GetSwordPose(out var oldGrip,out var oldLine);
            SumiCombatState next=move==flash?SumiCombatState.DashStrike:move==heavy?SumiCombatState.HeavyAttack:
                step==1?SumiCombatState.Attack1:step==2?SumiCombatState.Attack2:SumiCombatState.Attack3;
            Enter(next,null,0);CurrentAttack=move;comboStep=step;input.Clear();
            blendPose=carry;transitionGrip=transform.InverseTransformPoint(oldGrip);transitionLine=transform.InverseTransformDirection(oldLine);
            strikeDir=axis.sqrMagnitude>.1f?player.WorldDirection(axis):transform.forward;
            assistedTarget=FindAssist(strikeDir);
            if(assistedTarget)
            {
                Vector3 to=assistedTarget.transform.position-transform.position;to.y=0;
                strikeDir=Vector3.RotateTowards(strikeDir,to.normalized,35*Mathf.Deg2Rad,0);
            }
            travelDistance=move.lunge;
            if(assistedTarget)travelDistance=Mathf.Min(travelDistance,Mathf.Max(0,Vector3.Distance(transform.position,assistedTarget.transform.position)-1.1f));
            human.PlayCombat(move);
            SumiCombatFeedback.Swing(move.finisher||move==flash);
        }
        void StartFlash(Vector2 axis)
        {
            StartAttack(flash,axis,0);dashAt=-99;nextDashAt=Time.time+.82f;
            dashDir=axis.sqrMagnitude>.02f?player.WorldDirection(axis):strikeDir;
            travelDistance=FlashDistance;
            foreach(var enemy in SumiEnemy.Active)
            {
                if(enemy.dead)continue;Vector3 to=enemy.transform.position-transform.position;to.y=0;
                float distance=to.magnitude;
                if(distance>.01f&&distance<FlashDistance+1&&Vector3.Dot(to/distance,dashDir)>.8f)
                    travelDistance=Mathf.Min(travelDistance,Mathf.Max(0,distance-.9f));
            }
            SumiCombatFeedback.Dash(dashDir);
            SumiCombatFeedback.DashStroke(transform.position,transform.position+dashDir*travelDistance);
        }
        void Enter(SumiCombatState next,string animation,float fade)
        {
            CurrentAttack=null;elapsed=0;state=next;comboStep=0;HitConfirmed=false;impactPlayed=false;hitIds.Clear();blendPose=false;
            if(next==SumiCombatState.HitStun||next==SumiCombatState.Dead)ClearInput();
            player.BeginCombatMotion();player.dodgeRemaining=0;
            if(animation!=null)human.PlayCombat(animation,fade);
        }

        SumiEnemy FindAssist(Vector3 direction)
        {
            SumiEnemy best=null;float score=10;
            foreach(var e in SumiEnemy.Active)
            {
                if(e.dead)continue;Vector3 to=e.transform.position-transform.position;to.y=0;
                float distance=to.magnitude,angle=Vector3.Angle(direction,to);
                if(distance>3.1f||angle>40||Physics.Linecast(transform.position+Vector3.up,e.transform.position+Vector3.up,1<<8,QueryTriggerInteraction.Ignore))continue;
                float candidate=distance+angle*.035f-(player.locked&&player.target==e.transform?.65f:0);
                if(candidate<score){score=candidate;best=e;}
            }
            return best;
        }
        Vector3 TargetDirection()
        {
            if(player.target){Vector3 to=player.target.position-transform.position;to.y=0;if(to.sqrMagnitude>.001f&&to.sqrMagnitude<100)return to.normalized;}
            return transform.forward;
        }
        void LocalSwordPose(float time,out Vector3 grip,out Vector3 line)
        {
            if(CurrentAttack)
            {
                CurrentAttack.LocalPose(time,out grip,out line);
                if(blendPose)
                {
                    float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/Mathf.Min(CurrentAttack.activeWindow.x,CurrentAttack.blend)));
                    grip=Vector3.Lerp(transitionGrip,grip,blend);line=Vector3.Slerp(transitionLine,line,blend).normalized;
                }
            }
            else {grip=new Vector3(.17f,1.65f,.18f);line=new Vector3(-.83f,.53f,.17f).normalized;}
        }
        public void GetSwordPose(out Vector3 grip,out Vector3 line)
        {
            LocalSwordPose(elapsed,out grip,out line);grip=transform.TransformPoint(grip);line=transform.TransformDirection(line);
        }
        void BladeAt(float time,Vector3 position,Quaternion rotation,out Vector3 a,out Vector3 b)
        {
            LocalSwordPose(time,out var grip,out var line);
            a=position+rotation*(grip+line*.08f);b=position+rotation*(grip+line*1.13f);
        }
        void SweepBlade(float before,float after,Vector3 oldPosition,Quaternion oldRotation)
        {
            var move=CurrentAttack;if(!move.CrossesActive(before,after))return;
            float start=Mathf.Max(before,move.activeWindow.x),end=Mathf.Min(after,move.activeWindow.y);
            // Subdivide time and the full blade, covering fast arcs and hitches without an invisible forward hitbox.
            int steps=Mathf.Max(1,Mathf.CeilToInt((end-start)/.008f));
            float fraction=Mathf.InverseLerp(before,after,start);
            BladeAt(start,Vector3.Lerp(oldPosition,transform.position,fraction),Quaternion.Slerp(oldRotation,transform.rotation,fraction),out var oldA,out var oldB);
            Trace(oldA,oldB,.14f);
            for(int i=1;i<=steps;i++)
            {
                float sample=Mathf.Lerp(start,end,i/(float)steps);fraction=Mathf.InverseLerp(before,after,sample);
                BladeAt(sample,Vector3.Lerp(oldPosition,transform.position,fraction),Quaternion.Slerp(oldRotation,transform.rotation,fraction),out var a,out var b);
                Trace(a,b,.14f);
                for(int j=0;j<=5;j++)Trace(Vector3.Lerp(oldA,oldB,j/5f),Vector3.Lerp(a,b,j/5f),.14f);
                oldA=a;oldB=b;
            }
        }
        void Trace(Vector3 from,Vector3 to,float radius)
        {
            int count=Physics.OverlapCapsuleNonAlloc(from,to,radius,hits,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var enemy=hits[i].GetComponentInParent<SumiEnemy>();
                if(!enemy||enemy.dead||hitIds.Contains(enemy.GetInstanceID()))continue;
                Vector3 contact=hits[i].ClosestPoint((from+to)*.5f);
                if(Physics.Linecast(transform.position+Vector3.up*1.2f,contact,1<<8,QueryTriggerInteraction.Ignore))continue;
                hitIds.Add(enemy.GetInstanceID());HitConfirmed=true;
                var kind=state==SumiCombatState.DashStrike?SumiHitKind.DashCut:CurrentAttack==heavy?SumiHitKind.HeavyCut:CurrentAttack.finisher?SumiHitKind.Finisher:SumiHitKind.ShoulderCut;
                enemy.TakeHit(CurrentAttack.damage,(enemy.transform.position-transform.position).normalized,kind);
                if(!impactPlayed){SumiCombatFeedback.Hit(CurrentAttack.hitStop,CurrentAttack.cameraKick,contact);impactPlayed=true;}
                if(kind==SumiHitKind.DashCut)SumiCombatFeedback.WaistCut(enemy,contact);
            }
        }

        public void ReceiveEnemyHit(float damage,float blockCost,SumiEnemy enemy,Vector3 contact,bool unblockable=false)
        {
            if(state==SumiCombatState.Dead||Time.time<damageGraceUntil||Invulnerable)return;
            Vector3 to=enemy.transform.position-transform.position;to.y=0;
            bool facing=Vector3.Dot(transform.forward,to.normalized)>.05f;
            if(Perfect&&facing&&!unblockable){enemy.Parried(true);SumiCombatFeedback.Parry(contact,to);return;}
            if(IsGuarding&&facing&&!unblockable)
            {
                // A held block mitigates damage; only a timed deflection stops the attacker.
                health=Mathf.Max(0,health-damage*.18f*(steadyHeart?.8f:1));damageGraceUntil=Time.time+.18f;
                SumiCombatFeedback.Block(contact);NotifyDamage();return;
            }
            Damage(damage,.26f,contact);
        }
        public void ReceiveWorldHit(float damage,Vector3 contact)
        {
            if(state==SumiCombatState.Dead||Invulnerable||Time.time<damageGraceUntil)return;
            if(Perfect){SumiCombatFeedback.Parry(contact,Vector3.right);return;}
            Damage(damage,.22f,contact);
        }
        void Damage(float damage,float stun,Vector3 contact)
        {
            if(steadyHeart)damage*=.8f;health=Mathf.Max(0,health-damage);damageGraceUntil=Time.time+.52f;recovery=stun;
            Enter(health<=0?SumiCombatState.Dead:SumiCombatState.HitStun,health<=0?"Death":"Hit",.045f);
            SumiCombatFeedback.Hit(.045f,.26f,contact);NotifyDamage();
        }
        void NotifyDamage(){if(health<=0&&state!=SumiCombatState.Dead)Enter(SumiCombatState.Dead,"Death",.08f);if(SumiGame.I&&SumiGame.I.run)SumiGame.I.run.PlayerDamaged();}
        void FindExecutionTarget()
        {
            executionTarget=null;float best=2.1f;
            foreach(var e in SumiEnemy.Active)
            {
                if(!e.CanExecute)continue;float distance=Vector3.Distance(transform.position,e.transform.position);
                if(distance<best&&!Physics.Linecast(transform.position+Vector3.up,e.transform.position+Vector3.up,1<<8)){best=distance;executionTarget=e;}
            }
        }
        void Execute(SumiEnemy e)
        {
            Vector3 d=e.transform.position-transform.position;d.y=0;if(d.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(d);
            player.MoveCombat(d.normalized*Mathf.Max(0,d.magnitude-1.05f));e.Execute();if(secondBreath)health=Mathf.Min(maxHealth,health+10);
            StartAttack(heavy,Vector2.zero,3);SumiCombatFeedback.Hit(.09f,.3f,e.transform.position+Vector3.up);
        }
        void ThrowInk()
        {
            Vector3 direction=TargetDirection();
            if(!player.target){float best=18;foreach(var e in SumiEnemy.Active){if(!e||e.dead)continue;Vector3 d=e.transform.position-transform.position;d.y=0;float range=d.magnitude;if(range<best&&range>0&&Vector3.Dot(d/range,transform.forward)>.55f){best=range;direction=d/range;}}}
            Vector3 origin=transform.position+Vector3.up*1.25f+direction*.5f;
            int count=splitInk?2:1;for(int i=0;i<count;i++){float angle=count==1?0:(i==0?-8:8);Vector3 flight=Quaternion.AngleAxis(angle,Vector3.up)*direction;new GameObject("Ink dart").AddComponent<SumiInkDart>().Init(origin,flight,deepInk?34:22);}
            SumiCombatFeedback.Swing(false);
        }
        public void ApplyUpgrade(int id){if(id==0){maxHealth+=25;health=Mathf.Min(maxHealth,health+25);}else if(id==1)secondBreath=true;else if(id==2)steadyHeart=true;else if(id==3)splitInk=true;else if(id==4)deepInk=true;else if(id==5)quickInk=true;}
        void OnGUI()
        {
            if(!showCombatDebug)return;
            string phase=!CurrentAttack?state.ToString():elapsed<CurrentAttack.activeWindow.x?"STARTUP":BladeActive?"ACTIVE":"RECOVERY";
            GUI.Box(new Rect(16,70,410,85),$"{phase}   {elapsed:0.000}s   Hit: {HitConfirmed}\nLink: {(CurrentAttack&&CurrentAttack.CanLink(elapsed))}   Buffered: {input.Peek(Time.unscaledTime)}\nGuard cancel: {(CurrentAttack&&elapsed>=CurrentAttack.guardCancel)}   Step: {ComboStep}");
        }
    }
}
