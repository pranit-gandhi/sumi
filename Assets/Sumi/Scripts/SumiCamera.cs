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
        public float impulse;float cinematicUntil,cinematicWeight,cinematicInfluence=1,cinematicZoom,walkPhase,walkWeight,strikeOffset;Vector3 smoothPosition;bool poseReady;
        // Lock-on steering. The mouse always outranks it; the automatic bearing only fills the gap
        // the player leaves, and it moves under a rate limit so acquiring never snaps the view.
        float lockWeight,manualUntil,manualAuthority=1,lockBearing,yawVelocity,pitchVelocity,followRate=16f,volleyLift,volleyLiftTarget,volleyLiftVelocity;
        Vector3 focusLead,focusLeadVelocity,shakeDir=Vector3.up,swayOffset,swayVelocity;
        const float LockEngage=.40f,LockRelease=.22f,ManualHold=.16f,LockRange=18f,MinLockSpan=1.15f;
        const float YawSmooth=.20f,MaxYawRate=420f,YawDeadzone=1.1f,PitchSmooth=.34f,MaxPitchRate=75f;
        public void Kick(float strength)
        {
            float amp=reducedMotion?strength*.15f:strength;if(amp<.001f)return;
            impulse=Mathf.Max(impulse,amp);
            shakeDir=new Vector3(Random.Range(-.6f,.6f),Random.Range(.25f,1f),0).normalized;
        }
        // A dash lags the orbit opposite the lunge, then eases back; it is not an impact punch.
        public void Sway(float strength,Vector3 worldDir)
        {
            float amp=reducedMotion?strength*.15f:strength;if(amp<.001f)return;
            impulse=Mathf.Max(impulse,amp*.45f);
            shakeDir=new Vector3(Random.Range(-.6f,.6f),Random.Range(.25f,1f),0).normalized;
            Vector3 lateral=worldDir;lateral.y=0;if(lateral.sqrMagnitude<.01f)lateral=transform.forward;
            swayOffset+=Vector3.ClampMagnitude(-lateral.normalized*amp*.7f+Vector3.up*amp*.16f,.5f);
        }
        public void Frame(Transform target,float duration){cinematicTarget=target;cinematicUntil=Time.unscaledTime+duration;cinematicInfluence=1;cinematicZoom=-1.45f;}
        // A warned circle needs a higher eyeline so the player can read its radius; orbit otherwise stays untouched.
        public void LiftForVolley(bool raised){volleyLiftTarget=raised?(player&&player.config&&player.config.arrowVolley!=null?player.config.arrowVolley.cameraLift:2.6f):0;}
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
                float influence=cinematicWeight*cinematicInfluence;
                focus=Vector3.Lerp(focus,cinematicTarget.position,.45f*influence);
                zoom=Mathf.Lerp(zoom,distance+cinematicZoom,influence);
            }
            Quaternion angle=Quaternion.Euler(pitch,yaw,0);Vector3 desired=focus-angle*Vector3.forward*zoom+angle*Vector3.right*.5f;
            volleyLift=Mathf.SmoothDamp(volleyLift,volleyLiftTarget,ref volleyLiftVelocity,.45f,12f,dt);
            desired+=Vector3.up*volleyLift;
            var ray=desired-focus; if(Physics.SphereCast(focus,.24f,ray.normalized,out var hit,ray.magnitude,1<<8,QueryTriggerInteraction.Ignore))desired=focus+ray.normalized*Mathf.Max(.65f,hit.distance-.12f);
            impulse*=Mathf.Exp(-dt*6.4f);if(impulse<.002f)impulse=0;
            Vector3 rumble=new Vector3(Mathf.Sin(Time.unscaledTime*48f),Mathf.Cos(Time.unscaledTime*41f),0)*impulse*.16f;
            Vector3 shake=shakeDir*impulse*.48f+rumble;
            swayOffset=Vector3.SmoothDamp(swayOffset,Vector3.zero,ref swayVelocity,.16f,14f,dt);
            strikeOffset=Mathf.MoveTowards(strikeOffset,striking?.22f:0,dt*(striking?2.7f:1.8f));
            desired+=angle*Vector3.right*(step*.012f*walkWeight+strikeOffset);
            // Easing the follow constant keeps the dash entry and exit from stepping the smoothing.
            followRate=Mathf.MoveTowards(followRate,dashing?11f:16f,dt*26f);
            if(!poseReady){smoothPosition=desired;poseReady=true;}smoothPosition=Vector3.Lerp(smoothPosition,desired,1-Mathf.Exp(-followRate*dt));
            transform.position=smoothPosition+shake+swayOffset;
            transform.rotation=Quaternion.LookRotation(focus-smoothPosition)*Quaternion.Euler(-impulse*8.2f+Mathf.Sin(Time.unscaledTime*29f)*impulse*3.2f,Mathf.Sin(Time.unscaledTime*17f)*impulse*2.4f,shakeDir.x*impulse*9.5f);
            if(virtualCamera){virtualCamera.transform.SetPositionAndRotation(transform.position,transform.rotation);}
        }
    }
}
