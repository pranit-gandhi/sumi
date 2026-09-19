using UnityEngine;
using UnityEngine.Rendering;

namespace Sumi
{
    // Dimensional ink architecture. Flat drawings are reserved for the distant horizon.
    public static class SumiSketchWorld
    {
        static Transform root;
        static Material ink, charcoal, pale, stone, distant, roofWash;
        static Texture2D atlas;

        public static Transform Build()
        {
            var oldRandom=Random.state; Random.InitState(2209);
            root=new GameObject("Sumi — dimensional ink village").transform;
            atlas=Resources.Load<Texture2D>("Drawings/ShrineAtlas");
            ink=SumiArt.Mat("Ink",new Color(.055f,.057f,.055f));
            charcoal=SumiArt.Mat("CharcoalWood",new Color(.17f,.17f,.16f));
            pale=SumiArt.Mat("Paper",new Color(.78f,.775f,.735f));
            stone=SumiArt.Mat("Stone",new Color(.55f,.56f,.53f));
            distant=SumiArt.Mat("DistantInk",new Color(.56f,.57f,.55f));
            roofWash=SumiArt.Mat("RoofWash",new Color(.36f,.36f,.34f));
            SetInk(ink,new Color(.07f,.071f,.067f),.10f);SetInk(charcoal,new Color(.25f,.25f,.235f),.19f);
            SetInk(pale,new Color(.84f,.835f,.80f),.07f);SetInk(stone,new Color(.66f,.665f,.63f),.15f);
            SetInk(distant,new Color(.75f,.75f,.72f),.29f);SetInk(roofWash,new Color(.58f,.58f,.55f),.27f);

            // One generous brush-painted courtyard. The middle stays empty and readable while
            // four gates, dwellings and mountain washes imply a settlement continuing forever.
            SumiArt.Box("Unbroken paper arena",root,new Vector3(0,-.18f,0),new Vector3(74,.35f,74),SumiArt.Mat("BarePaper",new Color(.88f,.88f,.855f),true),true);
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
            for(int i=0;i<20;i++){float a=i*Mathf.PI*2/20f+Random.Range(-.08f,.08f),r=Random.Range(23.5f,29f);Vector3 p=new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);if(i%4==0)Pine(root,p,Random.Range(.75f,1.12f));else BrushBush(root,p,Random.Range(.72f,1.2f),i);}
            for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12f;Mountain(new Vector3(Mathf.Sin(a)*52f,-1,Mathf.Cos(a)*52f),Random.Range(20f,31f),Random.Range(13f,23f),i);}
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
            for(int s=-1;s<=1;s+=2) SumiArt.Box("Lantern paper slit",t,new Vector3(s*.315f,1.46f,0),new Vector3(.018f,.25f,.29f),pale);
            SumiArt.Shape("Lantern rain cap",t,SumiArt.Lathe("LanternCap",new[]{0f,.72f,.58f,.20f,0f},new[]{-.15f,-.13f,0,.26f,.29f},8),new Vector3(0,1.82f,0),Vector3.one,stone);
            SumiArt.Shape("Lantern finial",t,SumiArt.Lathe("LanternFinial",new[]{.18f,.12f,.03f},new[]{0,.22f,.48f},7),new Vector3(0,2.03f,0),Vector3.one,stone);
            InkPanel(t,"Lantern ink drawing",3,new Vector3(0,.02f,-.38f),2.05f,2.2f,.58f);
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
            var lightObject=new GameObject("Soft paper moonlight");lightObject.transform.SetParent(root);lightObject.transform.rotation=Quaternion.Euler(47,-32,0);
            var sun=lightObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(.91f,.92f,.90f);sun.intensity=1.08f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.52f;RenderSettings.sun=sun;
            RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0105f;RenderSettings.fogColor=new Color(.88f,.875f,.845f);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.82f,.82f,.79f);
        }
    }
}
