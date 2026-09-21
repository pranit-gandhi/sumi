using UnityEngine;
using UnityEngine.Rendering;

namespace Sumi
{
    // Dimensional ink architecture. Flat drawings are reserved for the distant horizon.
    public static class SumiSketchWorld
    {
        static Transform root;
        static Material ink, charcoal, pale, stone, distant, roofWash, ground, grass, wet, amber, reflection, lightPool;
        static Texture2D atlas;

        public static Transform Build()
        {
            var oldRandom=Random.state; Random.InitState(2209);
            root=new GameObject("Sumi — dimensional ink village").transform;
            atlas=Resources.Load<Texture2D>("Drawings/ShrineAtlas");
            ink=SumiArt.Mat("Ink",new Color(.028f,.026f,.024f));
            charcoal=SumiArt.Mat("CharcoalWood",new Color(.095f,.086f,.078f));
            pale=SumiArt.Mat("Paper",new Color(.36f,.34f,.31f));
            stone=SumiArt.Mat("Stone",new Color(.19f,.18f,.165f));
            distant=SumiArt.Mat("DistantInk",new Color(.20f,.145f,.115f));
            roofWash=SumiArt.Mat("RoofWash",new Color(.105f,.095f,.087f));
            ground=SumiArt.Mat("WetCharcoalGround",new Color(.13f,.125f,.118f));
            grass=SumiArt.Mat("PaleGrass",new Color(.78f,.72f,.61f));
            wet=SumiArt.Mat("DampPatch",new Color(.035f,.031f,.028f),true);
            amber=SumiArt.Mat("LanternGlow",new Color(2.2f,.82f,.19f),true);
            reflection=SumiArt.Mat("AmberReflection",new Color(1.45f,.48f,.10f),true);
            lightPool=SumiArt.Mat("LanternLightPool",new Color(.72f,.26f,.065f),true);
            SetInk(ink,new Color(.028f,.026f,.024f),.13f);SetInk(charcoal,new Color(.095f,.086f,.078f),.22f);
            SetInk(pale,new Color(.36f,.34f,.31f),.10f);SetInk(stone,new Color(.19f,.18f,.165f),.19f);
            SetInk(distant,new Color(.20f,.145f,.115f),.31f);SetInk(roofWash,new Color(.105f,.095f,.087f),.29f);

            // One generous brush-painted courtyard. The middle stays empty and readable while
            // four gates, dwellings and mountain washes imply a settlement continuing forever.
            SumiArt.Box("Damp charcoal courtyard",root,new Vector3(0,-.18f,0),new Vector3(86,.35f,92),ground,true);
            for(int ring=0;ring<5;ring++) for(int i=0;i<28;i++)
            {
                float a=i*Mathf.PI*2/28f+ring*.027f,r=5.2f+ring*3.1f;
                Vector3 p=new Vector3(Mathf.Sin(a)*r,.006f,Mathf.Cos(a)*r),tangent=new Vector3(Mathf.Cos(a),0,-Mathf.Sin(a));
                Stroke(root,p-tangent*Random.Range(.22f,.7f),p+tangent*Random.Range(.3f,.95f),Random.Range(.006f,.018f),Random.Range(.08f,.28f));
            }
            Torii(root,new Vector3(0,0,21.8f),7.4f,5.8f); Torii(root,new Vector3(0,0,-21.8f),7.4f,5.8f);
            var east=SumiArt.Node("East gate quarter",root,new Vector3(21.8f,0,0));east.localRotation=Quaternion.Euler(0,90,0);Torii(east,Vector3.zero,7.4f,5.8f);
            var west=SumiArt.Node("West gate quarter",root,new Vector3(-21.8f,0,0));west.localRotation=Quaternion.Euler(0,90,0);Torii(west,Vector3.zero,7.4f,5.8f);
            Shrine(root,new Vector3(0,0,31f),.92f,true,0);
            for(int quadrant=0;quadrant<4;quadrant++)
            {
                float a=quadrant*Mathf.PI*.5f+.70f;Vector3 radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));int side=radial.x<0?-1:1;
                Hut(root,radial*28f,Random.Range(.84f,1.05f),side,quadrant);Lantern(root,radial*18.8f,Random.Range(.82f,1.05f),quadrant);
            }
            BuildGroundComposition();
            BuildLanternRhythm();
            BuildPerimeterDressing();
            BuildDistantSettlement();
            BuildMountainLayers();
            for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24f;Vector3 p=new Vector3(Mathf.Sin(a)*21f,1,Mathf.Cos(a)*21f);var w=SumiArt.Box("Invisible arena wall",root,p,new Vector3(5.7f,2,.32f),ink,true);w.transform.rotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);w.GetComponent<Renderer>().enabled=false;}
            EncounterAnchors();
            int accent=0;foreach(var r in root.GetComponentsInChildren<Renderer>()){if((r.name.Contains("Lantern chamber")||r.name.Contains("Torii crown")||r.name.Contains("Ridge brush"))&&accent++<18){var g=r.gameObject.AddComponent<SumiGoldSurface>();g.threshold=18+(accent%4)*19;}}
            Atmosphere(); Random.state=oldRandom; return root;
        }

        static void BuildRoad()
        {
            for(int i=0;i<78;i++)
            {
                float z=-42+i*2.05f+Random.Range(-.18f,.18f);
                var slab=SumiArt.Shape("Hand-hewn path stone",root,IrregularSlab(i),new Vector3(Random.Range(-.42f,.42f),-.025f,z),new Vector3(Random.Range(2.6f,4.25f),.11f,Random.Range(1.15f,1.8f)),i%4==0?stone:pale);
                slab.transform.localRotation=Quaternion.Euler(0,Random.Range(-5.5f,5.5f),0);
            }
            for(int i=0;i<56;i++)
            {
                float z=-40+i*2.9f,side=i%2==0?-1:1;
                Stroke(root,new Vector3(side*Random.Range(3.2f,5f),.006f,z),new Vector3(side*Random.Range(4.2f,6.4f),.007f,z+Random.Range(.2f,.8f)),Random.Range(.008f,.018f),.18f);
            }
        }

        static void Shrine(Transform parent,Vector3 pos,float scale,bool grand,int variant)
        {
            var t=SumiArt.Node(grand?"Moon shrine — dimensional":"Roadside shrine",parent,pos);t.localScale=Vector3.one*scale;
            float w=grand?8.2f:4.8f,depth=grand?5.4f:3.6f;
            for(int s=0;s<4;s++) SumiArt.Shape("Weathered stone step",t,IrregularSlab(70+s),new Vector3(0,s*.17f,-depth*.56f+s*.38f),new Vector3(w+1.4f-s*.32f,.23f,1.35f),stone,true);
            SumiArt.Box("Deep foundation wash",t,new Vector3(0,.62f,0),new Vector3(w,.55f,depth),charcoal,true);
            SumiArt.Box("Raised veranda",t,new Vector3(0,.92f,-depth*.32f),new Vector3(w+1,.20f,depth*.38f),charcoal,true);
            SumiArt.Box("Recessed paper wall",t,new Vector3(0,2.18f,depth*.32f),new Vector3(w-1f,2.4f,.16f),pale,true);
            for(int side=-1;side<=1;side+=2)
            {
                TaperedPost(t,new Vector3(side*w*.48f,2.12f,-depth*.34f),2.95f,.28f);
                TaperedPost(t,new Vector3(side*w*.48f,2.12f,depth*.34f),2.95f,.25f);
                SumiArt.Box("Side paper wall",t,new Vector3(side*w*.49f,2.02f,0),new Vector3(.14f,2.1f,depth*.72f),pale,true);
            }
            int bays=grand?7:4;
            for(int i=0;i<=bays;i++)
            {
                float x=Mathf.Lerp(-w*.43f,w*.43f,i/(float)bays);
                SumiArt.Box("Uneven timber mullion",t,new Vector3(x,2.14f,depth*.305f),new Vector3(i%2==0?.105f:.075f,2.28f,.19f),charcoal);
            }
            for(int row=0;row<4;row++) SumiArt.Box("Paper lattice",t,new Vector3(0,1.35f+row*.48f,depth*.292f),new Vector3(w*.86f,.055f,.20f),charcoal);
            SumiArt.Box("Heavy eave beam",t,new Vector3(0,3.28f,0),new Vector3(w+1.05f,.28f,depth+.45f),ink);
            LayeredRoof(t,new Vector3(0,3.31f,0),w+2,depth+1.4f,grand?1.52f:1.15f,variant);
            if(grand)
            {
                LayeredRoof(t,new Vector3(0,4.24f,.35f),w*.67f,depth*.72f,1f,variant+11);
                SumiArt.Box("Dark inner sanctuary",t,new Vector3(0,2.05f,depth*.25f-.05f),new Vector3(1.7f,2.2f,.12f),ink);
                for(int panel=-1;panel<=1;panel++)SetNoShadows(SumiArt.Box("Veiled sanctuary glow",t,new Vector3(panel*1.02f,2.10f,depth*.285f),new Vector3(.68f,1.18f,.022f),amber));
                var innerLight=new GameObject("Sanctuary practical light",typeof(Light));innerLight.transform.SetParent(t,false);innerLight.transform.localPosition=new Vector3(0,2.05f,-depth*.12f);
                var shrineLight=innerLight.GetComponent<Light>();shrineLight.type=LightType.Point;shrineLight.color=new Color(1f,.47f,.14f);shrineLight.intensity=5.2f;shrineLight.range=8.2f;shrineLight.shadows=LightShadows.None;
                for(int side=-1;side<=1;side+=2) Lantern(t,new Vector3(side*2.8f,1,-depth*.45f),.85f,side);
            }
            Rail(t,w,depth);
            InkPanel(t,"Shrine ink drawing",0,new Vector3(0,.04f,-depth*.57f),w+1.4f,5.25f,.80f);
        }

        static void Hut(Transform parent,Vector3 pos,float scale,int side,int variant)
        {
            var t=SumiArt.Node("Repeated ink dwelling "+side+"-"+variant,parent,pos);t.localScale=Vector3.one*scale;
            t.localRotation=Quaternion.Euler(0,side<0?-88f:88f,Random.Range(-1.2f,1.2f));
            float w=Random.Range(4.6f,6.2f),depth=Random.Range(3.4f,4.5f);
            SumiArt.Box("Raised sill wash",t,new Vector3(0,.48f,0),new Vector3(w,.45f,depth),charcoal,true);
            SumiArt.Box("Paper facade",t,new Vector3(0,1.62f,-depth*.43f),new Vector3(w-.48f,1.9f,.16f),pale,true);
            for(int k=-2;k<=2;k++) SumiArt.Box("Facade frame",t,new Vector3(k*w*.19f,1.64f,-depth*.46f),new Vector3(k==0?.14f:.09f,2.08f,.18f),charcoal);
            for(int row=0;row<3;row++) SumiArt.Box("Facade crossbar",t,new Vector3(0,1.12f+row*.58f,-depth*.47f),new Vector3(w-.22f,.065f,.17f),charcoal);
            for(int s=-1;s<=1;s+=2) TaperedPost(t,new Vector3(s*w*.49f,1.62f,-depth*.45f),2.42f,.22f);
            SumiArt.Box("Side wall wash",t,new Vector3(0,1.58f,depth*.38f),new Vector3(w,2,.16f),pale,true);
            LayeredRoof(t,new Vector3(0,2.62f,0),w+1.45f,depth+1.25f,Random.Range(.78f,1.04f),variant);
            if(variant%3!=1)
            {
                var window=SumiArt.Box("Restrained amber window",t,new Vector3(side*.62f,1.58f,-depth*.555f),new Vector3(.76f,.66f,.018f),amber);
                SetNoShadows(window);
            }
            if(variant%3==0)
            {
                var banner=SumiArt.Box("Wind-worn noren",t,new Vector3(0,1.62f,-depth*.55f),new Vector3(1.55f,1.38f,.025f),pale).transform;
                Stroke(banner,new Vector3(-.25f,-.43f,-.51f),new Vector3(-.18f,.41f,-.51f),.014f,.66f);
                Stroke(banner,new Vector3(.19f,-.43f,-.51f),new Vector3(.24f,.32f,-.51f),.012f,.52f);
            }
            InkPanel(t,"Dwelling ink drawing",1,new Vector3(0,.03f,-depth*.57f),w+1.1f,3.9f,.62f+variant%3*.07f);
        }

        static void LayeredRoof(Transform parent,Vector3 pos,float width,float depth,float rise,int variant)
        {
            var roof=SumiArt.Shape("Swept layered roof wash",parent,SumiArt.Roof(),pos,new Vector3(width*.5f,rise,depth*.5f),roofWash).transform;
            roof.localRotation=Quaternion.Euler(0,0,Random.Range(-.35f,.35f));
            SumiArt.Box("Ridge brush mass",parent,pos+new Vector3(0,rise*.91f,0),new Vector3(.16f,.18f,depth*1.08f),charcoal);
            for(int i=-5;i<=5;i++)
            {
                if((i+variant)%4==0) continue;
                float u=Mathf.Abs(i)/5f;
                float y=pos.y+rise*(.87f*(1-u)*(1-u)+.08f*Mathf.Pow(u,6));
                SumiArt.Box("Broken roof ink",parent,new Vector3(i*width*.083f,y,pos.z-.03f),new Vector3(Random.Range(.025f,.05f),.028f,depth*Random.Range(.48f,.60f)),charcoal);
            }
            for(int side=-1;side<=1;side+=2)
            {
                var end=SumiArt.Box("Lifted eave",parent,pos+new Vector3(side*width*.49f,.08f,0),new Vector3(width*.12f,.14f,depth*1.08f),ink).transform;
                end.localRotation=Quaternion.Euler(0,0,side*9f);
            }
        }

        static void Rail(Transform t,float width,float depth)
        {
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<4;i++) SumiArt.Box("Veranda baluster",t,new Vector3(side*(width*.18f+i*width*.095f),1.35f,-depth*.51f),new Vector3(.065f,.76f,.065f),charcoal);
                SumiArt.Box("Veranda rail",t,new Vector3(side*width*.32f,1.64f,-depth*.51f),new Vector3(width*.37f,.09f,.09f),charcoal);
            }
        }

        static void Torii(Transform parent,Vector3 pos,float width,float height)
        {
            var t=SumiArt.Node("Dimensional torii",parent,pos);
            for(int side=-1;side<=1;side+=2)
            {
                TaperedPost(t,new Vector3(side*width*.35f,height*.45f,0),height*.9f,.34f);
                SumiArt.Shape("Stone torii foot",t,IrregularSlab(130+side),new Vector3(side*width*.35f,.14f,0),new Vector3(.95f,.30f,.90f),stone);
            }
            SumiArt.Box("Torii secondary lintel",t,new Vector3(0,height*.72f,0),new Vector3(width,.24f,.34f),charcoal);
            SumiArt.Box("Torii crown",t,new Vector3(0,height*.94f,0),new Vector3(width*1.18f,.30f,.48f),ink);
            for(int side=-1;side<=1;side+=2)
            {
                var tip=SumiArt.Box("Raised crown tip",t,new Vector3(side*width*.60f,height*.99f,0),new Vector3(width*.16f,.28f,.49f),ink).transform;
                tip.localRotation=Quaternion.Euler(0,0,side*10f);
            }
            SumiArt.Box("Shadow tablet",t,new Vector3(0,height*.82f,-.26f),new Vector3(.63f,.68f,.10f),ink);
            InkPanel(t,"Torii calligraphy",2,new Vector3(0,.02f,-.31f),width*1.25f,height*1.08f,.72f);
        }

        static void Gate(Transform parent,Vector3 pos,int side,float scale)
        {
            var t=SumiArt.Node("Village side gate",parent,pos);t.localScale=Vector3.one*scale;t.localRotation=Quaternion.Euler(0,side<0?-90:90,0);
            for(int s=-1;s<=1;s+=2) TaperedPost(t,new Vector3(s*1.35f,1.25f,0),2.5f,.18f);
            LayeredRoof(t,new Vector3(0,2.28f,0),3.9f,1.25f,.46f,side);
            SumiArt.Box("Gate cross beam",t,new Vector3(0,1.95f,0),new Vector3(3.25f,.18f,.22f),charcoal);
        }

        static void Lantern(Transform parent,Vector3 pos,float scale,int variant)
        {
            var t=SumiArt.Node("Weathered stone lantern",parent,pos);t.localScale=Vector3.one*scale;t.localRotation=Quaternion.Euler(0,variant*17f%360,Random.Range(-1.2f,1.2f));
            SumiArt.Shape("Irregular lantern foundation",t,IrregularSlab(170+variant),new Vector3(0,.10f,0),new Vector3(.92f,.22f,.88f),stone,true);
            SumiArt.Shape("Tapered lantern stem",t,SumiArt.Lathe("LanternStem",new[]{.24f,.19f,.15f,.18f},new[]{-.5f,-.42f,.42f,.5f},7),new Vector3(0,.76f,0),new Vector3(1,1.1f,1),stone);
            SumiArt.Box("Lantern chamber shadow",t,new Vector3(0,1.46f,0),new Vector3(.62f,.52f,.60f),ink);
            for(int s=-1;s<=1;s+=2)
            {
                SumiArt.Box("Lantern paper slit",t,new Vector3(s*.315f,1.46f,0),new Vector3(.018f,.25f,.29f),pale);
                SetNoShadows(SumiArt.Box("Amber lantern core",t,new Vector3(s*.326f,1.46f,0),new Vector3(.014f,.205f,.23f),amber));
                SetNoShadows(SumiArt.Box("Amber lantern core",t,new Vector3(0,1.46f,s*.316f),new Vector3(.23f,.205f,.014f),amber));
            }
            SumiArt.Shape("Lantern rain cap",t,SumiArt.Lathe("LanternCap",new[]{0f,.72f,.58f,.20f,0f},new[]{-.15f,-.13f,0,.26f,.29f},8),new Vector3(0,1.82f,0),Vector3.one,stone);
            SumiArt.Shape("Lantern finial",t,SumiArt.Lathe("LanternFinial",new[]{.18f,.12f,.03f},new[]{0,.22f,.48f},7),new Vector3(0,2.03f,0),Vector3.one,stone);
            InkPanel(t,"Lantern ink drawing",3,new Vector3(0,.02f,-.38f),2.05f,2.2f,.58f);
            var glow=new GameObject("Warm practical light",typeof(Light));glow.transform.SetParent(t,false);glow.transform.localPosition=new Vector3(0,1.48f,0);
            var point=glow.GetComponent<Light>();point.type=LightType.Point;point.color=new Color(1f,.52f,.18f);point.intensity=4.2f;point.range=5.4f;point.shadows=LightShadows.None;point.renderMode=LightRenderMode.Auto;
            var pool=SumiArt.Box("Soft lantern illumination pool",parent,pos+new Vector3(0,.014f,0),new Vector3(5.2f,.012f,5.2f),lightPool);SetNoShadows(pool);
            WetReflection(parent,pos+new Vector3(0,.018f,-2.35f),new Vector3(.82f,.018f,5.4f),variant);
        }

        static void BuildGroundComposition()
        {
            var dressing=SumiArt.Node("Ground detail — clustered and readable",root,Vector3.zero);
            // A broken route leads toward the shrine without becoming a hard gameplay lane.
            for(int i=0;i<15;i++)
            {
                float z=-8f+i*2.45f;float x=Mathf.Sin(i*1.77f)*1.05f+Random.Range(-.22f,.22f);
                var slab=SumiArt.Shape("Weathered approach stone",dressing,IrregularSlab(310+i),new Vector3(x,-.012f,z),new Vector3(Random.Range(1.55f,2.55f),.105f,Random.Range(.72f,1.25f)),i%4==0?stone:charcoal);
                slab.transform.localRotation=Quaternion.Euler(0,Random.Range(-13f,13f),0);
            }
            // Low-frequency damp islands create value and roughness variation while keeping the centre calm.
            for(int i=0;i<18;i++)
            {
                float a=Random.Range(0,Mathf.PI*2),r=Random.Range(i<5?5.5f:10f,25f);Vector3 p=new Vector3(Mathf.Sin(a)*r,-.005f,Mathf.Cos(a)*r);
                var patch=SumiArt.Shape("Shallow damp ink patch",dressing,IrregularSlab(400+i),p,new Vector3(Random.Range(2.4f,6.2f),.025f,Random.Range(1.5f,4.4f)),wet);
                patch.transform.localRotation=Quaternion.Euler(0,Random.Range(0,180),0);SetNoShadows(patch);
            }
            for(int i=0;i<22;i++)
            {
                float a=Random.Range(0,Mathf.PI*2),r=Random.Range(18.5f,30f);Rock(dressing,new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r),Random.Range(.45f,1.35f),500+i);
            }
            // Sparse reflected dashes complete the approach composition without becoming a painted road.
            for(int i=0;i<9;i++)
            {
                float z=6.5f+i*1.72f+Random.Range(-.18f,.18f),x=(i%2==0?-1f:1f)*Random.Range(.75f,2.35f);
                var glint=SumiArt.Shape("Gateward amber reflection",dressing,IrregularSlab(1160+i),new Vector3(x,.022f,z),new Vector3(Random.Range(.16f,.32f),.012f,Random.Range(.75f,1.65f)),reflection);
                glint.transform.localRotation=Quaternion.Euler(0,Random.Range(-7f,7f),0);SetNoShadows(glint);
            }
        }

        static void BuildLanternRhythm()
        {
            var practicals=SumiArt.Node("Lantern path — warm visual rhythm",root,Vector3.zero);
            Vector3[] positions={new Vector3(-11.8f,0,-12f),new Vector3(12.7f,0,-8f),new Vector3(-8.8f,0,-4.2f),new Vector3(9.4f,0,-1.2f),new Vector3(-13.2f,0,4.5f),new Vector3(12.2f,0,8.5f),new Vector3(-10.8f,0,18f),new Vector3(10.9f,0,20f)};
            for(int i=0;i<positions.Length;i++)
            {
                Lantern(practicals,positions[i],Random.Range(.84f,1.03f),40+i);
                Rock(practicals,positions[i]+new Vector3(i%2==0?1.1f:-1.05f,0,.38f),Random.Range(.55f,.92f),610+i);
                GrassCluster(practicals,positions[i]+new Vector3(i%2==0?-1.15f:1.05f,0,.25f),Random.Range(.78f,1.1f),700+i);
            }
        }

        static void BuildPerimeterDressing()
        {
            var perimeter=SumiArt.Node("Pale grass and ink perimeter",root,Vector3.zero);
            for(int i=0;i<30;i++)
            {
                float a=i*Mathf.PI*2/30f+Random.Range(-.055f,.055f),r=Random.Range(23f,30.5f);Vector3 p=new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);
                if(i%5==0)Pine(perimeter,p,Random.Range(.70f,1.04f));
                else if(i%3==0)BrushBush(perimeter,p,Random.Range(.68f,1.08f),i+90);
                else GrassCluster(perimeter,p,Random.Range(.72f,1.24f),800+i);
                if(i%4==1)Rock(perimeter,p+new Vector3(Random.Range(-1.1f,1.1f),0,Random.Range(-.8f,.8f)),Random.Range(.5f,1.2f),900+i);
            }
            // A few foreground edge clumps frame the camera without entering combat space.
            GrassCluster(perimeter,new Vector3(-16.5f,0,-9.5f),1.35f,970);
            GrassCluster(perimeter,new Vector3(17.2f,0,-7.4f),1.18f,971);
        }

        static void BuildDistantSettlement()
        {
            var village=SumiArt.Node("Mist-softened outer settlement",root,Vector3.zero);
            Vector3[] huts={new Vector3(-22f,0,37f),new Vector3(-13f,0,42f),new Vector3(14f,0,41f),new Vector3(23f,0,36f),new Vector3(-32f,0,29f),new Vector3(32f,0,28f)};
            for(int i=0;i<huts.Length;i++)Hut(village,huts[i],Random.Range(.46f,.68f),huts[i].x<0?-1:1,20+i);
            Gate(village,new Vector3(-17f,0,34f),-1,.66f);Gate(village,new Vector3(18f,0,35f),1,.61f);
            Shrine(village,new Vector3(30f,.5f,47f),.42f,false,32);
        }

        static void BuildMountainLayers()
        {
            var mountains=SumiArt.Node("Layered painted mountains",root,Vector3.zero);
            // Front, middle and ghost ridges overlap laterally and fade into the warm fog.
            for(int i=0;i<4;i++)Mountain(mountains,new Vector3(-36f+i*24f,-3f,52f),32f,Random.Range(13f,19f),100+i,.18f);
            for(int i=0;i<3;i++)Mountain(mountains,new Vector3(-32f+i*32f,-1f,68f),42f,Random.Range(18f,25f),110+i,.095f);
            for(int i=0;i<2;i++)Mountain(mountains,new Vector3(-25f+i*50f,2f,83f),54f,Random.Range(24f,31f),120+i,.045f);
        }

        static void GrassCluster(Transform parent,Vector3 pos,float scale,int seed)
        {
            var old=Random.state;Random.InitState(seed);var t=SumiArt.Node("Pale susuki cluster "+seed,parent,pos);t.localScale=Vector3.one*scale;t.localRotation=Quaternion.Euler(0,Random.Range(0,360f),0);
            int count=Random.Range(10,15);
            for(int i=0;i<count;i++)
            {
                Vector3 start=new Vector3(Random.Range(-.42f,.42f),.02f,Random.Range(-.28f,.28f));float h=Random.Range(.65f,1.55f);Vector3 bend=new Vector3(Random.Range(-.35f,.35f),h,Random.Range(-.28f,.28f));
                TaperedStroke(t,start,bend,.018f,grass);
                Vector3 plumeStart=Vector3.Lerp(start,bend,.72f),plumeEnd=bend+new Vector3(Random.Range(-.12f,.12f),.20f,Random.Range(-.08f,.08f));
                TaperedStroke(t,plumeStart,plumeEnd,.075f,grass);
            }
            foreach(var r in t.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;}
            Random.state=old;
        }

        static void Rock(Transform parent,Vector3 pos,float scale,int seed)
        {
            var go=SumiArt.Shape("Partially buried anchor rock",parent,IrregularSlab(seed),pos+new Vector3(0,-.02f,0),new Vector3(scale,scale*Random.Range(.38f,.72f),scale*Random.Range(.72f,1.22f)),stone);
            go.transform.localRotation=Quaternion.Euler(Random.Range(-7f,7f),Random.Range(0,180f),Random.Range(-5f,5f));
        }

        static void WetReflection(Transform parent,Vector3 pos,Vector3 scale,int seed)
        {
            for(int i=0;i<4;i++)
            {
                float length=scale.z*Random.Range(.45f,1.05f);Vector3 p=pos+new Vector3((i-1.5f)*.23f+Random.Range(-.08f,.08f),i*.006f,Random.Range(-.35f,.35f));
                var go=SumiArt.Shape("Broken amber ground reflection",parent,IrregularSlab(1000+seed+i),p,new Vector3(scale.x*Random.Range(.14f,.31f),scale.y,length),reflection);
                go.transform.localRotation=Quaternion.Euler(0,seed*37f%15f-7f,0);SetNoShadows(go);
            }
        }

        static void SetNoShadows(GameObject go)
        {
            var r=go.GetComponent<Renderer>();if(!r)return;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        static void BrushBush(Transform parent,Vector3 pos,float scale,int seed)
        {
            var t=SumiArt.Node("Dry-brush shrub "+seed,parent,pos);t.localScale=Vector3.one*scale;t.localRotation=Quaternion.Euler(0,Random.Range(0,360f),0);
            int count=7+Mathf.Abs(seed)%5;
            for(int i=0;i<count;i++)
            {
                float a=i/(float)count*Mathf.PI*2+Random.Range(-.22f,.22f),reach=Random.Range(.65f,1.25f);
                Vector3 start=new Vector3(Random.Range(-.12f,.12f),.05f,Random.Range(-.12f,.12f));
                Vector3 end=new Vector3(Mathf.Cos(a)*reach,Random.Range(.45f,1.12f),Mathf.Sin(a)*reach);
                TaperedStroke(t,start,end,Random.Range(.035f,.07f),i%3==0?charcoal:ink);
                if(i%2==0) TaperedStroke(t,Vector3.Lerp(start,end,.58f),end+new Vector3(-Mathf.Sin(a)*.34f,.18f,Mathf.Cos(a)*.34f),.025f,charcoal);
            }
            InkPanel(t,"Shrub calligraphy",6,new Vector3(0,.01f,0),2.3f,1.75f,.48f);
        }

        static void Pine(Transform parent,Vector3 pos,float scale)
        {
            var t=SumiArt.Node("Sculptural ink pine",parent,pos);t.localScale=Vector3.one*scale;t.localRotation=Quaternion.Euler(0,Random.Range(0,360f),Random.Range(-2f,2f));
            Vector3 top=new Vector3(Random.Range(-.45f,.45f),6.2f,Random.Range(-.3f,.3f));TaperedStroke(t,Vector3.zero,top,.30f,charcoal);
            for(int i=0;i<7;i++)
            {
                float side=i%2==0?-1:1;Vector3 start=Vector3.Lerp(Vector3.zero,top,.34f+i*.075f);
                Vector3 end=start+new Vector3(side*Random.Range(1.4f,3f),Random.Range(.15f,.75f),Random.Range(-1f,1f));TaperedStroke(t,start,end,.10f,charcoal);
                for(int j=0;j<4;j++) TaperedStroke(t,Vector3.Lerp(start,end,Random.Range(.55f,.95f)),end+new Vector3(side*Random.Range(.15f,.8f),Random.Range(-.08f,.36f),Random.Range(-.65f,.65f)),.035f,ink);
            }
            InkPanel(t,"Pine brush drawing",Random.value>.35f?4:5,new Vector3(0,.01f,0),7.4f,7.0f,.46f);
        }

        static void Mountain(Vector3 pos,float width,float height,int variant)
        {
            InkPanel(root,"Distant mountain wash "+variant,7,pos,width,height,.13f+variant*.012f);
        }

        static void Mountain(Transform parent,Vector3 pos,float width,float height,int variant,float opacity)
        {
            InkPanel(parent,"Painted mountain layer "+variant,7,pos,width,height,opacity);
        }

        static Mesh IrregularSlab(int seed)
        {
            string key="IrregularSlab"+(Mathf.Abs(seed)%9);if(SumiArt.Meshes.TryGetValue(key,out var found)&&found)return found;
            var old=Random.state;Random.InitState(8000+Mathf.Abs(seed)%9);const int n=8;var v=new Vector3[n*2];var sideTri=new int[n*6];
            for(int i=0;i<n;i++)
            {
                float a=Mathf.PI*2*i/n,r=Random.Range(.42f,.58f),x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;v[i]=new Vector3(x,.5f,z);v[i+n]=new Vector3(x*1.03f,-.5f,z*1.03f);
                int next=(i+1)%n,k=i*6;sideTri[k]=i;sideTri[k+1]=next;sideTri[k+2]=i+n;sideTri[k+3]=next;sideTri[k+4]=next+n;sideTri[k+5]=i+n;
            }
            var all=new int[sideTri.Length+(n-2)*6];sideTri.CopyTo(all,0);int offset=sideTri.Length;
            for(int i=0;i<n-2;i++){all[offset+i*3]=0;all[offset+i*3+1]=i+2;all[offset+i*3+2]=i+1;int q=offset+(n-2)*3+i*3;all[q]=n;all[q+1]=n+i+1;all[q+2]=n+i+2;}
            var mesh=new Mesh{name=key,vertices=v,triangles=all};mesh.RecalculateNormals();mesh.RecalculateBounds();SumiArt.Meshes[key]=mesh;Random.state=old;return mesh;
        }

        static void TaperedPost(Transform parent,Vector3 pos,float height,float width)
        {
            var mesh=SumiArt.Lathe("WeatheredSquarePost",new[]{.54f,.50f,.46f,.52f},new[]{-.5f,-.38f,.38f,.5f},4);
            var go=SumiArt.Shape("Hand-hewn timber post",parent,mesh,pos,new Vector3(width,height,width),charcoal,true);
            go.transform.localRotation=Quaternion.Euler(0,Random.Range(-3f,3f),Random.Range(-.65f,.65f));
        }

        static void TaperedStroke(Transform parent,Vector3 a,Vector3 b,float width,Material material)
        {
            var mesh=SumiArt.Lathe("TaperedBrush",new[]{.62f,.54f,.16f,0f},new[]{-.5f,-.36f,.39f,.5f},5);
            var t=SumiArt.Shape("Tapered brush branch",parent,mesh,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),material).transform;t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }

        static void Stroke(Transform parent,Vector3 a,Vector3 b,float width,float strength)
        {
            var go=new GameObject("Loose ink stroke");go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;
            line.sharedMaterial=SumiArt.Mat("GroundInk",new Color(.18f,.18f,.17f),true);line.startColor=line.endColor=Color.Lerp(new Color(.82f,.82f,.79f),new Color(.04f,.04f,.038f),strength);
            line.startWidth=width;line.endWidth=width*.18f;line.positionCount=4;
            for(int i=0;i<4;i++) line.SetPosition(i,Vector3.Lerp(a,b,i/3f)+new Vector3(0,Random.Range(-.006f,.006f),Random.Range(-.025f,.025f)));
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        }

        static void Boundary(Vector3 pos,Vector3 scale){var wall=SumiArt.Box("Invisible courtyard boundary",root,pos,scale,ink,true);wall.GetComponent<Renderer>().enabled=false;}

        static void EncounterAnchors()
        {
            Anchor("PlayerArenaAnchor",new Vector3(0,0,-3));
            Anchor("NorthSpawn",new Vector3(0,0,18.5f)); Anchor("EastSpawn",new Vector3(18.5f,0,0));
            Anchor("SouthSpawn",new Vector3(0,0,-18.5f)); Anchor("WestSpawn",new Vector3(-18.5f,0,0));
            Anchor("BossSpawn",new Vector3(0,0,17.5f));
        }
        static void Anchor(string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=position;}

        static void SetInk(Material material,Color pigment,float porosity){if(!material)return;if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",pigment);if(material.HasProperty("_Porosity"))material.SetFloat("_Porosity",porosity);}

        static SumiDrawing InkPanel(Transform parent,string name,int frame,Vector3 localPosition,float width,float height,float opacity)
        {
            var go=new GameObject(name,typeof(SumiDrawing));go.transform.SetParent(parent,false);go.transform.localPosition=localPosition;
            var drawing=go.GetComponent<SumiDrawing>();drawing.Setup(atlas,frame,width,height);drawing.billboard=false;drawing.wind=frame>=4&&frame<=6?.16f:0;drawing.strength=opacity;drawing.Apply();return drawing;
        }

        static void Atmosphere()
        {
            var lightObject=new GameObject("Low dusk key light");lightObject.transform.SetParent(root);lightObject.transform.rotation=Quaternion.Euler(36,-28,0);
            var sun=lightObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(.78f,.50f,.34f);sun.intensity=.82f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;RenderSettings.sun=sun;
            RenderSettings.skybox=Resources.Load<Material>("Sumi/Evening Gradient Sky");RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0155f;RenderSettings.fogColor=new Color(.25f,.105f,.052f);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.17f,.115f,.085f);
            var volumeObject=new GameObject("Burnt-orange night grade",typeof(Volume));volumeObject.transform.SetParent(root,false);var volume=volumeObject.GetComponent<Volume>();volume.isGlobal=true;volume.priority=0;volume.sharedProfile=Resources.Load<VolumeProfile>("Sumi/Atmosphere");
        }
    }
}
