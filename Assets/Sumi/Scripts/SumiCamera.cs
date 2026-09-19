using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
namespace Sumi
{
    public class SumiCamera : MonoBehaviour
    {
        public SumiPlayer player;public Transform cinematicTarget;public float yaw,pitch=11,distance=5.65f;public bool reducedMotion;
        public CinemachineCamera virtualCamera;
        public float lookSensitivity=.115f,guardLookSensitivity=.15f;
        public float impulse;float cinematicUntil,cinematicWeight,walkPhase,walkWeight,strikeOffset;Vector3 smoothPosition;bool poseReady;
        // Lock-on steering. The mouse always outranks it; the automatic bearing only fills the gap
        // the player leaves, and it moves under a rate limit so acquiring never snaps the view.
        float lockWeight,manualUntil,manualAuthority=1,lockBearing,yawVelocity,pitchVelocity,followRate=16f;
        Vector3 focusLead,focusLeadVelocity;
        const float LockEngage=.40f,LockRelease=.22f,ManualHold=.16f,LockRange=18f,MinLockSpan=1.15f;
        const float YawSmooth=.20f,MaxYawRate=420f,YawDeadzone=1.1f,PitchSmooth=.34f,MaxPitchRate=75f;
        public void Kick(float strength){impulse=Mathf.Max(impulse,reducedMotion?strength*.15f:strength);}
        public void Frame(Transform target,float duration){cinematicTarget=target;cinematicUntil=Time.unscaledTime+duration;}
        // Pressing lock is an explicit request for the camera to take the bearing, so the hold that
        // normally protects mouse aiming is dropped; the rate limit still keeps the swing smooth.
        public void EngageLock(){manualUntil=0;}
        void LateUpdate()
        {
            if(!player)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            var combatState=player.combat?player.combat.state:SumiCombatState.Free;
            bool guarding=combatState==SumiCombatState.GuardStartup||combatState==SumiCombatState.GuardHeld;

            Vector2 look=Cursor.lockState==CursorLockMode.Locked&&Mouse.current!=null?Mouse.current.delta.ReadValue():Vector2.zero;
            if(look.sqrMagnitude>.25f)
            {
                yaw+=look.x*(guarding?guardLookSensitivity:lookSensitivity);
                pitch=Mathf.Clamp(pitch-look.y*.07f,-4,54);
                // Take the axes back immediately, and drop any automatic momentum with them so the
                // two controllers can never fight over the same degree.
                manualUntil=Time.unscaledTime+ManualHold;yawVelocity=0;pitchVelocity=0;
            }
            manualAuthority=Mathf.MoveTowards(manualAuthority,Time.unscaledTime<manualUntil?0:1,dt*3.1f);

            bool canWalk=combatState==SumiCombatState.Free||combatState==SumiCombatState.GuardStartup||combatState==SumiCombatState.GuardHeld;
            float walking=canWalk?Mathf.InverseLerp(.15f,3.3f,player.velocity.magnitude):0;
            walkWeight=Mathf.MoveTowards(walkWeight,walking,dt*3.2f);
            if(walkWeight>.01f)walkPhase+=dt*Mathf.Lerp(4.2f,7.2f,walking);
            float step=Mathf.Sin(walkPhase),rise=Mathf.Abs(Mathf.Cos(walkPhase));
            Vector3 focus=player.transform.position+Vector3.up*(1.3f+rise*.018f*walkWeight);

            Transform target=player.locked?player.target:null;
            Vector3 flat=Vector3.zero;float span=0;
            if(target){flat=target.position-player.transform.position;flat.y=0;span=flat.magnitude;}
            bool steering=target&&span<=LockRange;
            lockWeight=Mathf.MoveTowards(lockWeight,steering?1:0,dt/(steering?LockEngage:LockRelease));
            // Atan2 is meaningless once the two capsules overlap, so the last stable bearing is held
            // instead of recomputed; dashing straight through a foe no longer spins the camera.
            if(steering&&span>MinLockSpan)lockBearing=Mathf.Atan2(flat.x,flat.z)*Mathf.Rad2Deg;

            // Guard footwork needs a free orbit. Lock-on still frames the foe but yields most of the yaw.
            float authority=lockWeight*manualAuthority*(guarding?.45f:1f);
            if(authority>.002f)
            {
                float error=Mathf.DeltaAngle(yaw,lockBearing);
                // A dead zone stops an orbiting foe from buzzing the view one degree at a time.
                if(Mathf.Abs(error)>YawDeadzone)
                {
                    float goal=yaw+error-Mathf.Sign(error)*YawDeadzone;
                    yaw=Mathf.SmoothDampAngle(yaw,goal,ref yawVelocity,YawSmooth/authority,MaxYawRate,dt);
                }
                else yawVelocity=Mathf.MoveTowards(yawVelocity,0,dt*240f);
                // Closer foes want a steeper look so the footwork stays on screen.
                if(Time.unscaledTime>=manualUntil)
                {
                    float framed=Mathf.Lerp(21f,9f,Mathf.InverseLerp(2.2f,11f,span));
                    pitch=Mathf.Clamp(Mathf.SmoothDampAngle(pitch,framed,ref pitchVelocity,PitchSmooth/authority,MaxPitchRate,dt),-4,54);
                }
            }
            else yawVelocity=Mathf.MoveTowards(yawVelocity,0,dt*240f);

            // Clamp and smooth the lead so passing a foe at dash speed cannot yank the focus point.
            Vector3 wantedLead=steering?Vector3.ClampMagnitude(flat*(guarding?.12f:.22f),1.45f)*lockWeight:Vector3.zero;
            focusLead=Vector3.SmoothDamp(focusLead,wantedLead,ref focusLeadVelocity,.13f,26f,dt);
            focus+=focusLead;

            bool striking=player.combat&&player.combat.IsAttacking;
            bool dashing=combatState==SumiCombatState.Dash||combatState==SumiCombatState.DashStrike;
            float zoom=distance+(dashing?.23f:0);
            if(steering)zoom+=Mathf.Clamp((span-2.3f)*.14f,0,.65f)*lockWeight;
            // The cinematic pull eases both ways; expiring used to drop the focus in a single frame.
            float cinematic=cinematicTarget&&Time.unscaledTime<cinematicUntil?1:0;
            cinematicWeight=Mathf.MoveTowards(cinematicWeight,cinematic,dt/(cinematic>0?.16f:.26f));
            if(cinematicWeight>.002f&&cinematicTarget)
            {
                focus=Vector3.Lerp(focus,cinematicTarget.position+Vector3.up*1.2f,.45f*cinematicWeight);
                zoom=Mathf.Lerp(zoom,4.2f,cinematicWeight);
            }
            Quaternion angle=Quaternion.Euler(pitch,yaw,0);Vector3 desired=focus-angle*Vector3.forward*zoom+angle*Vector3.right*.5f;
            var ray=desired-focus; if(Physics.SphereCast(focus,.24f,ray.normalized,out var hit,ray.magnitude,1<<8,QueryTriggerInteraction.Ignore))desired=focus+ray.normalized*Mathf.Max(.65f,hit.distance-.12f);
            impulse=Mathf.MoveTowards(impulse,0,dt*1.6f);var noise=new Vector3(Mathf.Sin(Time.unscaledTime*23),Mathf.Cos(Time.unscaledTime*19),0)*impulse*.18f;
            strikeOffset=Mathf.MoveTowards(strikeOffset,striking?.22f:0,dt*(striking?2.7f:1.8f));
            desired+=angle*Vector3.right*(step*.012f*walkWeight+strikeOffset);
            // Easing the follow constant keeps the dash entry and exit from stepping the smoothing.
            followRate=Mathf.MoveTowards(followRate,dashing?11f:16f,dt*26f);
            if(!poseReady){smoothPosition=desired;poseReady=true;}smoothPosition=Vector3.Lerp(smoothPosition,desired,1-Mathf.Exp(-followRate*dt));transform.position=smoothPosition+noise;
            transform.rotation=Quaternion.LookRotation(focus-smoothPosition)*Quaternion.Euler(0,0,Mathf.Sin(Time.unscaledTime*21)*impulse*1.5f);
            if(virtualCamera){virtualCamera.transform.SetPositionAndRotation(transform.position,transform.rotation);}
        }
    }
}
