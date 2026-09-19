using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
namespace Sumi
{
    public class SumiCamera : MonoBehaviour
    {
        public SumiPlayer player;public Transform cinematicTarget;public float yaw,pitch=11,distance=5.65f;public bool reducedMotion;
        public CinemachineCamera virtualCamera;
        public float impulse;float cinematicUntil,walkPhase,walkWeight,strikeOffset;Vector3 smoothPosition;bool poseReady;
        public void Kick(float strength){impulse=Mathf.Max(impulse,reducedMotion?strength*.15f:strength);}
        public void Frame(Transform target,float duration){cinematicTarget=target;cinematicUntil=Time.unscaledTime+duration;}
        void LateUpdate()
        {
            if(!player)return;float dt=Time.unscaledDeltaTime;
            var combatState=player.combat?player.combat.state:SumiCombatState.Free;
            bool guarding=combatState==SumiCombatState.GuardStartup||combatState==SumiCombatState.GuardHeld;
            if(Cursor.lockState==CursorLockMode.Locked&&Mouse.current!=null)
            {
                var delta=Mouse.current.delta.ReadValue();
                yaw+=delta.x*(guarding?.13f:.095f);pitch=Mathf.Clamp(pitch-delta.y*.07f,10,52);
            }
            bool canWalk=combatState==SumiCombatState.Free||combatState==SumiCombatState.GuardStartup||combatState==SumiCombatState.GuardHeld;
            float walking=canWalk?Mathf.InverseLerp(.15f,3.3f,player.velocity.magnitude):0;
            walkWeight=Mathf.MoveTowards(walkWeight,walking,dt*3.2f);
            if(walkWeight>.01f)walkPhase+=dt*Mathf.Lerp(4.2f,7.2f,walking);
            float step=Mathf.Sin(walkPhase),rise=Mathf.Abs(Mathf.Cos(walkPhase));
            Vector3 focus=player.transform.position+Vector3.up*(1.3f+rise*.018f*walkWeight);
            if(player.locked&&player.target)
            {
                var d=player.target.position-player.transform.position;d.y=0;
                if(d.magnitude<15)
                {
                    // Guard footwork needs a free orbit. Lock-on still frames the foe but does not pull the camera back.
                    if(!guarding){float wanted=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;yaw=Mathf.LerpAngle(yaw,wanted,1-Mathf.Exp(-2.7f*dt));}
                    focus+=d*(guarding?.12f:.22f);
                }
            }
            bool striking=combatState==SumiCombatState.Attack1||combatState==SumiCombatState.DashStrike;
            bool dashing=combatState==SumiCombatState.Dash||combatState==SumiCombatState.DashStrike;
            float zoom=distance+(dashing?.23f:0);
            if(player.target&&player.locked)zoom+=Mathf.Clamp((Vector3.Distance(player.transform.position,player.target.position)-2.3f)*.14f,0,.65f);
            if(cinematicTarget&&Time.unscaledTime<cinematicUntil){focus=Vector3.Lerp(focus,cinematicTarget.position+Vector3.up*1.2f,.45f);zoom=4.2f;}
            Quaternion angle=Quaternion.Euler(pitch,yaw,0);Vector3 desired=focus-angle*Vector3.forward*zoom+angle*Vector3.right*.5f;
            var ray=desired-focus; if(Physics.SphereCast(focus,.24f,ray.normalized,out var hit,ray.magnitude,1<<8,QueryTriggerInteraction.Ignore))desired=focus+ray.normalized*Mathf.Max(.65f,hit.distance-.12f);
            impulse=Mathf.MoveTowards(impulse,0,dt*1.6f);var noise=new Vector3(Mathf.Sin(Time.unscaledTime*23),Mathf.Cos(Time.unscaledTime*19),0)*impulse*.18f;
            strikeOffset=Mathf.MoveTowards(strikeOffset,striking?.22f:0,dt*(striking?2.7f:1.8f));
            desired+=angle*Vector3.right*(step*.012f*walkWeight+strikeOffset);
            float follow=dashing?11f:16f;
            if(!poseReady){smoothPosition=desired;poseReady=true;}smoothPosition=Vector3.Lerp(smoothPosition,desired,1-Mathf.Exp(-follow*dt));transform.position=smoothPosition+noise;
            transform.rotation=Quaternion.LookRotation(focus-smoothPosition)*Quaternion.Euler(0,0,Mathf.Sin(Time.unscaledTime*21)*impulse*1.5f);
            if(virtualCamera){virtualCamera.transform.SetPositionAndRotation(transform.position,transform.rotation);}
        }
    }
}
