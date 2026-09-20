using UnityEngine;

namespace Sumi
{
    // One timing definition drives anticipation, sword posing, contact and recovery.
    public sealed class SumiEnemyAttack
    {
        public readonly string name;
        public readonly float windup,strike,recovery,damage,reach,halfAngle,lunge;
        public readonly bool braced,unblockable;
        public readonly SumiSwordArc arc;
        public readonly SumiEnemyAttack followUp;
        public float ContactStart=>strike*.32f;
        public float ContactEnd=>strike*.64f;

        SumiEnemyAttack(string name,float windup,float strike,float recovery,float damage,float reach,float halfAngle,float lunge,
            SumiSwordArc arc,bool braced=false,bool unblockable=false,SumiEnemyAttack followUp=null)
        {
            this.name=name;this.windup=windup;this.strike=strike;this.recovery=recovery;this.damage=damage;
            this.reach=reach;this.halfAngle=halfAngle;this.lunge=lunge;this.arc=arc;
            this.braced=braced;this.unblockable=unblockable;this.followUp=followUp;
        }

        public static readonly SumiEnemyAttack Return=new SumiEnemyAttack("RETURN CUT",.38f,.32f,.72f,12,2.25f,48,.25f,SumiSwordArc.Returning);
        public static readonly SumiEnemyAttack RetainerCut=new SumiEnemyAttack("TWIN CUT",.52f,.34f,.65f,16,2.3f,48,.5f,SumiSwordArc.Descending,followUp:Return);
        public static readonly SumiEnemyAttack RetainerHeavy=new SumiEnemyAttack("BRACED CUT",.82f,.40f,.9f,22,2.4f,32,.65f,SumiSwordArc.Overhead,true);
        public static readonly SumiEnemyAttack ShadeLunge=new SumiEnemyAttack("INK LUNGE",.50f,.32f,.55f,14,2.2f,28,1.25f,SumiSwordArc.Descending);
        public static readonly SumiEnemyAttack OniReturn=new SumiEnemyAttack("ONI RETURN",.46f,.40f,.95f,18,2.9f,65,.35f,SumiSwordArc.Returning,true);
        public static readonly SumiEnemyAttack OniCut=new SumiEnemyAttack("ONI CUT",.68f,.42f,.80f,23,2.9f,52,.55f,SumiSwordArc.Descending,true);
        public static readonly SumiEnemyAttack OniTwin=new SumiEnemyAttack("ONI TWIN CUT",.62f,.42f,.85f,23,2.9f,52,.55f,SumiSwordArc.Descending,true,followUp:OniReturn);
        public static readonly SumiEnemyAttack OniHeavy=new SumiEnemyAttack("FALLING MOUNTAIN",1.04f,.46f,1.15f,28,3.1f,30,.8f,SumiSwordArc.Overhead,true);
        public static readonly SumiEnemyAttack OniSweep=new SumiEnemyAttack("CRIMSON SWEEP",.98f,.48f,1.05f,24,3.15f,115,.15f,SumiSwordArc.Sweep,true,true);

        public void LocalPose(float amount,out Vector3 grip,out Vector3 line)
        {
            float t=Mathf.SmoothStep(0,1,amount);
            if(arc==SumiSwordArc.Sweep)
            {
                grip=Vector3.Lerp(new Vector3(-.4f,1.16f,.32f),new Vector3(.4f,1.10f,.4f),t);
                float angle=Mathf.Lerp(-115,115,t)*Mathf.Deg2Rad;
                line=new Vector3(Mathf.Sin(angle),-.1f,Mathf.Cos(angle)).normalized;
            }
            else if(arc==SumiSwordArc.Overhead)
            {
                grip=Vector3.Lerp(new Vector3(.10f,1.82f,.23f),new Vector3(.02f,.91f,.56f),t);
                line=Vector3.Slerp(new Vector3(.02f,.96f,.28f),new Vector3(.02f,-.58f,.82f),t).normalized;
            }
            else
            {
                if(arc==SumiSwordArc.Returning)t=1-t;
                grip=Vector3.Lerp(new Vector3(.38f,1.58f,.25f),new Vector3(-.38f,1.02f,.52f),t);
                float angle=Mathf.Lerp(68,-68,t)*Mathf.Deg2Rad;
                line=new Vector3(Mathf.Sin(angle)*.78f,Mathf.Sin(angle)*.62f,Mathf.Cos(angle)).normalized;
            }
        }
    }
}
