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
        public float impulse;float cinematicUntil,cinematicWeight,cinematicInfluence=1,cinematicZoom,walkPhase,walkWeight,strikeOffset;Vector3 smoothPosition,impulseDir;bool poseReady;
        // Lock-on steering. The mouse always outranks it; the automatic bearing only fills the gap
        // the player leaves, and it moves under a rate limit so acquiring never snaps the view.
        float lockWeight,manualUntil,manualAuthority=1,lockBearing,yawVelocity,pitchVelocity,followRate=16f,volleyLift,volleyLiftTarget,volleyLiftVelocity;
        Vector3 focusLead,focusLeadVelocity,shakeDir=Vector3.up,swayOffset,swayVelocity;
        bool titleHold;
        const float LockEngage=.40f,LockRelease=.22f,ManualHold=.16f,LockRange=18f,MinLockSpan=1.15f;
        const float YawSmooth=.20f,MaxYawRate=420f,YawDeadzone=1.1f,PitchSmooth=.34f,MaxPitchRate=75f;
        // South torii sits at (0,0,-21.8); dusk sun is behind that gate. Park here on the title screen.
        static readonly Vector3 TitleCamPos=new Vector3(0f,2.2f,-8f);
        static readonly Vector3 TitleLookAt=new Vector3(0f,3.4f,-21.8f);
        public void HoldTitle(bool hold)
        {
            titleHold=hold;poseReady=false;
            if(hold)
            {
                transform.position=TitleCamPos;
                transform.rotation=Quaternion.LookRotation(TitleLookAt-TitleCamPos);
                smoothPosition=TitleCamPos;yaw=0;pitch=11;
            }
        }
        public void Kick(float strength){Kick(strength,Vector3.zero);}
        public void Kick(float strength,Vector3 worldDir)
        {
            float amp=reducedMotion?strength*.15f:strength;if(amp<.001f)return;
            impulse=Mathf.Min(.9f,Mathf.Max(impulse,amp)+amp*.16f);
            shakeDir=new Vector3(Random.Range(-.6f,.6f),Random.Range(.25f,1f),0).normalized;
            if(worldDir.sqrMagnitude>.0001f)impulseDir=worldDir.normalized;
        }
        // Dashes lag the orbit opposite the lunge, then settle without reading as a weapon impact.
        public void Sway(float strength,Vector3 worldDir)
        {
            float amp=reducedMotion?strength*.15f:strength;if(amp<.001f)return;
            impulse=Mathf.Min(.9f,Mathf.Max(impulse,amp*.62f)+amp*.12f);if(worldDir.sqrMagnitude>.0001f)impulseDir=worldDir.normalized;
            shakeDir=new Vector3(Random.Range(-.6f,.6f),Random.Range(.25f,1f),0).normalized;
            Vector3 direction=worldDir;direction.y=0;if(direction.sqrMagnitude<.01f)direction=transform.forward;
            swayOffset+=Vector3.ClampMagnitude(-direction.normalized*amp*.95f+Vector3.up*amp*.22f,.62f);
        }
        public void Frame(Transform target,float duration){cinematicTarget=target;cinematicUntil=Time.unscaledTime+duration;cinematicInfluence=1;cinematicZoom=-.65f;}
        // Volley framing only biases composition. Mouse orbit remains live and the bias eases out
        // before precise dodging matters.
        public void FrameVolley(Transform target,float duration,float strength){cinematicTarget=target;cinematicUntil=Time.unscaledTime+duration;cinematicInfluence=Mathf.Clamp01(strength);cinematicZoom=.35f;}
        // The warning circle needs a higher eyeline; orbit input remains fully available.
        public void LiftForVolley(bool raised){volleyLiftTarget=raised?(player&&player.config&&player.config.arrowVolley!=null?player.config.arrowVolley.cameraLift:2.6f):0;}
        // Pressing lock is an explicit request for the camera to take the bearing, so the hold that
        // normally protects mouse aiming is dropped; the rate limit still keeps the swing smooth.
        public void EngageLock(){manualUntil=0;}
        void LateUpdate()
        {
            if(titleHold)
            {
                transform.position=TitleCamPos;
                transform.rotation=Quaternion.LookRotation(TitleLookAt-TitleCamPos);
                if(virtualCamera)virtualCamera.transform.SetPositionAndRotation(transform.position,transform.rotation);
                return;
            }
            if(!player)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            var combatState=player.combat?player.combat.state:SumiCombatState.Free;
            bool guarding=combatState==SumiCombatState.GuardStartup||combatState==SumiCombatState.GuardHeld;
            bool dying=combatState==SumiCombatState.Dead;

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
                if(dying)
                {
                    pitch=Mathf.MoveTowards(pitch,27f,dt*34f);
                    focus=Vector3.Lerp(focus,cinematicTarget.position+Vector3.up*.20f,.9f*influence);
                }
                else focus=Vector3.Lerp(focus,cinematicTarget.position,.45f*influence);
                zoom=Mathf.Lerp(zoom,distance+cinematicZoom,influence);
            }
            Quaternion angle=Quaternion.Euler(pitch,yaw,0);Vector3 desired=focus-angle*Vector3.forward*zoom+angle*Vector3.right*.5f;
            volleyLift=Mathf.SmoothDamp(volleyLift,volleyLiftTarget,ref volleyLiftVelocity,.45f,12f,dt);
            desired+=Vector3.up*volleyLift;
            var ray=desired-focus; if(Physics.SphereCast(focus,.24f,ray.normalized,out var hit,ray.magnitude,1<<8,QueryTriggerInteraction.Ignore))desired=focus+ray.normalized*Mathf.Max(.65f,hit.distance-.12f);
            impulse*=Mathf.Exp(-dt*5.15f);if(impulse<.002f)impulse=0;
            Vector3 rumble=new Vector3(Mathf.Sin(Time.unscaledTime*52f),Mathf.Cos(Time.unscaledTime*43f),0)*impulse*.24f;
            Vector3 shake=shakeDir*impulse*.68f+rumble;
            Vector3 directed=Vector3.zero;
            if(impulse>.001f&&impulseDir.sqrMagnitude>.001f)
            {
                Vector3 camRight=angle*Vector3.right,camUp=angle*Vector3.up;
                directed=(camRight*Vector3.Dot(impulseDir,camRight)+camUp*Vector3.Dot(impulseDir,camUp))*impulse*.44f;
            }
            swayOffset=Vector3.SmoothDamp(swayOffset,Vector3.zero,ref swayVelocity,.16f,14f,dt);
            strikeOffset=Mathf.MoveTowards(strikeOffset,dying?0:striking?.22f:0,dt*(dying?4f:striking?2.7f:1.8f));
            desired+=angle*Vector3.right*(step*.012f*walkWeight+strikeOffset);
            // Easing the follow constant keeps the dash entry and exit from stepping the smoothing.
            followRate=Mathf.MoveTowards(followRate,dying?7.5f:dashing?11f:16f,dt*26f);
            if(!poseReady){smoothPosition=desired;poseReady=true;}smoothPosition=Vector3.Lerp(smoothPosition,desired,1-Mathf.Exp(-followRate*dt));transform.position=smoothPosition+shake+directed+swayOffset;
            transform.rotation=Quaternion.LookRotation(focus-smoothPosition)*Quaternion.Euler(-impulse*10.5f+Mathf.Sin(Time.unscaledTime*31f)*impulse*4.2f,Mathf.Sin(Time.unscaledTime*19f)*impulse*3.2f,shakeDir.x*impulse*12f);
            if(virtualCamera){virtualCamera.transform.SetPositionAndRotation(transform.position,transform.rotation);}
        }
    }
}
