using UnityEngine;

namespace Sumi
{
    [DefaultExecutionOrder(30)]
    public class SumiHumanoidRonin:MonoBehaviour
    {
        public Animator animator;
        public Transform visual;
        SumiPlayer player;
        bool dodging;
        LineRenderer paintedBlade,brushArc;
        Material strokeInk;
        static readonly int Speed=Animator.StringToHash("Speed");
        public void Init(SumiPlayer source)
        {
            player=source;
            var prefab=Resources.Load<GameObject>("Ronin/Humanoid");
            visual=Instantiate(prefab,transform,false).transform;
            animator=visual.GetComponent<Animator>();
            var guardIK=visual.gameObject.AddComponent<SumiGuardIK>();guardIK.player=player;
            player.rig=visual.gameObject.AddComponent<SumiRig>();player.rig.enabled=false;
            player.rig.animator=animator;
            strokeInk=new Material(Shader.Find("Universal Render Pipeline/Unlit"));strokeInk.color=new Color(.012f,.012f,.014f);
            paintedBlade=Stroke("Painted katana silhouette",.055f);
            brushArc=Stroke("Sweeping ink cut",.19f);
        }
        LineRenderer Stroke(string title,float width)
        {
            var go=new GameObject(title);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=strokeInk;line.useWorldSpace=true;
            line.startWidth=width;line.endWidth=.005f;line.numCapVertices=2;line.enabled=false;
            return line;
        }
        void Update()
        {
            if(!animator||!player)return;
            // The walk-to-jog blend follows actual code-driven velocity.
            animator.SetFloat(Speed,Mathf.Min(player.velocity.magnitude,3.5f),.20f,Time.deltaTime);
            // Combat owns its state transitions; locomotion may only resume when that owner returns to Free.
            if(player.combat!=null){if(player.combat.state==SumiCombatState.Free&&dodging){animator.CrossFadeInFixedTime("Locomotion",.14f);dodging=false;}return;}
            bool active=player.dodgeRemaining>0;
            if(active!=dodging){animator.CrossFadeInFixedTime(active?"Dodge":"Locomotion",active?.06f:.14f);dodging=active;}
        }
        public void PlayCombat(string state,float fade){if(animator){animator.speed=1;animator.CrossFadeInFixedTime(state,fade);}}
        void LateUpdate()
        {
            if(!player||!player.combat||!paintedBlade)return;
            var action=player.combat.state;float t=player.combat.StateTime;
            bool guard=player.combat.IsGuarding,normal=action==SumiCombatState.Attack1,flash=action==SumiCombatState.DashStrike;
            paintedBlade.enabled=guard||normal||flash;
            if(paintedBlade.enabled)
            {
                Vector3 up=transform.up,right=transform.right,forward=transform.forward;
                Vector3 grip,dir;
                if(normal)
                {
                    float cut=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.19f,.48f,t));
                    grip=transform.position+Vector3.Lerp(up*1.52f+forward*.22f+right*.39f,up*.96f+forward*.62f-right*.37f,cut);
                    dir=Vector3.Slerp((-right*.68f+up*.70f+forward*.25f).normalized,(right*.76f-up*.43f+forward*.48f).normalized,cut);
                }
                else if(flash)
                {
                    float cut=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,.41f,t));
                    grip=transform.position+Vector3.Lerp(up*1.22f+forward*.32f-right*.37f,up*1.04f+forward*.65f+right*.38f,cut);
                    dir=Vector3.Slerp((-right*.94f+forward*.34f+up*.12f).normalized,(right*.94f+forward*.34f-up*.12f).normalized,cut);
                }
                else{grip=transform.position+up*1.65f+forward*.18f+right*.17f;dir=(-right*.83f+up*.53f+forward*.17f).normalized;}
                paintedBlade.positionCount=3;paintedBlade.SetPosition(0,grip);paintedBlade.SetPosition(1,grip+dir*.64f);paintedBlade.SetPosition(2,grip+dir*1.13f);
            }
            bool active=normal?t>=.20f&&t<=.56f:flash&&t>=.14f&&t<=.47f;
            brushArc.enabled=active;
            if(active)
            {
                float phase=normal?Mathf.InverseLerp(.20f,.48f,t):Mathf.InverseLerp(.14f,.41f,t);
                int count=Mathf.Clamp(Mathf.RoundToInt(phase*22),2,22);brushArc.positionCount=count;
                Vector3 center=transform.position+transform.forward*(normal?.83f:1.02f)+transform.up*(normal?1.23f:1.08f);
                for(int i=0;i<count;i++)
                {
                    float u=i/21f,angle=normal?Mathf.Lerp(145,-30,u)*Mathf.Deg2Rad:Mathf.Lerp(200,-20,u)*Mathf.Deg2Rad;
                    Vector3 p=center+transform.right*(Mathf.Cos(angle)*(normal?.91f:1.13f))+transform.up*(Mathf.Sin(angle)*(normal?.87f:.34f));
                    brushArc.SetPosition(i,p);
                }
            }
        }
        void OnDestroy(){if(strokeInk)Destroy(strokeInk);}
    }

    // The licensed source clips supply weight, torso motion and footwork. Humanoid IK
    // gives each action its own deliberate blade line without replacing the animated rig.
    public sealed class SumiGuardIK:MonoBehaviour
    {
        public SumiPlayer player;
        Animator animator;float weight;
        void Awake(){animator=GetComponent<Animator>();}
        void OnAnimatorIK(int layer)
        {
            if(!animator||!player||player.combat==null)return;
            var state=player.combat.state;float actionTime=player.combat.StateTime;
            bool guard=player.combat.IsGuarding,shoulder=state==SumiCombatState.Attack1,waist=state==SumiCombatState.DashStrike;
            float target=guard||shoulder||waist?1:0;
            if(shoulder)target*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.12f,actionTime))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,.62f,actionTime));
            if(waist)target*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.01f,.08f,actionTime))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.53f,.43f,actionTime));
            weight=Mathf.MoveTowards(weight,target,Time.deltaTime*(target>0?17f:10f));
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight*.86f);
            if(weight<=.001f)return;
            Vector3 right=player.transform.right,up=player.transform.up,forward=player.transform.forward;
            Vector3 bladeLine,rightGrip;
            if(shoulder)
            {
                float cut=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.19f,.48f,actionTime));
                Vector3 raised=player.transform.position+up*1.52f+forward*.22f+right*.39f;
                Vector3 contact=player.transform.position+up*.96f+forward*.62f-right*.37f;
                rightGrip=Vector3.Lerp(raised,contact,cut);
                Vector3 first=(-right*.68f+up*.70f+forward*.25f).normalized;
                Vector3 last=(right*.76f-up*.43f+forward*.48f).normalized;
                bladeLine=Vector3.Slerp(first,last,cut).normalized;
            }
            else if(waist)
            {
                float cut=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,.41f,actionTime));
                rightGrip=Vector3.Lerp(player.transform.position+up*1.22f+forward*.32f-right*.37f,player.transform.position+up*1.04f+forward*.65f+right*.38f,cut);
                bladeLine=Vector3.Slerp((-right*.94f+forward*.34f+up*.12f).normalized,(right*.94f+forward*.34f-up*.12f).normalized,cut).normalized;
            }
            else
            {
                // A raised, horizontal blade silhouette: the guard remains readable while walking.
                bladeLine=(-right*.83f+up*.53f+forward*.17f).normalized;
                rightGrip=player.transform.position+up*1.65f+forward*.18f+right*.17f;
            }
            Vector3 leftGrip=rightGrip-bladeLine*.16f;
            Quaternion handRotation=Quaternion.FromToRotation(Vector3.up,bladeLine);
            animator.SetIKPosition(AvatarIKGoal.RightHand,rightGrip);animator.SetIKRotation(AvatarIKGoal.RightHand,handRotation);
            animator.SetIKPosition(AvatarIKGoal.LeftHand,leftGrip);animator.SetIKRotation(AvatarIKGoal.LeftHand,handRotation);
        }
    }
}
