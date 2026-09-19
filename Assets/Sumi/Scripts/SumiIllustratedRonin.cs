using UnityEngine;

namespace Sumi
{
    // Deform the original drawing in separate body, leg and fabric regions.
    // Distance-driven gait preserves costume and weapon continuity between steps.
    [DefaultExecutionOrder(20)]
    public class SumiIllustratedRonin : MonoBehaviour
    {
        public SumiRig rig;
        public SumiDrawing drawing;
        public float strideLength=2.15f;
        public float motionAmount { get; private set; }
        public float clothDeflection { get; private set; }
        SumiPlayer player;
        Mesh mesh;
        Vector3[] rest,vertices;
        Vector2[] uv;
        Vector3 previousPosition,previousVelocity;
        float phase,pace,lean,leanVelocity,cloth,clothVelocity,previousYaw;
        int direction=4;

        public void Init(SumiRig source)
        {
            rig=source;player=GetComponent<SumiPlayer>();
            foreach(var renderer in rig.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            drawing=new GameObject("Articulated ink ronin",typeof(SumiDrawing)).GetComponent<SumiDrawing>();
            drawing.transform.SetParent(transform,false);drawing.subdivisions=48;
            drawing.Setup(Resources.Load<Texture2D>("Drawings/RoninViews"),4,2.85f,2.85f);
            drawing.wind=0;drawing.Apply();
            mesh=drawing.GetComponent<MeshFilter>().sharedMesh;mesh.MarkDynamic();
            rest=mesh.vertices;vertices=new Vector3[rest.Length];uv=mesh.uv;
            mesh.bounds=new Bounds(new Vector3(0,1.25f,0),new Vector3(4.5f,4,2));
            previousPosition=transform.position;previousYaw=transform.eulerAngles.y;
        }

        void LateUpdate()
        {
            var camera=Camera.main;
            if(!drawing||!camera||Time.deltaTime<=0)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);
            Vector3 displacement=transform.position-previousPosition;displacement.y=0;
            previousPosition=transform.position;
            float speed=player?player.velocity.magnitude:rig.speed;
            bool dodge=player&&player.dodgeRemaining>0;
            Vector3 toCamera=camera.transform.position-transform.position;toCamera.y=0;
            float angle=Mathf.Repeat(Vector3.SignedAngle(transform.forward,toCamera.normalized,Vector3.up),360);
            // Hysteresis prevents view chatter near a direction boundary.
            if(Mathf.Abs(Mathf.DeltaAngle(direction*45,angle))>29)
                direction=Mathf.RoundToInt(angle/45)%8;
            drawing.frame=direction;
            pace=Mathf.MoveTowards(pace,dodge?0:Mathf.Clamp01(speed/3.4f),dt*5);
            if(!dodge)phase+=Mathf.Min(displacement.magnitude,.4f)/strideLength*Mathf.PI*2;
            float yawRate=Mathf.Clamp(Mathf.DeltaAngle(previousYaw,transform.eulerAngles.y)/dt,-260,260);
            previousYaw=transform.eulerAngles.y;
            Vector3 screenRight=camera.transform.right;screenRight.y=0;screenRight.Normalize();
            float lateral=Vector3.Dot(transform.forward,screenRight);
            Vector3 currentVelocity=player?player.velocity:Vector3.zero;
            float acceleration=Vector3.Dot(currentVelocity-previousVelocity,screenRight)/dt;
            previousVelocity=currentVelocity;
            float targetLean=Mathf.Clamp(lateral*speed*.012f-yawRate*.00026f+acceleration*.0014f,-.16f,.16f);
            lean=Mathf.SmoothDamp(lean,targetLean,ref leanVelocity,.14f,2,dt);
            float wind=Mathf.Sin(Time.time*1.8f)*.06f+Mathf.Sin(Time.time*3.7f)*.025f;
            float targetCloth=wind-lateral*speed*.044f+yawRate*.00075f;
            // Stable spring integration leaves follow-through after movement stops.
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt/.012f));float h=dt/steps;
            for(int s=0;s<steps;s++){clothVelocity+=(targetCloth-cloth)*48*h;clothVelocity*=Mathf.Exp(-6*h);cloth+=clothVelocity*h;}
            cloth=Mathf.Clamp(cloth,-.45f,.45f);clothDeflection=cloth;
            float breath=Mathf.Sin(Time.time*2.05f)*.013f;
            float hipSway=Mathf.Sin(phase)*.042f*pace;
            float bob=(1-Mathf.Cos(phase*2))*.025f*pace;
            float duck=dodge?Mathf.Sin(Mathf.PI*Mathf.Clamp01(1-player.dodgeRemaining/player.config.dodgeDuration))*.28f:0;
            float sideView=Mathf.Abs(lateral);
            motionAmount=0;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=rest[i];float x=uv[i].x,y=uv[i].y;
                float upper=Smooth(.26f,.77f,y);
                float leg=1-Smooth(.09f,.40f,y);
                float left=1-Smooth(.46f,.54f,x);
                float leftPhase=Mathf.Sin(phase),rightPhase=-leftPhase;
                float swing=Mathf.Lerp(rightPhase,leftPhase,left);
                float lift=Mathf.Lerp(Mathf.Max(0,rightPhase),Mathf.Max(0,leftPhase),left);
                // Alternating lift and travel below the knee; weight shift above it.
                p.x+=leg*swing*pace*(.055f+.17f*sideView);
                p.y+=leg*lift*pace*.17f;
                p.z+=leg*swing*pace*.10f*(1-sideView);
                p.x+=hipSway*(1-leg)+lean*Mathf.Max(0,p.y-.16f);
                p.y+=(bob+breath*upper-duck)*(1-leg);
                // Hat and feet stay outside the flutter masks.
                float outer=Smooth(.10f,.37f,Mathf.Abs(x-.5f));
                float garment=Smooth(.08f,.28f,y)*(1-Smooth(.65f,.84f,y));
                float hem=(1-Smooth(.33f,.66f,y))*garment;
                float flutter=Mathf.Sin(Time.time*6-y*13+x*7)*.025f+Mathf.Sin(Time.time*9+x*17)*.009f;
                p.x+=(cloth*(.3f+outer*.9f)+flutter*(.5f+pace))*garment;
                p.y+=Mathf.Sin(Time.time*4.2f+x*9-y*7)*(.015f+pace*.022f)*outer*garment;
                p.z+=(cloth*.4f+Mathf.Sin(phase-1)*pace*.045f)*hem;
                vertices[i]=p;motionAmount=Mathf.Max(motionAmount,(p-rest[i]).magnitude);
            }
            mesh.vertices=vertices;
            drawing.Apply();
        }

        static float Smooth(float a,float b,float value)
        {return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));}
    }
}
