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
        TrailRenderer inkTrail,edgeTrail,flareTrail;
        SumiGuardIK guardIK;
        Material strokeInk;
        static readonly int Speed=Animator.StringToHash("Speed"),AttackRate=Animator.StringToHash("AttackRate");
        public void Init(SumiPlayer source)
        {
            player=source;
            visual=Instantiate(Resources.Load<GameObject>("Ronin/Humanoid"),transform,false).transform;
            animator=visual.GetComponent<Animator>();animator.applyRootMotion=false;
            guardIK=visual.gameObject.AddComponent<SumiGuardIK>();guardIK.player=player;
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
            var flare=new GameObject("Gold finishing flare");flare.transform.SetParent(tip,false);
            flareTrail=flare.AddComponent<TrailRenderer>();flareTrail.sharedMaterial=SumiArt.Gilt;flareTrail.time=.14f;flareTrail.startWidth=.075f;flareTrail.endWidth=0;flareTrail.minVertexDistance=.01f;flareTrail.emitting=false;
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
            // Returning is the descending clip played backwards, so the hips rise with the blade
            // instead of repeating the opening chop. Offset lands on the clip's last frame.
            float offset=move.ReverseClip?attackClipLength*.98f:0;
            animator.CrossFadeInFixedTime(move.StateHash,move.blend,0,offset);
        }
        public void ReleaseSword(Vector3 impulse)
        {
            if(!sword)return;
            if(inkTrail)inkTrail.emitting=false;if(edgeTrail)edgeTrail.emitting=false;if(flareTrail)flareTrail.emitting=false;
            SumiDeathFx.DropWeapon(sword,impulse);
            sword=null;
        }
        void LateUpdate()
        {
            if(!player||!player.combat||!tip)return;
            var combat=player.combat;
            if(!sword||combat.state==SumiCombatState.Dead)
            {
                if(inkTrail)inkTrail.emitting=false;if(edgeTrail)edgeTrail.emitting=false;if(flareTrail)flareTrail.emitting=false;
                return;
            }
            bool posed=combat.IsAttacking||combat.IsGuarding;
            if(posed)
            {
                combat.GetSwordPose(out var grip,out var line);
                if(guardIK&&guardIK.HasResolvedGrip){grip=guardIK.ResolvedGrip;line=guardIK.ResolvedLine;}
                // Position the whole katana, including guard and handle, on the hand target.
                if(sword)
                {
                    sword.localPosition=swordHome;sword.localRotation=swordRotation;sword.localScale=swordScale;
                    Vector3 animatedPosition=sword.position;Quaternion animatedRotation=sword.rotation;
                    float scale=1.05f/Mathf.Max(.01f,bladeHeight*sword.lossyScale.y);
                    sword.localScale*=scale;
                    sword.rotation=Quaternion.FromToRotation(Vector3.up,line);
                    // The katana root is authored directly on the hand bone. Reusing the exact IK
                    // target keeps the wrapped grip inside the palm through extreme cuts.
                    Vector3 posedPosition=grip;
                    float weight=combat.SwordPoseWeight;
                    sword.position=Vector3.Lerp(animatedPosition,posedPosition,weight);
                    sword.rotation=Quaternion.Slerp(animatedRotation,sword.rotation,weight);
                    sword.localScale=Vector3.Lerp(swordScale,sword.localScale,weight);
                }
                tip.position=grip+line*1.13f;
            }
            else if(sword){sword.localPosition=swordHome;sword.localRotation=swordRotation;sword.localScale=swordScale;}
            bool active=combat.BladeActive;
            bool flourish=active&&combat.CurrentAttack&&(combat.CurrentAttack.finisher||combat.state==SumiCombatState.DashStrike);
            inkTrail.time=combat.state==SumiCombatState.DashStrike?.19f:.12f;inkTrail.startWidth=combat.state==SumiCombatState.DashStrike?.20f:.13f;
            if(active&&!inkTrail.emitting){inkTrail.Clear();edgeTrail.Clear();flareTrail.Clear();}
            inkTrail.emitting=active;edgeTrail.emitting=active;
            flareTrail.emitting=flourish;
        }
        void OnDestroy(){if(strokeInk)Destroy(strokeInk);}
    }

    // Licensed clip supplies hips and footwork. Hands follow the authored blade. Spine follows
    // through humanoid Look At, which keeps the avatar upright; writing Transform rotations into
    // SetBoneLocalRotation had put the hips into T-pose space and laid the body on the ground.
    public sealed class SumiGuardIK:MonoBehaviour
    {
        public SumiPlayer player;
        public Vector3 ResolvedGrip { get; private set; }
        public Vector3 ResolvedLine { get; private set; }
        public bool HasResolvedGrip { get; private set; }
        Animator animator;Transform leftShoulder,rightShoulder;
        float weight,offHandRelease,hiltGrip=GripFar,hiltSlide,comfort=.47f,reach=.56f;
        Vector3 lastGrip,lastLine;bool poseReady;
        const float GripFar=.24f,GripNear=.11f;
        void Awake(){animator=GetComponent<Animator>();}
        void Start()
        {
            leftShoulder=animator?animator.GetBoneTransform(HumanBodyBones.LeftUpperArm):null;
            rightShoulder=animator?animator.GetBoneTransform(HumanBodyBones.RightUpperArm):null;
            var elbow=animator?animator.GetBoneTransform(HumanBodyBones.LeftLowerArm):null;
            var wrist=animator?animator.GetBoneTransform(HumanBodyBones.LeftHand):null;
            if(!leftShoulder||!elbow||!wrist)return;
            float arm=Vector3.Distance(leftShoulder.position,elbow.position)+Vector3.Distance(elbow.position,wrist.position);
            comfort=arm*.80f;reach=arm*.96f;
        }
        void OnAnimatorIK(int layer)
        {
            if(!animator||!player||!player.combat)return;
            var combat=player.combat;bool throwing=combat.IsThrowing;bool posed=combat.IsAttacking||combat.IsGuarding||throwing;
            float target=throwing?combat.ThrowPoseWeight:combat.SwordPoseWeight;
            weight=Mathf.MoveTowards(weight,target,Time.deltaTime*(posed?22:14));
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
            if(throwing)
            {
                combat.GetThrowHandPose(out var hand,out var facing);Quaternion handRotation=Quaternion.LookRotation(facing,Vector3.up)*Quaternion.Euler(85,0,90);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand,0);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,0);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight);
                animator.SetIKPosition(AvatarIKGoal.LeftHand,hand);animator.SetIKRotation(AvatarIKGoal.LeftHand,handRotation*Quaternion.Euler(0,180,0));
                animator.SetIKHintPosition(AvatarIKHint.LeftElbow,hand-transform.forward*.34f-transform.right*.24f-transform.up*.14f);animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,weight*.75f);animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);
                animator.SetLookAtPosition(hand+facing*.9f);animator.SetLookAtWeight(weight*.48f,.16f,.72f,.25f,.5f);poseReady=false;offHandRelease=0;return;
            }
            if(posed){combat.GetSwordPose(out lastGrip,out lastLine);poseReady=true;}
            if(weight<=.001f||!poseReady)
            {
                HasResolvedGrip=false;
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,0);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,0);
                animator.SetLookAtWeight(0);animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,0);
                offHandRelease=0;hiltGrip=GripFar;hiltSlide=0;return;
            }
            lastGrip=Settle(lastGrip,rightShoulder,out _);
            ResolvedGrip=lastGrip;ResolvedLine=lastLine;HasResolvedGrip=true;
            Vector3 leftGrip=OffHand(lastGrip,lastLine,out float strain);
            offHandRelease=Mathf.MoveTowards(offHandRelease,strain,Time.deltaTime*7f);
            float offHand=weight*(1-offHandRelease);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,offHand);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,offHand*.86f);
            Quaternion rotation=Quaternion.FromToRotation(Vector3.up,lastLine);
            animator.SetIKPosition(AvatarIKGoal.RightHand,lastGrip);animator.SetIKRotation(AvatarIKGoal.RightHand,rotation);
            if(offHand>.001f){animator.SetIKPosition(AvatarIKGoal.LeftHand,leftGrip);animator.SetIKRotation(AvatarIKGoal.LeftHand,rotation);}
            var root=player.transform;
            animator.SetIKHintPosition(AvatarIKHint.RightElbow,lastGrip-root.forward*.28f+root.right*.22f-root.up*.12f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow,leftGrip-root.forward*.22f-root.right*.22f-root.up*.12f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,weight*.6f);animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,offHand*.5f);
            // Look At is clamped to chest height so a low finish never bows the avatar onto the ground.
            Vector3 look=lastGrip+lastLine*.45f;
            look.y=Mathf.Clamp(look.y,root.position.y+1.18f,root.position.y+1.82f);
            float body=combat.CurrentAttack==null?.12f:combat.CurrentAttack.arc==SumiSwordArc.Overhead?.34f:combat.CurrentAttack.arc==SumiSwordArc.Sweep?.28f:.2f;
            animator.SetLookAtPosition(look);animator.SetLookAtWeight(weight*.5f,body*weight,.62f,.2f,.4f);
        }
        Vector3 OffHand(Vector3 swordHand,Vector3 bladeLine,out float strain)
        {
            if(!leftShoulder){strain=0;return swordHand-bladeLine*GripFar;}
            Vector3 fromShoulder=swordHand-leftShoulder.position;
            float along=Vector3.Dot(fromShoulder,bladeLine);
            float slack=comfort*comfort-fromShoulder.sqrMagnitude+along*along;
            float span=Mathf.Clamp(slack>0?along+Mathf.Sqrt(slack):along,GripNear,GripFar);
            hiltGrip=Mathf.SmoothDamp(hiltGrip,span,ref hiltSlide,.05f,Mathf.Infinity,Time.deltaTime);
            return Settle(swordHand-bladeLine*hiltGrip,leftShoulder,out strain);
        }
        Vector3 Settle(Vector3 goal,Transform shoulder,out float strain)
        {
            strain=0;if(!shoulder)return goal;
            Vector3 offset=goal-shoulder.position;float span=offset.magnitude;
            if(span<.0001f)return goal;
            strain=Mathf.Clamp01(Mathf.InverseLerp(comfort,reach,span));
            return span>reach?shoulder.position+offset*(reach/span):goal;
        }
    }
}
