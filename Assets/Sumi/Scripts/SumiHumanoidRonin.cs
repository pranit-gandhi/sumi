using UnityEngine;

namespace Sumi
{
    [DefaultExecutionOrder(30)]
    public class SumiHumanoidRonin:MonoBehaviour
    {
        public Animator animator;
        public Transform visual;
        SumiPlayer player;
        Transform sword,tip;
        Vector3 swordHome,swordScale;
        Quaternion swordRotation;
        Vector3 bladeCenter;
        float bladeBottom,bladeHeight,attackClipLength;
        TrailRenderer inkTrail,edgeTrail;
        Material strokeInk;
        static readonly int Speed=Animator.StringToHash("Speed"),AttackRate=Animator.StringToHash("AttackRate");
        public void Init(SumiPlayer source)
        {
            player=source;
            visual=Instantiate(Resources.Load<GameObject>("Ronin/Humanoid"),transform,false).transform;
            animator=visual.GetComponent<Animator>();animator.applyRootMotion=false;
            var guardIK=visual.gameObject.AddComponent<SumiGuardIK>();guardIK.player=player;
            player.rig=visual.gameObject.AddComponent<SumiRig>();player.rig.enabled=false;player.rig.animator=animator;
            var clip=Resources.Load<AnimationClip>("Ronin/Sword_Attack");attackClipLength=clip?clip.length:1.2f;
            foreach(var mesh in visual.GetComponentsInChildren<MeshFilter>())
            {
                if(mesh.name!="Curved blade")continue;
                sword=mesh.transform.parent;swordHome=sword.localPosition;swordRotation=sword.localRotation;swordScale=sword.localScale;
                var bounds=mesh.sharedMesh.bounds;bladeCenter=bounds.center;bladeBottom=bounds.min.y;bladeHeight=bounds.size.y;
                break;
            }
            tip=new GameObject("Sword edge trail").transform;tip.SetParent(transform,false);
            strokeInk=new Material(Shader.Find("Universal Render Pipeline/Unlit"));strokeInk.color=new Color(.025f,.023f,.021f);
            inkTrail=tip.gameObject.AddComponent<TrailRenderer>();inkTrail.sharedMaterial=strokeInk;inkTrail.time=.12f;inkTrail.startWidth=.13f;inkTrail.endWidth=.003f;inkTrail.minVertexDistance=.012f;inkTrail.emitting=false;
            var edge=new GameObject("Pale sword trail");edge.transform.SetParent(tip,false);
            edgeTrail=edge.AddComponent<TrailRenderer>();edgeTrail.sharedMaterial=Resources.Load<Material>("Ronin/Pale blade edge");edgeTrail.time=.085f;edgeTrail.startWidth=.032f;edgeTrail.endWidth=.001f;edgeTrail.minVertexDistance=.012f;edgeTrail.emitting=false;
        }
        void Update()
        {
            if(!animator||!player)return;
            animator.SetFloat(Speed,Mathf.Min(player.velocity.magnitude,3.5f),.14f,Time.deltaTime);
        }
        public void PlayCombat(string state,float fade)
        {
            if(!animator)return;
            animator.speed=1;animator.SetFloat(AttackRate,1);
            animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer."+state),fade,0,0);
        }
        public void PlayCombat(SumiAttackDefinition move)
        {
            if(!animator)return;
            animator.speed=1;animator.SetFloat(AttackRate,attackClipLength/Mathf.Max(.1f,move.duration));
            animator.CrossFadeInFixedTime(move.StateHash,move.blend,0,0);
        }
        void LateUpdate()
        {
            if(!player||!player.combat||!tip)return;
            var combat=player.combat;
            bool posed=combat.IsAttacking||combat.IsGuarding;
            if(posed)
            {
                combat.GetSwordPose(out var grip,out var line);
                // Position the whole katana, including guard and handle, on the hand target.
                if(sword)
                {
                    sword.localPosition=swordHome;sword.localRotation=swordRotation;sword.localScale=swordScale;
                    Vector3 animatedPosition=sword.position;Quaternion animatedRotation=sword.rotation;
                    float scale=1.05f/Mathf.Max(.01f,bladeHeight*sword.lossyScale.y);
                    sword.localScale*=scale;
                    sword.rotation=Quaternion.FromToRotation(Vector3.up,line);
                    Vector3 posedPosition=grip+line*.08f-sword.TransformVector(new Vector3(bladeCenter.x,bladeBottom,bladeCenter.z));
                    float weight=combat.SwordPoseWeight;
                    sword.position=Vector3.Lerp(animatedPosition,posedPosition,weight);
                    sword.rotation=Quaternion.Slerp(animatedRotation,sword.rotation,weight);
                    sword.localScale=Vector3.Lerp(swordScale,sword.localScale,weight);
                }
                tip.position=grip+line*1.13f;
            }
            else if(sword){sword.localPosition=swordHome;sword.localRotation=swordRotation;sword.localScale=swordScale;}
            bool active=combat.BladeActive;
            if(active&&!inkTrail.emitting){inkTrail.Clear();edgeTrail.Clear();}
            inkTrail.emitting=active;edgeTrail.emitting=active;
        }
        void OnDestroy(){if(strokeInk)Destroy(strokeInk);}
    }

    // Keep the licensed full-body motion; blend hand targets across legal action changes.
    public sealed class SumiGuardIK:MonoBehaviour
    {
        public SumiPlayer player;
        Animator animator;float weight;Vector3 lastGrip,lastLine;bool poseReady;
        void Awake(){animator=GetComponent<Animator>();}
        void OnAnimatorIK(int layer)
        {
            if(!animator||!player||!player.combat)return;
            var combat=player.combat;bool posed=combat.IsAttacking||combat.IsGuarding;
            float target=combat.SwordPoseWeight;
            weight=Mathf.MoveTowards(weight,target,Time.deltaTime*(posed?22:14));
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight*.9f);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight*.85f);
            if(posed){combat.GetSwordPose(out lastGrip,out lastLine);poseReady=true;}
            if(weight<=.001f||!poseReady)return;
            Quaternion rotation=Quaternion.FromToRotation(Vector3.up,lastLine);
            animator.SetIKPosition(AvatarIKGoal.RightHand,lastGrip);animator.SetIKRotation(AvatarIKGoal.RightHand,rotation);
            animator.SetIKPosition(AvatarIKGoal.LeftHand,lastGrip-lastLine*.15f);animator.SetIKRotation(AvatarIKGoal.LeftHand,rotation);
        }
    }
}
