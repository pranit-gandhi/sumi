using UnityEngine;

namespace Sumi
{
    // Full-body animation and the actual katana share the enemy attack clock.
    public sealed class SumiEnemyBlade : MonoBehaviour
    {
        SumiEnemy enemy;
        Animator animator;
        Transform sword,rightShoulder,leftShoulder,tip;
        Vector3 home,scale,bladeCenter;
        Quaternion rotation;
        float bladeBottom,weight,rightReach=.7f,leftReach=.7f;
        TrailRenderer trail;

        public void Init(SumiEnemy source)
        {
            enemy=source;animator=GetComponent<Animator>();
            rightShoulder=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftShoulder=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightReach=Reach(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand);
            leftReach=Reach(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand);
            foreach(var mesh in GetComponentsInChildren<MeshFilter>())
            {
                if(mesh.name!="Curved blade")continue;
                sword=mesh.transform.parent;home=sword.localPosition;rotation=sword.localRotation;scale=sword.localScale;
                var b=mesh.sharedMesh.bounds;bladeCenter=b.center;bladeBottom=b.min.y;break;
            }
            tip=new GameObject("Enemy blade trail").transform;tip.SetParent(transform,false);
            trail=tip.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=SumiArt.Black;
            trail.time=.11f;trail.startWidth=.07f;trail.endWidth=.004f;trail.minVertexDistance=.025f;trail.emitting=false;
        }
        float Reach(HumanBodyBones shoulder,HumanBodyBones elbow,HumanBodyBones hand)
        {
            var a=animator.GetBoneTransform(shoulder);var b=animator.GetBoneTransform(elbow);var c=animator.GetBoneTransform(hand);
            return a&&b&&c?(Vector3.Distance(a.position,b.position)+Vector3.Distance(b.position,c.position))*.97f:.7f;
        }
        Vector3 ClampHand(Vector3 goal,Transform shoulder,float reach)
        {return shoulder?shoulder.position+Vector3.ClampMagnitude(goal-shoulder.position,reach):goal;}

        void OnAnimatorIK(int layer)
        {
            if(!enemy||!animator)return;
            weight=Mathf.MoveTowards(weight,enemy.dead?0:enemy.PoseWeight,Time.deltaTime*16);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight*.9f);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight*.8f);
            if(weight<=0||(enemy.CurrentAttack==null&&!enemy.Deflected))return;
            enemy.GetSwordPose(out var grip,out var line);
            grip=ClampHand(grip,rightShoulder,rightReach);
            animator.SetIKPosition(AvatarIKGoal.RightHand,grip);animator.SetIKRotation(AvatarIKGoal.RightHand,Quaternion.FromToRotation(Vector3.up,line));
            animator.SetIKPosition(AvatarIKGoal.LeftHand,ClampHand(grip-line*.18f,leftShoulder,leftReach));
            animator.SetIKRotation(AvatarIKGoal.LeftHand,Quaternion.FromToRotation(Vector3.up,line));
        }

        public void Release(Vector3 impulse)
        {
            enabled=false;if(trail){trail.emitting=false;trail.Clear();}
            if(!sword)return;
            SumiDeathFx.DropWeapon(sword,impulse);
            sword=null;
        }

        void LateUpdate()
        {
            if(!enemy||!sword||!tip)return;
            if(enemy.dead){if(trail)trail.emitting=false;return;}
            sword.localPosition=home;sword.localRotation=rotation;sword.localScale=scale;
            bool posed=!enemy.dead&&(enemy.CurrentAttack!=null||enemy.Deflected)&&weight>0;
            if(posed)
            {
                enemy.GetSwordPose(out var grip,out var line);grip=ClampHand(grip,rightShoulder,rightReach);
                Vector3 position=sword.position;Quaternion animated=sword.rotation;
                sword.rotation=Quaternion.FromToRotation(Vector3.up,line);
                // Katana is authored directly on the hand bone; matching the resolved IK root
                // prevents the wrapped grip from hovering beyond the palm.
                Vector3 goal=grip;
                sword.position=Vector3.Lerp(position,goal,weight);sword.rotation=Quaternion.Slerp(animated,sword.rotation,weight);
                tip.position=grip+line*(enemy.kind==SumiEnemyKind.Oni?1.45f:1.13f);
            }
            bool striking=posed&&enemy.state==SumiEnemyState.Strike;
            if(striking&&!trail.emitting)trail.Clear();
            trail.sharedMaterial=enemy.CurrentAttack!=null&&enemy.CurrentAttack.unblockable?SumiArt.Crimson:SumiArt.Black;
            trail.emitting=striking;
        }
    }
}
