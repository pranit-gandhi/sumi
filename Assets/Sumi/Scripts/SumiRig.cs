using UnityEngine;
namespace Sumi
{
    public class SumiRig : MonoBehaviour
    {
        public Transform body, head, upperR,upperL,foreR,foreL,legR,legL,shinR,shinL,sword,clothR,clothL;
        public TrailRenderer trail;
        public Animator animator;
        public float speed,poseTime,poseDuration=1, hitLean;
        public int pose,combo;
        public bool oni;
        public Renderer rim;
        float gait;
        public void Construct(bool boss=false,bool enemy=false)
        {
            oni=boss;
            var ink=SumiArt.Black;var cloth=enemy?SumiArt.Mat("EnemyCloth",new Color(.22f,.24f,.25f)):SumiArt.White;
            body=SumiArt.Node("Body",transform,new Vector3(0,1,0));
            SumiArt.Shape("Layered haori",body,SumiArt.Torso,Vector3.zero,Vector3.one,cloth);
            var collar=SumiArt.Box("Crossed collar",body,new Vector3(.05f,.63f,.27f),new Vector3(.10f,.47f,.05f),ink).transform;collar.localRotation=Quaternion.Euler(0,0,25);
            SumiArt.Box("Obi",body,new Vector3(0,.18f,0),new Vector3(.76f,.15f,.53f),ink);
            head=SumiArt.Node("Head",body,new Vector3(0,.9f,0));
            SumiArt.Shape("Shadowed face",head,SumiArt.Cylinder,Vector3.zero,new Vector3(.34f,.38f,.30f),SumiArt.Stone);
            if(!boss){SumiArt.Shape("Black kasa",head,SumiArt.Hat,new Vector3(0,.16f,0),Vector3.one,ink);rim=SumiArt.Shape("Hat edge",head,SumiArt.Hat,new Vector3(0,.151f,0),new Vector3(1.012f,.85f,1.012f),SumiArt.Stone).GetComponent<Renderer>();}
            else {
                SumiArt.Box("Oni mask",head,new Vector3(0,0,.14f),new Vector3(.49f,.42f,.15f),SumiArt.Mat("OniMask",new Color(.44f,.08f,.085f)));
                for(int i=-1;i<=1;i+=2){var horn=SumiArt.Shape("Horn",head,SumiArt.Cone,new Vector3(i*.23f,.35f,0),new Vector3(.16f,.58f,.17f),SumiArt.White).transform;horn.localRotation=Quaternion.Euler(0,0,-i*24);SumiArt.Box("Eye",head,new Vector3(i*.12f,.055f,.225f),new Vector3(.11f,.035f,.025f),SumiArt.Crimson);}
            }
            upperR=Arm("Right",body,.4f,cloth,out foreR);upperL=Arm("Left",body,-.4f,cloth,out foreL);
            legR=Leg("Right",transform,.21f,out shinR);legL=Leg("Left",transform,-.21f,out shinL);
            clothR=SumiArt.Node("CoatRight",body,new Vector3(.2f,.12f,-.12f));clothL=SumiArt.Node("CoatLeft",body,new Vector3(-.2f,.12f,-.12f));
            foreach(var c in new[]{clothR,clothL}) SumiArt.Shape("Coat tail",c,SumiArt.Sleeve,new Vector3(0,-.05f,0),new Vector3(1.1f,1.7f,.35f),cloth);
            sword=SumiArt.Node("Katana",foreR,new Vector3(0,-.47f,.03f));sword.localRotation=Quaternion.Euler(-90,0,0);
            SumiArt.Box("Wrapped grip",sword,new Vector3(0,.08f,0),new Vector3(.06f,.25f,.065f),ink);
            SumiArt.Shape("Tsuba",sword,SumiArt.Cylinder,new Vector3(0,.24f,0),new Vector3(.18f,.026f,.18f),SumiArt.Stone);
            var blade=SumiArt.Beam("Katana steel",sword,new Vector3(0,.26f,0),new Vector3(.045f,1.26f,0),boss?.10f:.045f,SumiArt.Mat("Steel",new Color(.8f,.84f,.82f)));
            SumiArt.Beam("Cutting edge",sword,new Vector3(-.025f,.26f,.022f),new Vector3(.020f,1.27f,.022f),.012f,SumiArt.Moon);
            var tip=SumiArt.Node("Blade tip",sword,new Vector3(.045f,1.27f,0));trail=tip.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=SumiArt.Mat("Trail",new Color(.85f,.83f,.68f),true);trail.time=.13f;trail.startWidth=.24f;trail.endWidth=0;trail.minVertexDistance=.05f;trail.emitting=false;
            SumiArt.Beam("Scabbard",body,new Vector3(-.37f,.22f,.10f),new Vector3(-.55f,-.55f,-.42f),.07f,ink);
            animator=gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("Sumi/RoninLocomotion");animator.applyRootMotion=false;
        }
        Transform Arm(string name,Transform parent,float x,Material material,out Transform fore)
        {
            var upper=SumiArt.Node(name+"Arm",parent,new Vector3(x,.65f,0));SumiArt.Shape(name+"Sleeve",upper,SumiArt.Sleeve,Vector3.zero,Vector3.one,material);
            fore=SumiArt.Node(name+"Forearm",upper,new Vector3(0,-.42f,0));SumiArt.Box("Cloth wrap",fore,new Vector3(0,-.16f,0),new Vector3(.14f,.35f,.15f),SumiArt.Stone);SumiArt.Box("Hand",fore,new Vector3(0,-.38f,0),new Vector3(.13f,.16f,.13f),SumiArt.Black);return upper;
        }
        Transform Leg(string name,Transform parent,float x,out Transform shin)
        {
            var upper=SumiArt.Node(name+"Leg",parent,new Vector3(x,1.03f,0));SumiArt.Shape("Hakama folds",upper,SumiArt.Sleeve,Vector3.zero,new Vector3(1.0f,1.18f,1.0f),SumiArt.Black);
            shin=SumiArt.Node(name+"Shin",upper,new Vector3(0,-.49f,0));SumiArt.Box("Leg wrap",shin,new Vector3(0,-.20f,0),new Vector3(.15f,.35f,.16f),SumiArt.Stone);SumiArt.Box("Sandal",shin,new Vector3(0,-.44f,.10f),new Vector3(.23f,.10f,.40f),SumiArt.Black);return upper;
        }
        void LateUpdate()
        {
            if(!body)return;
            gait+=Time.deltaTime*Mathf.Lerp(1.7f,10f,speed/4.6f);
            if(animator&&animator.runtimeAnimatorController)animator.SetFloat("Speed",speed,.10f,Time.deltaTime);
            else {float walk=Mathf.Sin(gait)*Mathf.Min(speed/4.6f,1)*30;legR.localRotation=Quaternion.Euler(walk,0,0);legL.localRotation=Quaternion.Euler(-walk,0,0);shinR.localRotation=Quaternion.Euler(Mathf.Max(0,-walk),0,0);shinL.localRotation=Quaternion.Euler(Mathf.Max(0,walk),0,0);}
            float t=Mathf.Clamp01(poseTime/Mathf.Max(.01f,poseDuration)),s=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.18f)/.40f));
            Vector3 ur=new Vector3(-16,0,-12),ul=new Vector3(-12,0,12),fr=new Vector3(-30,0,0),fl=new Vector3(-20,0,0);float twist=0,lean=0;
            if(pose==1){float sign=combo==1?-1:1;ur=Vector3.Lerp(new Vector3(-125,sign*-35,-40),new Vector3(-45,sign*70,45),s);ul=Vector3.Lerp(new Vector3(-100,0,50),new Vector3(-60,0,15),s);fr=new Vector3(-65+50*s,0,0);twist=sign*Mathf.Lerp(-38,48,s);lean=Mathf.Sin(t*Mathf.PI)*12;}
            if(pose==2){ur=new Vector3(-80,-20,-35);ul=new Vector3(-78,25,35);fr=new Vector3(-80,0,0);fl=new Vector3(-85,0,0);lean=-5;}
            if(pose==3){lean=33;ur=new Vector3(25,0,-20);ul=new Vector3(25,0,20);}
            if(pose==4){lean=-Mathf.Sin(t*Mathf.PI)*32;ur=new Vector3(-25,0,-55);ul=new Vector3(-30,0,45);}
            if(pose==5){ur=Vector3.Lerp(new Vector3(-150,0,-30),new Vector3(-30,60,30),s);ul=Vector3.Lerp(new Vector3(-130,0,35),new Vector3(-35,-30,15),s);fr=new Vector3(-35,0,0);twist=Mathf.Lerp(-40,60,s);lean=15;}
            if(pose==6){lean=60;ur=new Vector3(15,0,-30);ul=new Vector3(15,0,30);}
            body.localRotation=Quaternion.Euler(lean+hitLean,twist,pose==4?12:0);
            upperR.localRotation=Quaternion.Euler(ur);upperL.localRotation=Quaternion.Euler(ul);foreR.localRotation=Quaternion.Euler(fr);foreL.localRotation=Quaternion.Euler(fl);
            clothR.localRotation=Quaternion.Euler(-12-speed*4+Mathf.Sin(gait+1)*8,0,8);clothL.localRotation=Quaternion.Euler(-12-speed*4+Mathf.Sin(gait)*8,0,-8);
            hitLean=Mathf.MoveTowards(hitLean,0,Time.deltaTime*70);
        }
    }
}
