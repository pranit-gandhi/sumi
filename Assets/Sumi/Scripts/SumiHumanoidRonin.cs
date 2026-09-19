using UnityEngine;

namespace Sumi
{
    // One source of truth for where the katana sits during an action. The hand IK goals and the
    // painted silhouette read the same curve, so the drawn blade always agrees with the grip, and
    // every step of the chain opens on the pose the previous step closed with.
    public static class SumiBladePose
    {
        // Grips are offsets from the shoulder line, in the actor's own frame: x right, y up,
        // z forward. Anchoring to the shoulders rather than the feet is what keeps the hands on the
        // body — the rig's animated stance rides about 0.29 lower than its T-pose, so heights
        // measured from the ground stranded every grip well outside the arms.
        struct Key { public Vector3 grip,dir; }
        static Key K(float rx,float uy,float fz,float dx,float dy,float dz)
            =>new Key{grip=new Vector3(rx,uy,fz),dir=new Vector3(dx,dy,dz).normalized};

        // Every grip below sits inside the arms' envelope — the Ronin's shoulders are 0.25 apart
        // with a 0.58m arm — and the long blade does the travelling instead. Reaching the tip out by
        // moving the hands is what folded the elbows through the chest and knotted the forearms.
        // A raised diagonal guard: still a readable silhouette, held in front of the sternum.
        static readonly Key Guarded  =K(-.02f,-.01f, .34f, -.58f, .54f, .61f);
        // 袈裟 — raised near vertical above the brow, so the off hand stacks under the sword hand
        // instead of being swung out to the side of it.
        static readonly Key OneOpen  =K( .10f, .09f, .18f, -.22f, .95f, .22f);
        // …finishing in front of the left hip with the tip carried down and out to the left.
        static readonly Key OneShut  =K(-.01f,-.26f, .34f, -.44f,-.76f, .48f);
        // 逆袈裟 — rises straight out of that finish to a high right line, hands near the shoulder.
        static readonly Key TwoShut  =K( .08f,-.01f, .20f,  .30f, .90f,-.31f);
        // 回旋 — the hands stay in front of the chest while the tip alone wheels behind the left.
        static readonly Key ThreeMid =K(-.14f,-.14f, .21f, -.74f, .32f,-.59f);
        static readonly Key ThreeShut=K( .06f,-.26f, .39f, -.05f,-.83f, .56f);
        // 筆閃 — a level draw across the waist.
        static readonly Key FlashOpen=K(-.09f,-.24f, .28f, -.93f, .10f, .35f);
        static readonly Key FlashShut=K( .14f,-.30f, .41f,  .93f,-.10f, .35f);

        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static void Blend(Key from,Key to,float cut,out Vector3 grip,out Vector3 dir)
        {grip=Vector3.Lerp(from.grip,to.grip,cut);dir=Vector3.Slerp(from.dir,to.dir,cut);}

        public static bool Evaluate(SumiCombatState state,float t,out Vector3 grip,out Vector3 dir)
        {
            switch(state)
            {
                case SumiCombatState.Attack1: Blend(OneOpen,OneShut,Ease(.13f,.40f,t),out grip,out dir);return true;
                case SumiCombatState.Attack2: Blend(OneShut,TwoShut,Ease(.09f,.32f,t),out grip,out dir);return true;
                case SumiCombatState.Attack3:
                    // The finisher needs two arcs: the flat sweep behind, then the chop through.
                    if(t<.26f)Blend(TwoShut,ThreeMid,Ease(.06f,.26f,t),out grip,out dir);
                    else Blend(ThreeMid,ThreeShut,Ease(.26f,.50f,t),out grip,out dir);
                    return true;
                case SumiCombatState.DashStrike: Blend(FlashOpen,FlashShut,Ease(.14f,.41f,t),out grip,out dir);return true;
                case SumiCombatState.GuardStartup:
                case SumiCombatState.GuardHeld: grip=Guarded.grip;dir=Guarded.dir;return true;
            }
            grip=Guarded.grip;dir=Guarded.dir;return false;
        }

