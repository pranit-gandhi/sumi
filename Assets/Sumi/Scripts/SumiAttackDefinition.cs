using UnityEngine;

namespace Sumi
{
    public enum SumiSwordArc { Descending, Returning, Sweep, Overhead }

    [CreateAssetMenu(menuName="Sumi/Sword attack")]
    public sealed class SumiAttackDefinition : ScriptableObject
    {
        public string animationState="Attack1";
        public SumiSwordArc arc;
        [Min(.1f)] public float duration=.48f;
        [Tooltip("Seconds from attack start; shared by damage, IK and the sword trail.")]
        public Vector2 activeWindow=new Vector2(.105f,.255f);
        public Vector2 linkWindow=new Vector2(.255f,.42f);
        [Min(0)] public float guardCancel=.29f,dodgeCancel=.29f,hitDodgeCancel=.16f;
        [Min(0)] public float damage=21,lunge=.48f;
        [Range(0,1)] public float startupMovement=.65f,activeMovement=.3f,recoveryMovement=.85f;
        [Min(0)] public float turnSpeed=360,hitStop=.04f,cameraKick=.13f;
        [Range(.01f,.2f)] public float blend=.065f;
        public bool finisher;
        public SumiAttackDefinition lightFollowUp,heavyFollowUp;
        public int StateHash=>Animator.StringToHash("Base Layer."+animationState);
        public bool CanLink(float time)=>time>=linkWindow.x&&time<=linkWindow.y;
        public bool CrossesActive(float before,float after)=>after>=activeWindow.x&&before<activeWindow.y;
        public float Movement(float time)=>time<activeWindow.x?startupMovement:time<=activeWindow.y?activeMovement:
            Mathf.Lerp(activeMovement,recoveryMovement,Mathf.InverseLerp(activeWindow.y,duration,time));
        public float Travel(float time)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(activeWindow.x*.35f,activeWindow.y,time));

        public void Validate()
        {
            duration=Mathf.Max(.1f,duration);
            activeWindow.x=Mathf.Clamp(activeWindow.x,.01f,duration-.02f);
            activeWindow.y=Mathf.Clamp(activeWindow.y,activeWindow.x+.01f,duration);
            linkWindow.x=Mathf.Clamp(linkWindow.x,activeWindow.y,duration);
            linkWindow.y=Mathf.Clamp(linkWindow.y,linkWindow.x,duration);
            guardCancel=Mathf.Clamp(guardCancel,activeWindow.y,duration);
            dodgeCancel=Mathf.Clamp(dodgeCancel,activeWindow.y,duration);
            hitDodgeCancel=Mathf.Clamp(hitDodgeCancel,activeWindow.x,duration);
        }
        void OnValidate(){Validate();}

        // The same curve supplies hand IK, the real katana and swept contact samples.
        public void LocalPose(float time,out Vector3 grip,out Vector3 direction)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(activeWindow.x,activeWindow.y,time));
            if(arc==SumiSwordArc.Overhead)
            {
                grip=Vector3.Lerp(new Vector3(.12f,1.82f,.27f),new Vector3(.03f,.88f,.64f),t);
                direction=Vector3.Slerp(new Vector3(.05f,.94f,.34f),new Vector3(.05f,-.58f,.82f),t).normalized;
            }
            else if(arc==SumiSwordArc.Sweep)
            {
                grip=Vector3.Lerp(new Vector3(-.38f,1.14f,.32f),new Vector3(.38f,1.02f,.5f),t);
                float angle=Mathf.Lerp(-80,80,t)*Mathf.Deg2Rad;
                direction=new Vector3(Mathf.Sin(angle),-.1f,Mathf.Cos(angle)).normalized;
            }
            else
            {
                bool reverse=arc==SumiSwordArc.Returning;
                Vector3 raised=new Vector3(.38f,1.58f,.25f),lowered=new Vector3(-.38f,1.02f,.52f);
                grip=Vector3.Lerp(reverse?lowered:raised,reverse?raised:lowered,t);
                // A forward diagonal arc; the return starts where the opening cut ends.
                float u=reverse?1-t:t,angle=Mathf.Lerp(68,-68,u)*Mathf.Deg2Rad;
                direction=new Vector3(Mathf.Sin(angle)*.78f,Mathf.Sin(angle)*.62f,Mathf.Cos(angle)).normalized;
            }
        }
    }

    // One expiring action, never a growing queue of old button presses.
    public enum SumiAttackInput { None,Light,Heavy }
    public struct SumiCombatInputBuffer
    {
        public SumiAttackInput Action { get; private set; }
        float expires;
        public void Press(SumiAttackInput action,float now,float lifetime){Action=action;expires=now+lifetime;}
        public SumiAttackInput Peek(float now){if(now>expires)Clear();return Action;}
        public void Clear(){Action=SumiAttackInput.None;expires=0;}
    }
}
