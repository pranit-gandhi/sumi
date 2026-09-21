using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sumi
{
    public enum SumiVolleyState { Telegraph,Falling,Fade }

    // One warned circle, then a staggered fall of decorative shafts. Damage is a single occupancy
    // test against that circle between the first and last landing, not a per-arrow hitbox.
    public sealed class SumiArrowStrike:MonoBehaviour
    {
        struct Shaft
        {
            public Vector3 spawn,land,normal,incoming;
            public float landAt,roll;
            public bool stuck;
        }

        public SumiVolleyState State { get; private set; }=SumiVolleyState.Telegraph;
        public IReadOnlyList<Vector3> PredictedGameplayLandings=>landings;

        SumiPlayer player;SumiRunDirector run;SumiArrowVolleyTuning tuning;
        readonly List<Shaft> shafts=new List<Shaft>(32);
        readonly List<Vector3> landings=new List<Vector3>(32);
        LineRenderer ring;MeshRenderer fillRenderer;Mesh discMesh,arrowMesh;Material ringMaterial,fillMaterial,arrowMaterial;
        Matrix4x4[] arrowMatrices;
        Vector3 center;float radius,age,fill,fade=1;bool damaged;
        AudioSource rainVoice;bool rainStarted,voicesPaused;
        const int RingPoints=64;

        public void Init(SumiPlayer p,SumiRunDirector r)
        {
            player=p;run=r;tuning=p&&p.config&&p.config.arrowVolley!=null?p.config.arrowVolley:new SumiArrowVolleyTuning();
            radius=tuning.targetRadius;
            Vector3 origin=p.transform.position;origin.y=0;
            Vector3 offset=p.velocity;offset.y=0;
            if(offset.sqrMagnitude<.04f)offset=Quaternion.Euler(0,Random.Range(0,360),0)*Vector3.forward;
            center=Ground(origin+offset.normalized*tuning.centerOffset);
            BuildMarkings();BuildShafts();BuildVoices();
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.LiftForVolley(true);
        }

        void BuildMarkings()
        {
            Color ink=tuning.WarningColor;
            ringMaterial=VolleyInk(ink,3005);fillMaterial=VolleyInk(new Color(ink.r,ink.g,ink.b,.72f),3000);
            ring=gameObject.AddComponent<LineRenderer>();ring.useWorldSpace=true;ring.loop=true;ring.positionCount=RingPoints;
            ring.sharedMaterial=ringMaterial;ring.shadowCastingMode=ShadowCastingMode.Off;ring.textureMode=LineTextureMode.Stretch;
            ring.startWidth=ring.endWidth=.07f;ring.numCornerVertices=2;
            for(int i=0;i<RingPoints;i++)
            {
                float a=i*Mathf.PI*2/RingPoints;
                ring.SetPosition(i,Ground(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius)+Vector3.up*.05f);
            }
            discMesh=BuildDisc();
            var fillGo=new GameObject("Volley fill");fillGo.transform.SetParent(transform,false);
            fillGo.AddComponent<MeshFilter>().sharedMesh=discMesh;
            fillRenderer=fillGo.AddComponent<MeshRenderer>();
            fillRenderer.sharedMaterial=fillMaterial;fillRenderer.shadowCastingMode=ShadowCastingMode.Off;
            fillRenderer.receiveShadows=false;fillRenderer.enabled=false;
            arrowMesh=BuildArrowMesh();arrowMaterial=VolleyInk(new Color(.03f,.03f,.032f,1),3200);
            arrowMaterial.enableInstancing=true;arrowMatrices=new Matrix4x4[Mathf.Max(8,tuning.arrowCount)];
        }

        void BuildShafts()
        {
            int count=Mathf.Max(2,tuning.arrowCount);float first=tuning.telegraphDuration+tuning.fallDuration;
            float last=first+tuning.fallStagger;
            for(int i=0;i<count;i++)
            {
                Vector2 disc=Random.insideUnitCircle*radius*.92f;
                Vector3 land=Ground(center+new Vector3(disc.x,0,disc.y));
                Vector3 slant=Quaternion.Euler(0,Random.Range(0,360),0)*Vector3.forward*Random.Range(1.6f,3.4f);
                Vector3 spawn=land+Vector3.up*tuning.spawnHeight+slant;
                Vector3 incoming=(land-spawn).normalized;
                landings.Add(land);
                shafts.Add(new Shaft{spawn=spawn,land=land,normal=Vector3.up,incoming=incoming,landAt=Mathf.Lerp(first,last,i/(count-1f)),roll=Random.Range(-28f,28f)});
            }
        }

        static AudioSource Voice(GameObject host,float volume)
        {
            var source=host.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=0;source.loop=false;source.volume=volume;
            return source;
        }

        void BuildVoices()
        {
            rainVoice=Voice(gameObject,.55f);
            rainVoice.clip=Resources.Load<AudioClip>("Sumi/Audio/VolleyArrows");
        }

        void PauseVoices(bool pause)
        {
            if(pause==voicesPaused)return;voicesPaused=pause;
            if(!rainVoice)return;
            if(pause)rainVoice.Pause();else rainVoice.UnPause();
        }

        void TickAudio(float spawn,float last)
        {
            if(!rainVoice||!rainVoice.clip)return;
            bool window=age>=spawn&&age<=last;
            if(!rainStarted&&window){rainVoice.Play();rainStarted=true;}
            if(rainStarted&&rainVoice.isPlaying&&!window)rainVoice.Stop();
        }

        void Update()
        {
            if(run&&run.Paused){PauseVoices(true);return;}
            PauseVoices(false);
            if(!run||!run.CombatActive){Destroy(gameObject);return;}
            if(Time.timeScale<=0)return;
            age+=Time.deltaTime;
            float telegraph=tuning.telegraphDuration,first=telegraph+tuning.fallDuration,last=first+tuning.fallStagger;
            if(age<telegraph)State=SumiVolleyState.Telegraph;
            else if(age<last)State=SumiVolleyState.Falling;
            else State=SumiVolleyState.Fade;
            if(State!=SumiVolleyState.Telegraph&&SumiGame.I&&SumiGame.I.view)SumiGame.I.view.LiftForVolley(false);
            fill=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/telegraph));
            fade=State==SumiVolleyState.Fade?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(last,last+tuning.fadeDuration,age)):1;
            TickAudio(telegraph,last);
            DrawMarkings();DrawShafts();
            if(age>=first&&age<=last&&!damaged&&InsideCircle())Hit();
            if(age>=last+tuning.fadeDuration)Destroy(gameObject);
        }

        void DrawMarkings()
        {
            Color ink=tuning.WarningColor;
            Color ringTint=new Color(ink.r,ink.g,ink.b,fade);
            ringMaterial.SetColor("_BaseColor",ringTint);ringMaterial.color=ringTint;
            ring.startColor=ring.endColor=ringTint;
            ring.enabled=fade>.01f;ring.startWidth=.08f;ring.endWidth=.08f;
            bool showFill=fill>.01f&&fade>.01f;
            if(fillRenderer)fillRenderer.enabled=showFill;
            if(showFill)
            {
                Color fillTint=new Color(ink.r,ink.g,ink.b,.72f*fade);
                fillMaterial.SetColor("_BaseColor",fillTint);fillMaterial.color=fillTint;
                float span=radius*fill;
                fillRenderer.transform.SetPositionAndRotation(center+Vector3.up*.04f,Quaternion.identity);
                fillRenderer.transform.localScale=new Vector3(span,1,span);
            }
        }

        void DrawShafts()
        {
            if(age<tuning.telegraphDuration||fade<=.01f)return;
            Color shaft=new Color(.03f,.03f,.032f,fade);
            arrowMaterial.SetColor("_BaseColor",shaft);arrowMaterial.color=shaft;
            int drawn=0;
            for(int i=0;i<shafts.Count;i++)
            {
                Shaft s=shafts[i];
                float start=s.landAt-tuning.fallDuration;
                if(age<start)continue;
                float u=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-start)/tuning.fallDuration));
                Vector3 rest=s.land+Vector3.up*.07f;
                Vector3 position=Vector3.Lerp(s.spawn,rest,u);
                Vector3 dir=s.incoming;
                if(u>=.999f){position=rest;dir=Vector3.Slerp(-s.normal,s.incoming,.4f).normalized;s.stuck=true;shafts[i]=s;}
                Quaternion rotation=Quaternion.LookRotation(dir)*Quaternion.Euler(0,0,s.roll);
                arrowMatrices[drawn++]=Matrix4x4.TRS(position,rotation,Vector3.one);
                if(drawn==arrowMatrices.Length){Flush(drawn);drawn=0;}
            }
            Flush(drawn);
        }

        void Flush(int count)
        {
            if(count<=0)return;
            Graphics.DrawMeshInstanced(arrowMesh,0,arrowMaterial,arrowMatrices,count,null,ShadowCastingMode.On,true,gameObject.layer);
        }

        bool InsideCircle()
        {
            Vector3 d=player.transform.position-center;d.y=0;return d.sqrMagnitude<=radius*radius;
        }
        void Hit()
        {
            damaged=true;
            player.combat.ReceiveWorldHit(tuning.damage,center);
        }

        static Vector3 Ground(Vector3 point)
        {
            if(Physics.Raycast(point+Vector3.up*35f,Vector3.down,out var hit,80f,1<<8,QueryTriggerInteraction.Ignore))return hit.point;
            point.y=.025f;return point;
        }

        static Material VolleyInk(Color color,int queue)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",Texture2D.whiteTexture);
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            m.color=color;m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);
            m.SetOverrideTag("RenderType","Transparent");
            m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite",0);m.SetInt("_Cull",(int)CullMode.Off);
            if(m.HasProperty("_QueueControl"))m.SetFloat("_QueueControl",1);
            m.DisableKeyword("_ALPHATEST_ON");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue=queue;m.enableInstancing=true;return m;
        }

        static Mesh BuildDisc()
        {
            if(SumiArt.Meshes.TryGetValue("VolleyFillDisc",out var cached)&&cached)return cached;
            const int n=48;var v=new Vector3[n+1];var uv=new Vector2[n+1];var t=new int[n*6];
            v[0]=Vector3.zero;uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n;v[i+1]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                uv[i+1]=new Vector2(Mathf.Cos(a)*.5f+.5f,Mathf.Sin(a)*.5f+.5f);
                int next=i+1==n?1:i+2,k=i*6;
                t[k]=0;t[k+1]=next;t[k+2]=i+1;
                t[k+3]=0;t[k+4]=i+1;t[k+5]=next;
            }
            var mesh=new Mesh{name="VolleyFillDisc"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();SumiArt.Meshes[mesh.name]=mesh;return mesh;
        }

        // Shaft, three vanes and a pyramidal head, laid along +Z so LookRotation aims the tip.
        static Mesh BuildArrowMesh()
        {
            if(SumiArt.Meshes.TryGetValue("WarnedVolleyArrow",out var cached)&&cached)return cached;
            var v=new List<Vector3>();var tri=new List<int>();
            void Cap(float z,float r,int seg)
            {
                for(int i=0;i<seg;i++){float a=i*Mathf.PI*2/seg;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z));}
            }
            void Tube(int a,int b,int seg)
            {
                for(int i=0;i<seg;i++){int i1=(i+1)%seg;tri.Add(a+i);tri.Add(b+i);tri.Add(a+i1);tri.Add(a+i1);tri.Add(b+i);tri.Add(b+i1);}
            }
            const int s=8;int nock=v.Count;Cap(-.52f,.01f,s);int tail=v.Count;Cap(-.46f,.016f,s);int mid=v.Count;Cap(.18f,.015f,s);int neck=v.Count;Cap(.26f,.038f,s);
            Tube(nock,tail,s);Tube(tail,mid,s);Tube(mid,neck,s);
            int tip=v.Count;v.Add(new Vector3(0,0,.58f));
            for(int i=0;i<s;i++){tri.Add(neck+i);tri.Add(neck+(i+1)%s);tri.Add(tip);}
            for(int i=0;i<3;i++)
            {
                float a=i*120*Mathf.Deg2Rad;Vector3 n=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                int b=v.Count;
                v.Add(-n*.01f+new Vector3(0,0,-.44f));v.Add(n*.055f+new Vector3(0,0,-.40f));v.Add(n*.012f+new Vector3(0,0,-.08f));v.Add(-n*.01f+new Vector3(0,0,-.12f));
                tri.Add(b);tri.Add(b+1);tri.Add(b+2);tri.Add(b);tri.Add(b+2);tri.Add(b+3);
                tri.Add(b);tri.Add(b+2);tri.Add(b+1);tri.Add(b);tri.Add(b+3);tri.Add(b+2);
            }
            var mesh=new Mesh{name="WarnedVolleyArrow"};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();SumiArt.Meshes[mesh.name]=mesh;return mesh;
        }

        void OnDestroy()
        {
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.LiftForVolley(false);
            if(rainVoice)rainVoice.Stop();
            if(ringMaterial)Destroy(ringMaterial);if(fillMaterial)Destroy(fillMaterial);if(arrowMaterial)Destroy(arrowMaterial);
        }
    }
}
