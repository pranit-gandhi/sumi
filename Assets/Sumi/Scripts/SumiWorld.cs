using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Sumi
{
    public static class SumiWorld
    {
        public static Transform Build()
        {
            Random.InitState(417);
            var root=new GameObject("SumiWorld").transform;
            var black=SumiArt.Black;var stone=SumiArt.Stone;var paper=SumiArt.White;
            SumiArt.Box("Courtyard foundation",root,new Vector3(0,-.22f,0),new Vector3(48,.4f,50),SumiArt.Mat("Ground",new Color(.88f,.89f,.86f)),true);
            for(int x=-5;x<=5;x++)for(int z=-7;z<=7;z++)
            {
                if(Mathf.Abs(x)>1&&Random.value>.22f)continue;
                var tile=SumiArt.Box("Weathered paving",root,new Vector3(x*1.64f+Random.Range(-.16f,.16f),-.009f,z*1.44f+Random.Range(-.09f,.09f)),new Vector3(Random.Range(1.22f,1.56f),.055f,Random.Range(1.13f,1.40f)),SumiArt.Mat("Paving"+(Mathf.Abs(x*7+z)%4),Color.Lerp(stone.color,paper.color,.5f+(Mathf.Abs(x*7+z)%4)*.085f)));
                tile.transform.localRotation=Quaternion.Euler(0,Random.Range(-5f,5f),0);
            }
            Shrine(root,new Vector3(0,0,14),true);Shrine(root,new Vector3(-14,0,5),false);Shrine(root,new Vector3(14,0,8),false);
            Torii(root,new Vector3(0,0,-12),8,5.5f);Torii(root,new Vector3(0,0,9.5f),6,4.8f);
            for(int i=-1;i<=1;i+=2)
            {
                for(int z=-9;z<=10;z+=5) Lantern(root,new Vector3(i*9.5f,0,z));
                for(int z=-10;z<=12;z+=2)
                {
                    SumiArt.Box("Fence post",root,new Vector3(i*11.8f,.7f,z),new Vector3(.18f,1.5f,.18f),black);
                    SumiArt.Box("Fence rail",root,new Vector3(i*11.8f,.8f,z),new Vector3(.10f,.12f,2.05f),black);
                }
                var boundary=SumiArt.Box("Arena boundary",root,new Vector3(i*12.2f,1,0),new Vector3(.3f,2,25),black,true);boundary.GetComponent<Renderer>().enabled=false;
                for(int n=0;n<4;n++) Tree(root,new Vector3(i*Random.Range(14f,21f),0,Random.Range(-8f,24f)),Random.Range(.8f,1.5f));
                for(int n=0;n<7;n++) Mountain(root,new Vector3(i*(10+n*7),0,37+Random.Range(-2,15)),n);
                var banner=SumiArt.Node("Wind banner",root,new Vector3(i*7.8f,0,7));
                SumiArt.Box("Banner pole",banner,new Vector3(0,2,0),new Vector3(.08f,4,.08f),black);
                var cloth=SumiArt.Box("Offering cloth",banner,new Vector3(.48f,3,0),new Vector3(.9f,1.5f,.018f),paper).transform;
                cloth.gameObject.AddComponent<SumiWind>();
                Accent(SumiArt.Box("Banner seal",cloth,new Vector3(0,0,-1f),new Vector3(.35f,.48f,.2f),black),35);
            }
            // Invisible collision only across entrance and shrine steps keeps traversal predictable.
            foreach(float z in new[]{-12.8f,11.5f}) {var wall=SumiArt.Box("Fog boundary",root,new Vector3(0,1,z),new Vector3(25,2,.2f),black,true);wall.GetComponent<Renderer>().enabled=false;}
            var moon=SumiArt.Shape("Moon",root,SumiArt.Cylinder,new Vector3(-5,18,37),new Vector3(9,.07f,9),SumiArt.Moon).transform;moon.localRotation=Quaternion.Euler(85,0,0);
            Accent(SumiArt.Shape("Moon gold edge",moon,SumiArt.Cylinder,Vector3.zero,new Vector3(1.025f,.8f,1.025f),stone),80);
            var light=new GameObject("Moonlight");light.transform.SetParent(root);light.transform.rotation=Quaternion.Euler(48,-35,0);var sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(.86f,.91f,1f);sun.intensity=1.3f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.75f;
            RenderSettings.sun=sun;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.75f,.76f,.75f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.88f,.89f,.87f);RenderSettings.fogDensity=.029f;
            var volumeGO=new GameObject("Ink atmosphere");volumeGO.transform.SetParent(root);var vol=volumeGO.AddComponent<Volume>();vol.isGlobal=true;vol.sharedProfile=Resources.Load<VolumeProfile>("Sumi/Atmosphere");
            return root;
        }
        static void Accent(GameObject go,float threshold){var a=go.AddComponent<SumiGoldSurface>();a.threshold=threshold;}
        static void Torii(Transform root,Vector3 pos,float width,float height)
        {
            var t=SumiArt.Node("Torii",root,pos);var black=SumiArt.Black;
            for(int i=-1;i<=1;i+=2){SumiArt.Shape("Torii pillar",t,SumiArt.Cylinder,new Vector3(i*width*.38f,height*.47f,0),new Vector3(.45f,height,.45f),black,true);SumiArt.Shape("Stone foot",t,SumiArt.Cylinder,new Vector3(i*width*.38f,.2f,0),new Vector3(.85f,.4f,.85f),SumiArt.Stone);}
            SumiArt.Box("Lower lintel",t,new Vector3(0,height*.78f,0),new Vector3(width,.24f,.30f),black);
            SumiArt.Box("Crown lintel",t,new Vector3(0,height,0),new Vector3(width*1.2f,.35f,.52f),black);
            for(int i=-1;i<=1;i+=2){var end=SumiArt.Box("Swept crown",t,new Vector3(i*width*.60f,height+.11f,0),new Vector3(width*.2f,.3f,.52f),black).transform;end.localRotation=Quaternion.Euler(0,0,i*12);}
            Accent(SumiArt.Box("Torii gold inlay",t,new Vector3(0,height+.17f,-.015f),new Vector3(width*1.21f,.05f,.54f),SumiArt.Stone),55);
            SumiArt.Box("Central tablet",t,new Vector3(0,height*.87f,-.2f),new Vector3(.60f,.68f,.12f),black);
            for(int j=-3;j<=3;j++){float x=j*width*.1f;float y=height*.66f+.09f*j*j;SumiArt.Beam("Sacred rope",t,new Vector3(x-.35f,y+.08f,0),new Vector3(x+.35f,y,0),.05f,SumiArt.Stone);if(j%2==0){var paper=SumiArt.Box("Folded paper",t,new Vector3(x,y-.18f,0),new Vector3(.17f,.32f,.02f),SumiArt.White).transform;paper.localRotation=Quaternion.Euler(0,0,20);paper.gameObject.AddComponent<SumiWind>();}}
        }
        static void Shrine(Transform root,Vector3 pos,bool main)
        {
            var t=SumiArt.Node(main?"Moon shrine":"Charcoal hut",root,pos);if(!main)t.localRotation=Quaternion.Euler(0,pos.x<0?75:-75,0);
            float w=main?7:4;var ink=SumiArt.Black;
            for(int s=0;s<3;s++)SumiArt.Box("Stone step",t,new Vector3(0,s*.22f,-2.4f+s*.45f),new Vector3(w+1-s*.3f,.24f,1.8f),SumiArt.Stone,true);
            SumiArt.Box("Raised timber floor",t,new Vector3(0,.7f,0),new Vector3(w,.4f,4.4f),ink,true);
            SumiArt.Box("Paper wall",t,new Vector3(0,2,1.7f),new Vector3(w,2.4f,.2f),SumiArt.White,true);
            for(int i=0;i<8;i++){float x=(i/7f-.5f)*w;SumiArt.Box("Timber frame",t,new Vector3(x,2,1.55f),new Vector3(.12f,2.5f,.18f),ink);}
            for(int i=-1;i<=1;i+=2) {SumiArt.Box("Pillar",t,new Vector3(i*(w*.5f-.15f),2,-1.5f),new Vector3(.25f,2.6f,.25f),ink,true);SumiArt.Box("Side wall",t,new Vector3(i*w*.5f,1.8f,.2f),new Vector3(.2f,2.2f,3.2f),ink,true);}
            for(int i=0;i<5;i++)SumiArt.Box("Lattice",t,new Vector3(0,1.1f+i*.43f,1.5f),new Vector3(w,.065f,.12f),ink);
            SumiArt.Shape("Swept tiled roof",t,SumiArt.Roof(),new Vector3(0,3.1f,0),new Vector3(w*.65f,1.4f,2.8f),ink);
            for(int j=-9;j<=9;j++){float x=j*w*.065f;float u=Mathf.Abs(j)/9f;float y=3.15f+1.26f*(1-u)*(1-u)+.16f*Mathf.Pow(u,8);SumiArt.Box("Roof seam",t,new Vector3(x,y,0),new Vector3(.045f,.045f,5.4f+u*.35f),SumiArt.Stone);}
            Accent(SumiArt.Box("Shrine ridge",t,new Vector3(0,4.46f,0),new Vector3(.12f,.12f,5.8f),SumiArt.Stone),65);
            if(main){SumiArt.Box("Offering altar",t,new Vector3(0,1.2f,-.3f),new Vector3(2,.7f,1),ink);for(int i=-1;i<=1;i+=2)Lantern(t,new Vector3(i*2.5f,.85f,-1));}
        }
        static void Lantern(Transform root,Vector3 pos)
        {
            var t=SumiArt.Node("Stone lantern",root,pos);var stone=SumiArt.Stone;
            SumiArt.Box("Lantern base",t,new Vector3(0,.15f,0),new Vector3(.9f,.3f,.9f),stone,true);
            SumiArt.Shape("Lantern stem",t,SumiArt.Cylinder,new Vector3(0,.70f,0),new Vector3(.28f,.9f,.28f),stone);
            SumiArt.Box("Lantern chamber",t,new Vector3(0,1.35f,0),new Vector3(.64f,.48f,.64f),SumiArt.Black);
            Accent(SumiArt.Box("Lantern paper",t,new Vector3(0,1.36f,-.325f),new Vector3(.37f,.28f,.015f),SumiArt.White),20);
            SumiArt.Shape("Lantern cap",t,SumiArt.Cone,new Vector3(0,1.78f,0),new Vector3(.75f,.42f,.75f),stone);
        }
        static void Tree(Transform root,Vector3 pos,float size)
        {
            var t=SumiArt.Node("Wind pine",root,pos);t.localScale=Vector3.one*size;
            SumiArt.Beam("Trunk",t,Vector3.zero,new Vector3(.4f,5,0),.34f,SumiArt.Black);
            for(int i=0;i<7;i++)
            {
                float side=i%2==0?-1:1;var start=new Vector3(.2f,2.2f+i*.4f,0);var end=start+new Vector3(side*(2.2f-i*.2f),.7f,Random.Range(-.6f,.6f));SumiArt.Beam("Branch",t,start,end,.08f,SumiArt.Black);
                for(int j=0;j<10;j++) {var tip=end+new Vector3(Random.Range(-.8f,.8f),Random.Range(-.12f,.4f),Random.Range(-.7f,.7f));SumiArt.Beam("Pine brush needles",t,tip,tip+new Vector3(Random.Range(-.4f,.4f),.15f,Random.Range(-.2f,.2f)),.035f,SumiArt.Black);}
            }
        }
        static void Mountain(Transform root,Vector3 pos,int index)
        {
            var vertices=new Vector3[30];var triangles=new int[84];
            for(int i=0;i<15;i++){float x=(i/14f-.5f)*28;float y=Mathf.Sin(i/14f*Mathf.PI)*Random.Range(6f,13f);vertices[i*2]=new Vector3(x,-1,0);vertices[i*2+1]=new Vector3(x,y,0);if(i<14){int n=i*6,a=i*2;triangles[n]=a;triangles[n+1]=a+1;triangles[n+2]=a+2;triangles[n+3]=a+1;triangles[n+4]=a+3;triangles[n+5]=a+2;}}
            var mesh=new Mesh{name="PaintedMountain"+index+"_"+pos.x};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();SumiArt.Meshes[mesh.name]=mesh;
            SumiArt.Shape("Painted mountain wash",root,mesh,pos,Vector3.one,SumiArt.Mat("DistantInk",new Color(.64f,.66f,.65f)));
        }
    }
}