        // How strongly the authored line should override the animated arm, per action.
        public static float Weight(SumiCombatState state,float t)
        {
            switch(state)
            {
                case SumiCombatState.Attack1: return Ease(.02f,.11f,t)*Ease(.62f,.50f,t);
                case SumiCombatState.Attack2: return Ease(.02f,.09f,t)*Ease(.54f,.43f,t);
                case SumiCombatState.Attack3: return Ease(.02f,.11f,t)*Ease(.72f,.58f,t);
                case SumiCombatState.DashStrike: return Ease(.01f,.08f,t)*Ease(.53f,.43f,t);
                case SumiCombatState.GuardStartup:
                case SumiCombatState.GuardHeld: return 1;
            }
            return 0;
        }

        // The trailing ink fan drawn behind the edge, one shape per action. centre is an offset from
        // the same shoulder anchor the grips use, along forward/up; the sweep angles are measured in
        // the right/up plane and follow the blade tip, so each fan matches the arc the grip keys
        // above actually describe.
        public static bool Arc(SumiCombatState state,float t,out float phase,out Vector2 centre,out Vector2 radius,out Vector2 sweep)
        {
            switch(state)
            {
                case SumiCombatState.Attack1: phase=Mathf.InverseLerp(.15f,.42f,t);centre=new Vector2(.55f,-.10f);radius=new Vector2(1.21f,1.21f);sweep=new Vector2(97,244);return t>=.15f&&t<=.46f;
                case SumiCombatState.Attack2: phase=Mathf.InverseLerp(.10f,.34f,t);centre=new Vector2(.43f,-.15f);radius=new Vector2(1.17f,1.17f);sweep=new Vector2(242,430);return t>=.10f&&t<=.38f;
                case SumiCombatState.Attack3: phase=Mathf.InverseLerp(.26f,.52f,t);centre=new Vector2(.32f,-.35f);radius=new Vector2(.99f,.99f);sweep=new Vector2(150,270);return t>=.26f&&t<=.58f;
                case SumiCombatState.DashStrike: phase=Mathf.InverseLerp(.14f,.41f,t);centre=new Vector2(.61f,-.30f);radius=new Vector2(1.18f,.30f);sweep=new Vector2(172,-6);return t>=.14f&&t<=.47f;
            }
            phase=0;centre=Vector2.zero;radius=Vector2.zero;sweep=Vector2.zero;return false;
        }
    }

    [DefaultExecutionOrder(30)]
    public class SumiHumanoidRonin:MonoBehaviour
    {
        public Animator animator;
        public Transform visual;
        SumiPlayer player;
        bool dodging;
        LineRenderer paintedBlade,brushArc;
        Material strokeInk;
        // Carried from the outgoing action so a cancel never teleports the katana. Stored in the
        // actor's own frame so it keeps following the body while the stitch plays out.
        Vector3 carryGrip,carryDir;float carryAt=-9;
        Transform leftShoulder,rightShoulder;
        const float Stitch=.13f;
        static readonly int Speed=Animator.StringToHash("Speed");
        public void Init(SumiPlayer source)
        {
            player=source;
            var prefab=Resources.Load<GameObject>("Ronin/Humanoid");
            visual=Instantiate(prefab,transform,false).transform;
            animator=visual.GetComponent<Animator>();
            var guardIK=visual.gameObject.AddComponent<SumiGuardIK>();guardIK.player=player;guardIK.ronin=this;
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
            if(player.combat!=null)
            {
                if(player.combat.state==SumiCombatState.Free)
                {
                    if(animator.speed!=1)animator.speed=1;
                    if(dodging){animator.CrossFadeInFixedTime("Locomotion",.14f);dodging=false;}
                }
                return;
            }
            bool active=player.dodgeRemaining>0;
            if(active!=dodging){animator.CrossFadeInFixedTime(active?"Dodge":"Locomotion",active?.06f:.14f);dodging=active;}
        }
        public void PlayCombat(string state,float fade,float speed=1f){if(animator){animator.speed=speed;animator.CrossFadeInFixedTime(state,fade);}}

