using UnityEngine;
using UnityEngine.InputSystem;
namespace Sumi
{
    public class SumiPlayer : MonoBehaviour
    {
        public SumiConfig config;public SumiRig rig;public CharacterController motor;
        [HideInInspector] public SumiPlayerCombat combat;
        public Transform target;public bool locked,controllable=true;
        public Vector3 velocity;public Vector2 injectedMove;
        // Automation hooks; normally false. They allow Play Mode verification to exercise the same input path as players.
        public bool injectedAttack,injectedGuard,injectedParry,injectedDash,injectedJog,injectedHeavy;
        public bool jogHeld;
        public float dodgeRemaining,dodgeReady,gravity;
        Vector3 dodgeDirection,combatMoveVelocity;
        public void Init(SumiConfig c)
        {config=c;motor=gameObject.AddComponent<CharacterController>();motor.height=1.9f;motor.radius=.32f;motor.center=new Vector3(0,.98f,0);motor.stepOffset=.2f;motor.slopeLimit=45;var human=gameObject.AddComponent<SumiHumanoidRonin>();human.Init(this);combat=gameObject.AddComponent<SumiPlayerCombat>();combat.Init(this,human);}
        public bool Invulnerable=>combat?combat.Invulnerable:dodgeRemaining>.07f&&dodgeRemaining<config.dodgeDuration-.025f;
        public bool Dodge(Vector3 direction)
        {if(!controllable||Time.time<dodgeReady||dodgeRemaining>0)return false;dodgeRemaining=config.dodgeDuration;dodgeReady=Time.time+config.dodgeCooldown;dodgeDirection=direction.sqrMagnitude>.01f?direction.normalized:transform.forward;return true;}
        protected virtual void Update()
        {
            if(!motor||!config)return;
            var keyboard=Keyboard.current;
            Vector2 axis=injectedMove;
            if(controllable&&keyboard!=null){axis+=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));if(combat==null&&keyboard.qKey.wasPressedThisFrame)locked=!locked;}
            if(!controllable)axis=Vector2.zero;
            if(combat)
            {
                var mouse=Mouse.current;
                bool attack=injectedAttack||(controllable&&mouse!=null&&mouse.leftButton.wasPressedThisFrame);
                jogHeld=injectedJog||(controllable&&keyboard!=null&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed));
                // Held guard remains available to verification via injectedGuard; players use Q or RMB to parry.
                bool guard=injectedGuard;
                bool parry=injectedParry||(controllable&&((keyboard!=null&&keyboard.qKey.wasPressedThisFrame)||(mouse!=null&&mouse.rightButton.wasPressedThisFrame)));
                bool dash=injectedDash||(controllable&&keyboard!=null&&keyboard.spaceKey.wasPressedThisFrame);
                bool execute=controllable&&keyboard!=null&&keyboard.eKey.wasPressedThisFrame;
                bool throwShuriken=controllable&&keyboard!=null&&keyboard.fKey.wasPressedThisFrame;
                bool heavy=injectedHeavy||(controllable&&((keyboard!=null&&keyboard.rKey.wasPressedThisFrame)||(mouse!=null&&mouse.middleButton.wasPressedThisFrame)));
                if(keyboard!=null&&keyboard.f3Key.wasPressedThisFrame)combat.showCombatDebug=!combat.showCombatDebug;
                injectedAttack=false;injectedParry=false;injectedDash=false;injectedHeavy=false;
                combat.Tick(axis,attack,guard,parry,dash,execute,throwShuriken,heavy);return;
            }
            Vector3 desired=WorldDirection(axis);
            if(controllable&&keyboard!=null&&keyboard.spaceKey.wasPressedThisFrame)Dodge(desired);
            float dt=Time.deltaTime;
            if(dodgeRemaining>0){dodgeRemaining=Mathf.Max(0,dodgeRemaining-dt);velocity=dodgeRemaining>0?dodgeDirection*config.dodgeSpeed:Vector3.zero;rig.pose=3;rig.poseTime=config.dodgeDuration-dodgeRemaining;rig.poseDuration=config.dodgeDuration;}
            else {velocity=Vector3.MoveTowards(velocity,desired*config.moveSpeed,config.acceleration*dt);rig.pose=0;}
            gravity=motor.isGrounded?-2:Mathf.Max(-25,gravity-30*dt);
            motor.Move((velocity+Vector3.up*gravity)*dt);
            Vector3 facing=locked&&target?target.position-transform.position:velocity;facing.y=0;
            if(facing.sqrMagnitude>.02f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-8.5f*dt));
            rig.speed=velocity.magnitude;
        }
        public Vector3 WorldDirection(Vector2 axis){Vector3 forward=Camera.main?Camera.main.transform.forward:Vector3.forward;forward.y=0;forward.Normalize();return Vector3.ClampMagnitude(forward*axis.y+Vector3.Cross(Vector3.up,forward)*axis.x,1);}
        public void FreeMove(Vector2 axis,float dt){var desired=WorldDirection(axis);velocity=Vector3.MoveTowards(velocity,desired*config.moveSpeed*(jogHeld&&axis.sqrMagnitude>.01f?1.55f:1f),config.acceleration*dt);gravity=motor.isGrounded?-2:Mathf.Max(-25,gravity-30*dt);motor.Move((velocity+Vector3.up*gravity)*dt);if(velocity.sqrMagnitude>.02f)Face(velocity,dt,360);rig.speed=velocity.magnitude;}
        public void GuardMove(Vector2 axis,float dt){var desired=WorldDirection(axis)*config.moveSpeed*.56f;velocity=Vector3.MoveTowards(velocity,desired,config.acceleration*1.1f*dt);gravity=motor.isGrounded?-2:Mathf.Max(-25,gravity-30*dt);motor.Move((velocity+Vector3.up*gravity)*dt);if(desired.sqrMagnitude>.02f)Face(desired,dt,470);rig.speed=velocity.magnitude;}
        public void BeginCombatMotion(){combatMoveVelocity=Vector3.ClampMagnitude(velocity,config.moveSpeed);}
        public void AttackMove(Vector2 axis,float movement,Vector3 lunge,float dt)
        {
            combatMoveVelocity=Vector3.MoveTowards(combatMoveVelocity,WorldDirection(axis)*config.moveSpeed*movement,config.acceleration*1.6f*dt);
            gravity=motor.isGrounded?-2:Mathf.Max(-25,gravity-30*dt);
            Vector3 before=transform.position;motor.Move(combatMoveVelocity*dt+lunge+Vector3.up*gravity*dt);
            velocity=(transform.position-before)/Mathf.Max(.001f,dt);velocity.y=0;if(rig)rig.speed=velocity.magnitude;
        }
        public void CombatBrake(float dt){velocity=Vector3.MoveTowards(velocity,Vector3.zero,config.acceleration*1.6f*dt);gravity=motor.isGrounded?-2:Mathf.Max(-25,gravity-30*dt);motor.Move((velocity+Vector3.up*gravity)*dt);if(rig)rig.speed=velocity.magnitude;}
        public void MoveCombat(Vector3 delta){gravity=motor.isGrounded?-2:Mathf.Max(-25,gravity-30*Time.deltaTime);motor.Move(delta+Vector3.up*gravity*Time.deltaTime);velocity=delta/Mathf.Max(.001f,Time.deltaTime);rig.speed=velocity.magnitude;}
        public void Face(Vector3 direction,float dt,float maxDegrees){direction.y=0;if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),maxDegrees*dt);}
    }
}