        // Called by the combat owner just before it swaps state, while the old action time is still
        // valid, so the incoming step can grow out of the exact pose the player last saw.
        public void CarryBlade(SumiCombatState from,float t)
        {
            if(!SumiBladePose.Evaluate(from,t,out var grip,out var dir))return;
            carryGrip=grip;carryDir=dir;carryAt=Time.time;
        }

        // World-space grip, blade direction and authority for the current action. The pose is always
        // usable so the IK goal keeps a valid target while its weight decays; the return value only
        // says whether this action is one the painted silhouette should draw.
        public bool BladePose(out Vector3 grip,out Vector3 dir,out float weight)
        {
            grip=ShoulderAnchor();dir=transform.up;weight=0;
            if(player==null||player.combat==null)return false;
            var state=player.combat.state;float t=player.combat.StateTime;
            bool posed=SumiBladePose.Evaluate(state,t,out var local,out var localDir);
            float stitch=Mathf.Clamp01((Time.time-carryAt)/Stitch);
            if(stitch<1){local=Vector3.Lerp(carryGrip,local,stitch);localDir=Vector3.Slerp(carryDir,localDir,stitch);}
            var root=player.transform;
            grip=ShoulderAnchor()+root.right*local.x+root.up*local.y+root.forward*local.z;
            dir=(root.right*localDir.x+root.up*localDir.y+root.forward*localDir.z).normalized;
            weight=SumiBladePose.Weight(state,t);
            return posed;
        }

        // Where the grip keys hang from. Following the animated shoulders means the katana stays in
        // the hands through a crouch, a lean or a rescaled rig, instead of the pose table having to
        // guess a standing height that only ever matches one stance.
        Vector3 ShoulderAnchor()
        {
            if(animator)
            {
                if(!leftShoulder)leftShoulder=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                if(!rightShoulder)rightShoulder=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if(leftShoulder&&rightShoulder)return (leftShoulder.position+rightShoulder.position)*.5f;
            }
            return transform.position+transform.up*1.43f;
        }

        void LateUpdate()
        {
            if(!player||!player.combat||!paintedBlade)return;
            var action=player.combat.state;float t=player.combat.StateTime;
            paintedBlade.enabled=BladePose(out var grip,out var dir,out _);
            if(paintedBlade.enabled)
            {
                paintedBlade.positionCount=3;paintedBlade.SetPosition(0,grip);paintedBlade.SetPosition(1,grip+dir*.64f);paintedBlade.SetPosition(2,grip+dir*1.13f);
            }
            bool active=SumiBladePose.Arc(action,t,out float phase,out Vector2 centreOffset,out Vector2 radius,out Vector2 sweep);
            brushArc.enabled=active;
            if(active)
            {
                int count=Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(phase)*22),2,22);brushArc.positionCount=count;
                Vector3 centre=ShoulderAnchor()+transform.forward*centreOffset.x+transform.up*centreOffset.y;
                for(int i=0;i<count;i++)
                {
                    float u=i/21f,angle=Mathf.Lerp(sweep.x,sweep.y,u)*Mathf.Deg2Rad;
                    Vector3 p=centre+transform.right*(Mathf.Cos(angle)*radius.x)+transform.up*(Mathf.Sin(angle)*radius.y);
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
        public SumiHumanoidRonin ronin;
        Animator animator;Transform leftShoulder,rightShoulder;
        float weight,offHandRelease,hiltGrip=GripFar,hiltSlide,comfort=.47f,reach=.56f;
        // The off hand rides the tsuka somewhere between these two distances below the sword hand.
        // Being allowed to choke up the hilt is what lets it stay on the sword through a wide sweep
        // instead of either letting go or being dragged across the chest.
        const float GripFar=.24f,GripNear=.11f;
        void Awake(){animator=GetComponent<Animator>();}
        void Start()
        {
            leftShoulder=animator?animator.GetBoneTransform(HumanBodyBones.LeftUpperArm):null;
            rightShoulder=animator?animator.GetBoneTransform(HumanBodyBones.RightUpperArm):null;
            // Measure this rig rather than assuming one, so the envelope survives a rescaled model.
            var elbow=animator?animator.GetBoneTransform(HumanBodyBones.LeftLowerArm):null;
            var wrist=animator?animator.GetBoneTransform(HumanBodyBones.LeftHand):null;
            if(!leftShoulder||!elbow||!wrist)return;
            float arm=Vector3.Distance(leftShoulder.position,elbow.position)+Vector3.Distance(elbow.position,wrist.position);
            comfort=arm*.80f;reach=arm*.96f;
        }
        void OnAnimatorIK(int layer)
        {
            if(!animator||!player||!ronin||player.combat==null)return;
            ronin.BladePose(out var rightGrip,out var bladeLine,out float target);
            weight=Mathf.MoveTowards(weight,target,Time.deltaTime*(target>0?17f:10f));
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
            if(weight<=.001f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,0);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,0);
                offHandRelease=0;hiltGrip=GripFar;hiltSlide=0;return;
            }
            // Never ask a shoulder for more than it has. Without this the solver answers an
            // unreachable goal by folding the elbow through the torso.
            rightGrip=Settle(rightGrip,rightShoulder,out _);
            Vector3 leftGrip=OffHand(rightGrip,bladeLine,out float strain);
            // Only once even the highest grip on the hilt is out of range does a real cut let the off
            // hand go rather than hauling it across the chest; easing the weight out from there is
            // what keeps the forearms from knotting.
            offHandRelease=Mathf.MoveTowards(offHandRelease,strain,Time.deltaTime*7f);
            float offHand=weight*(1-offHandRelease);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,offHand);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,offHand*.86f);

            Quaternion handRotation=Quaternion.FromToRotation(Vector3.up,bladeLine);
            animator.SetIKPosition(AvatarIKGoal.RightHand,rightGrip);animator.SetIKRotation(AvatarIKGoal.RightHand,handRotation);
            if(offHand<=.001f)return;
            animator.SetIKPosition(AvatarIKGoal.LeftHand,leftGrip);animator.SetIKRotation(AvatarIKGoal.LeftHand,handRotation);
        }
        // Picks where on the hilt the off hand holds: the lowest grip, nearest the pommel where a two
        // handed cut wants it, that the shoulder can still reach comfortably. Solving the hilt
        // segment against the comfort sphere means the hand sits at the pommel while the sword is
        // close and slides up the tsuka only as far as a sweep actually demands.
        Vector3 OffHand(Vector3 swordHand,Vector3 bladeLine,out float strain)
        {
            if(!leftShoulder){strain=0;return swordHand-bladeLine*GripFar;}
            Vector3 fromShoulder=swordHand-leftShoulder.position;
            float along=Vector3.Dot(fromShoulder,bladeLine);
            float slack=comfort*comfort-fromShoulder.sqrMagnitude+along*along;
            float span=Mathf.Clamp(slack>0?along+Mathf.Sqrt(slack):along,GripNear,GripFar);
            // Damped, because the solved grip can jump a couple of centimetres between frames at the
            // fastest part of a sweep and the hand should slide rather than snap.
            hiltGrip=Mathf.SmoothDamp(hiltGrip,span,ref hiltSlide,.05f,Mathf.Infinity,Time.deltaTime);
            return Settle(swordHand-bladeLine*hiltGrip,leftShoulder,out strain);
        }

        // Pulls a goal back inside one arm's envelope, reporting 0 while it sits comfortably and
        // rising to 1 as it stretches out to the limit.
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
