using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sumi
{
    public enum SumiVolleyState { VolleyRequested,Telegraph,Launch,Ascending,Apex,Descending,Impact,Recovery }

    // A whole barrage is advanced here. Individual arrows are data, not behaviours, rigidbodies,
    // colliders or coroutines. The expensive gameplay subset and the decorative mass share the
    // exact same deterministic flight model, but only gameplay arrows can apply damage.
    public sealed class SumiArrowStrike:MonoBehaviour
    {
        sealed class Flight
        {
            public Vector3 start,target,targetNormal;
            public float launchAt,ascent,apex,descent,height,side,roll;
            public bool gameplay,impacted,nearMissPlayed;
            public float impactAt;
        }

        SumiPlayer player;SumiRunDirector run;SumiArrowVolleyTuning tuning;
        readonly List<Flight> flights=new List<Flight>(256);
        readonly List<Vector3> gameplayTargets=new List<Vector3>(64);
        LineRenderer coverage;Transform focusTarget;AudioSource releaseSource,highSource,descentSource,impactSource;
        Mesh arrowMesh,markerMesh;Material arrowMaterial,markerMaterial;
        Matrix4x4[] arrowMatrices,markerMatrices;
        Vector3 targetCenter,launchOrigin,travelDirection,travelRight;
        float age,nextImpactAudioAt,nextImpactKickAt,pendingImpactKick;
        int seed;

        public SumiVolleyState State { get; private set; }=SumiVolleyState.VolleyRequested;
        public IReadOnlyList<Vector3> PredictedGameplayLandings=>gameplayTargets;

        public void Init(SumiPlayer p,SumiRunDirector r)
        {
            player=p;run=r;tuning=p&&p.config&&p.config.arrowVolley!=null?p.config.arrowVolley:new SumiArrowVolleyTuning();
            seed=unchecked(Environment.TickCount*397^GetInstanceID());
            var random=new System.Random(seed);
            travelDirection=tuning.volleyDirection;travelDirection.y=0;
            if(travelDirection.sqrMagnitude<.001f)travelDirection=Vector3.forward;
            travelDirection.Normalize();travelRight=Vector3.Cross(Vector3.up,travelDirection).normalized;
            targetCenter=GroundPoint(p.transform.position+p.velocity*tuning.targetLead+tuning.targetCenterOffset,out _);
            launchOrigin=targetCenter-travelDirection*tuning.launchDistance+Vector3.up*tuning.launchHeight;
            arrowMesh=BuildArrowMesh();markerMesh=BuildAnnulusMesh();arrowMaterial=SumiArt.Black;markerMaterial=SumiArt.Crimson;
            arrowMaterial.enableInstancing=true;markerMaterial.enableInstancing=true;
            int total=Mathf.Max(1,tuning.waveCount*(tuning.gameplayArrowsPerWave+tuning.visualArrowsPerWave));
            int batch=Mathf.Clamp(tuning.instancingBatchSize,16,1023);
            arrowMatrices=new Matrix4x4[Mathf.Min(batch,total)];markerMatrices=new Matrix4x4[Mathf.Min(batch,Mathf.Max(1,tuning.gameplayArrowsPerWave*tuning.waveCount))];
            BuildFlights(random);BuildCoverage();BuildAudio();BuildFocus();State=SumiVolleyState.Telegraph;
        }

        void BuildFlights(System.Random random)
        {
            int clusters=Mathf.Max(1,tuning.clusterCount);var centers=new Vector2[clusters];
            for(int i=0;i<clusters;i++)centers[i]=Disc(random)*tuning.targetRadius*.72f;
            for(int wave=0;wave<tuning.waveCount;wave++)
            {
                AddWave(random,centers,wave,tuning.gameplayArrowsPerWave,true);
                AddWave(random,centers,wave,tuning.visualArrowsPerWave,false);
            }
        }

        void AddWave(System.Random random,Vector2[] clusters,int wave,int count,bool gameplay)
        {
            for(int i=0;i<count;i++)
            {
                Vector3 landing=ChooseLanding(random,clusters,gameplay);
                Vector3 normal;landing=GroundPoint(landing,out normal);
                if(gameplay)gameplayTargets.Add(landing);
                float waveStart=tuning.FirstLaunchAt+wave*(tuning.waveDuration+tuning.timeBetweenWaves);
                float within=count<=1?0:(float)i/(count-1)*tuning.waveDuration;
                within+=(NextSigned(random)*tuning.delayVariance);
                float formationX=NextSigned(random)*tuning.formationWidth*.5f;
                float formationZ=NextSigned(random)*tuning.formationDepth*.5f;
                Vector3 start=launchOrigin+travelRight*formationX+travelDirection*formationZ+Vector3.up*(NextSigned(random)*.45f);
                flights.Add(new Flight
                {
                    start=start,target=landing,targetNormal=normal,gameplay=gameplay,
                    launchAt=Mathf.Max(tuning.FirstLaunchAt,waveStart+within),
                    ascent=tuning.flightDuration*Mathf.Lerp(.91f,1.09f,Next01(random)),
                    apex=tuning.apexHold*Mathf.Lerp(.72f,1.25f,Next01(random)),
                    descent=tuning.descentDuration*Mathf.Lerp(.91f,1.10f,Next01(random)),
                    height=tuning.trajectoryHeight+NextSigned(random)*tuning.trajectoryHeightVariance,
                    side=NextSigned(random)*tuning.sideDrift,roll=NextSigned(random)*7f
                });
            }
        }

        Vector3 ChooseLanding(System.Random random,Vector2[] clusters,bool gameplay)
        {
            Vector2 offset=Vector2.zero;
            for(int attempt=0;attempt<12;attempt++)
            {
                bool clustered=Next01(random)<tuning.clusterStrength;
                offset=clustered?clusters[random.Next(clusters.Length)]+Gaussian2(random)*tuning.clusterRadius:Disc(random)*tuning.targetRadius;
                if(offset.magnitude>tuning.targetRadius)offset=offset.normalized*tuning.targetRadius;
                if(!gameplay||FarEnough(offset))break;
            }
            return targetCenter+travelRight*offset.x+travelDirection*offset.y;
        }

        bool FarEnough(Vector2 offset)
        {
            float spacing=tuning.minimumGameplaySpacing* tuning.minimumGameplaySpacing;
            Vector3 point=targetCenter+travelRight*offset.x+travelDirection*offset.y;
            for(int i=0;i<gameplayTargets.Count;i++){Vector3 d=gameplayTargets[i]-point;d.y=0;if(d.sqrMagnitude<spacing)return false;}
            return true;
        }

        void BuildCoverage()
        {
            coverage=gameObject.AddComponent<LineRenderer>();coverage.name="Predicted arrow coverage";coverage.useWorldSpace=true;coverage.loop=true;coverage.positionCount=65;
            coverage.sharedMaterial=markerMaterial;coverage.textureMode=LineTextureMode.Stretch;coverage.shadowCastingMode=ShadowCastingMode.Off;coverage.receiveShadows=false;
            for(int i=0;i<65;i++)
            {
                float a=i*Mathf.PI*2/64;float irregular=1f+.055f*Mathf.Sin(a*3f+seed*.001f)+.035f*Mathf.Sin(a*7f-seed*.002f);
                Vector3 p=targetCenter+(travelRight*Mathf.Cos(a)+travelDirection*Mathf.Sin(a))*tuning.targetRadius*irregular;
                coverage.SetPosition(i,GroundPoint(p,out _)+Vector3.up*.026f);
            }
            coverage.startWidth=coverage.endWidth=.006f;
        }

        void BuildAudio()
        {
            releaseSource=NewSource("Distant mass release",.55f);highSource=NewSource("High arrow hiss",.35f);descentSource=NewSource("Descending arrow whistle",.5f);impactSource=NewSource("Arrow impacts",.72f);
            releaseSource.transform.position=launchOrigin;if(tuning.distantRelease)releaseSource.PlayOneShot(tuning.distantRelease,tuning.volleyVolume);
            SetLoop(highSource,tuning.highWhistleLoop);SetLoop(descentSource,tuning.descentWhistleLoop);
        }

        AudioSource NewSource(string sourceName,float spatial)
        {
            var audioObject=new GameObject(sourceName);audioObject.transform.SetParent(transform,false);audioObject.transform.position=targetCenter;
            var source=audioObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=spatial;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=5;source.maxDistance=55;source.volume=0;return source;
        }
        static void SetLoop(AudioSource source,AudioClip clip){if(!clip)return;source.clip=clip;source.loop=true;source.Play();}

        void BuildFocus()
        {
            focusTarget=new GameObject("Volley focus target").transform;focusTarget.SetParent(transform,false);
            Vector3 centerStart=launchOrigin;focusTarget.position=Evaluate(centerStart,targetCenter,tuning.trajectoryHeight,0,.50f);
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.FrameVolley(focusTarget,tuning.attentionDuration,tuning.attentionStrength);
        }

        void Update()
        {
            if(run&&run.Paused)return;
            if(!run||!run.CombatActive){Destroy(gameObject);return;}
            if(Time.timeScale<=0)return;
            age+=Time.deltaTime;UpdateState();UpdateCoverage();DrawFlights();UpdateAudioAndCamera();
            if(age>tuning.TotalDuration)Destroy(gameObject);
        }

        void UpdateState()
        {
            float first=tuning.FirstLaunchAt;
            if(age<first)State=SumiVolleyState.Telegraph;
            else if(age<first+.16f)State=SumiVolleyState.Launch;
            else if(age<first+tuning.flightDuration)State=SumiVolleyState.Ascending;
            else if(age<first+tuning.flightDuration+tuning.apexHold)State=SumiVolleyState.Apex;
            else if(age<tuning.LastImpactAt-.08f)State=SumiVolleyState.Descending;
            else if(age<tuning.LastImpactAt+tuning.impactDuration)State=SumiVolleyState.Impact;
            else State=SumiVolleyState.Recovery;
        }

        void UpdateCoverage()
        {
            if(!coverage)return;
            float announce=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,tuning.telegraphDuration,age));
            float urgency=Mathf.SmoothStep(0,1,Mathf.InverseLerp(tuning.FirstLaunchAt,tuning.LastImpactAt,age));
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(tuning.LastImpactAt,tuning.LastImpactAt+tuning.impactDuration,age));
            float width=tuning.coverageLineWidth*Mathf.Lerp(.22f,1f,Mathf.Max(announce,urgency*tuning.coverageUrgency))*fade;
            coverage.startWidth=width;coverage.endWidth=width*.62f;coverage.enabled=fade>.01f;
        }

        void DrawFlights()
        {
            int arrowCount=0,markerCount=0;float descentIntensity=0;
            for(int i=0;i<flights.Count;i++)
            {
                Flight f=flights[i];float local=age-f.launchAt;
                if(local<0)continue;
                float duration=f.ascent+f.apex+f.descent;
                if(local>=duration)
                {
                    if(!f.impacted)Impact(f);
                    if(f.gameplay&&age-f.impactAt<tuning.impactDuration)AddMarker(f,1-Mathf.InverseLerp(0,tuning.impactDuration,age-f.impactAt),ref markerCount);
                    continue;
                }
                float u=FlightParameter(f,local);Vector3 position=Evaluate(f.start,f.target,f.height,f.side,u);Vector3 tangent=EvaluateTangent(f.start,f.target,f.height,f.side,u).normalized;
                Quaternion rotation=Quaternion.LookRotation(tangent)*Quaternion.Euler(0,0,f.roll);
                float width=tuning.arrowWidth*(f.gameplay?1.08f:1f);
                Vector3 scale=new Vector3(width/.035f,width/.035f,tuning.arrowLength/1.3f);
                AddArrow(Matrix4x4.TRS(position,rotation,scale),ref arrowCount);
                if(f.gameplay)
                {
                    float progress=local/duration;
                    if(progress>=tuning.exactMarkerReveal)AddMarker(f,Mathf.InverseLerp(tuning.exactMarkerReveal,1,progress),ref markerCount);
                    if(u>.55f)descentIntensity=Mathf.Max(descentIntensity,Mathf.InverseLerp(.55f,1,u));
                    CheckNearMiss(f,position,u);
                }
            }
            FlushArrows(arrowCount);FlushMarkers(markerCount);currentDescent=descentIntensity;
        }

        float currentDescent;
        void AddArrow(Matrix4x4 matrix,ref int count)
        {
            arrowMatrices[count++]=matrix;
            if(count==arrowMatrices.Length){DrawArrowBatch(count);count=0;}
        }
        void FlushArrows(int count){if(count>0)DrawArrowBatch(count);}
        void DrawArrowBatch(int count){Graphics.DrawMeshInstanced(arrowMesh,0,arrowMaterial,arrowMatrices,count,null,tuning.castArrowShadows?ShadowCastingMode.On:ShadowCastingMode.Off,true,gameObject.layer);}

        void AddMarker(Flight f,float strength,ref int count)
        {
            float pulse=.88f+Mathf.Sin((age+f.launchAt)*18f)*.12f;float radius=tuning.impactMarkerRadius*Mathf.Lerp(.55f,1.25f,Mathf.Clamp01(strength))*pulse;
            Quaternion slope=Quaternion.FromToRotation(Vector3.up,f.targetNormal);
            markerMatrices[count++]=Matrix4x4.TRS(f.target+f.targetNormal*.018f,slope,new Vector3(radius,1,radius));
            if(count==markerMatrices.Length){DrawMarkerBatch(count);count=0;}
        }
        void FlushMarkers(int count){if(count>0)DrawMarkerBatch(count);}
        void DrawMarkerBatch(int count){Graphics.DrawMeshInstanced(markerMesh,0,markerMaterial,markerMatrices,count,null,ShadowCastingMode.Off,false,gameObject.layer);}

        void CheckNearMiss(Flight f,Vector3 position,float u)
        {
            if(f.nearMissPlayed||u<.72f||position.y-player.transform.position.y>2.2f)return;
            Vector3 d=position-player.transform.position;d.y=0;
            if(d.magnitude>tuning.nearMissRadius)return;
            f.nearMissPlayed=true;
            float probability=Pseudo01(f.target.GetHashCode()^seed);
            if(probability<=tuning.nearMissChance&&tuning.nearMiss)impactSource.PlayOneShot(tuning.nearMiss,tuning.impactVolume*.8f);
        }

        void Impact(Flight f)
        {
            f.impacted=true;f.impactAt=age;
            if(f.gameplay)
            {
                Vector3 d=player.transform.position-f.target;d.y=0;
                if(d.magnitude<=tuning.damageRadius)player.combat.ReceiveWorldHit(tuning.damage,f.target);
            }
            Vector3 proximity=player.transform.position-f.target;proximity.y=0;
            if(proximity.magnitude<tuning.nearbyImpactRadius)
                pendingImpactKick+=Mathf.Lerp(.018f,.004f,proximity.magnitude/Mathf.Max(.01f,tuning.nearbyImpactRadius));
            if(tuning.impact&&Time.time>=nextImpactAudioAt){impactSource.transform.position=f.target;impactSource.PlayOneShot(tuning.impact,tuning.impactVolume);nextImpactAudioAt=Time.time+tuning.impactAudioInterval;}
            if(tuning.maxEmbeddedArrows>0&&Pseudo01(f.target.GetHashCode()+seed*3)<=tuning.embedChance)
            {
                Vector3 tangent=EvaluateTangent(f.start,f.target,f.height,f.side,1).normalized;
                SumiEmbeddedArrowField.Add(f.target,f.targetNormal,tangent,tuning,arrowMesh,arrowMaterial);
            }
        }

        void UpdateAudioAndCamera()
        {
            float ascent=Mathf.SmoothStep(0,1,Mathf.InverseLerp(tuning.FirstLaunchAt,tuning.FirstLaunchAt+tuning.flightDuration,age));
            highSource.volume=tuning.volleyVolume*.28f*ascent*(1-currentDescent);
            descentSource.volume=tuning.volleyVolume*.65f*Mathf.SmoothStep(0,1,currentDescent);
            if(pendingImpactKick>0&&Time.time>=nextImpactKickAt)
            {
                if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.Kick(Mathf.Min(tuning.maxImpactKick,pendingImpactKick));
                pendingImpactKick=0;nextImpactKickAt=Time.time+.12f;
            }
        }

        float FlightParameter(Flight f,float local)
        {
            if(local<f.ascent)return Mathf.Lerp(0,.46f,Mathf.SmoothStep(0,1,local/f.ascent));
            local-=f.ascent;if(local<f.apex)return Mathf.Lerp(.46f,.54f,f.apex<=.001f?1:local/f.apex);
            local-=f.apex;return Mathf.Lerp(.54f,1,Mathf.SmoothStep(0,1,local/f.descent));
        }

        static Vector3 Evaluate(Vector3 start,Vector3 end,float height,float side,float u)
        {
            Vector3 right=Vector3.Cross(Vector3.up,(end-start).normalized);right.y=0;if(right.sqrMagnitude>.001f)right.Normalize();
            return Vector3.Lerp(start,end,u)+Vector3.up*(4*height*u*(1-u))+right*(Mathf.Sin(Mathf.PI*u)*side);
        }
        static Vector3 EvaluateTangent(Vector3 start,Vector3 end,float height,float side,float u)
        {
            Vector3 right=Vector3.Cross(Vector3.up,(end-start).normalized);right.y=0;if(right.sqrMagnitude>.001f)right.Normalize();
            return end-start+Vector3.up*(4*height*(1-2*u))+right*(Mathf.PI*Mathf.Cos(Mathf.PI*u)*side);
        }

        static Vector3 GroundPoint(Vector3 point,out Vector3 normal)
        {
            if(Physics.Raycast(point+Vector3.up*35f,Vector3.down,out var hit,80f,1<<8,QueryTriggerInteraction.Ignore)){normal=hit.normal;return hit.point;}
            normal=Vector3.up;point.y=.025f;return point;
        }
        static float Next01(System.Random random)=>(float)random.NextDouble();
        static float NextSigned(System.Random random)=>Next01(random)*2-1;
        static Vector2 Disc(System.Random random){float a=Next01(random)*Mathf.PI*2,r=Mathf.Sqrt(Next01(random));return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);}
        static Vector2 Gaussian2(System.Random random)
        {
            float u=Mathf.Max(.0001f,Next01(random)),v=Next01(random);float r=Mathf.Sqrt(-2*Mathf.Log(u));float a=2*Mathf.PI*v;
            return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r)*.46f;
        }
        static float Pseudo01(int value){unchecked{uint x=(uint)value;x^=x<<13;x^=x>>17;x^=x<<5;return (x&0x00ffffff)/16777215f;}}

        static Mesh BuildArrowMesh()
        {
            if(SumiArt.Meshes.TryGetValue("VolleyArrow",out var cached)&&cached)return cached;
            float s=.026f;var v=new[]
            {
                new Vector3(-s,-s,-.65f),new Vector3(s,-s,-.65f),new Vector3(s,s,-.65f),new Vector3(-s,s,-.65f),
                new Vector3(-s,-s,.31f),new Vector3(s,-s,.31f),new Vector3(s,s,.31f),new Vector3(-s,s,.31f),
                new Vector3(-.08f,-.08f,.25f),new Vector3(.08f,-.08f,.25f),new Vector3(.08f,.08f,.25f),new Vector3(-.08f,.08f,.25f),new Vector3(0,0,.65f)
            };
            var t=new[]
            {
                0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7,0,3,2,0,2,1,
                8,9,12,9,10,12,10,11,12,11,8,12,8,11,10,8,10,9
            };
            var mesh=new Mesh{name="VolleyArrow"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();SumiArt.Meshes[mesh.name]=mesh;return mesh;
        }

        static Mesh BuildAnnulusMesh()
        {
            if(SumiArt.Meshes.TryGetValue("VolleyImpactRing",out var cached)&&cached)return cached;
            const int segments=20;var vertices=new Vector3[(segments+1)*2];var triangles=new int[segments*6];
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));vertices[i*2]=d;vertices[i*2+1]=d*.72f;}
            for(int i=0;i<segments;i++){int v=i*2,k=i*6;triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3;}
            var mesh=new Mesh{name="VolleyImpactRing"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();SumiArt.Meshes[mesh.name]=mesh;return mesh;
        }

        void OnDrawGizmosSelected()
        {
            if(!Application.isPlaying||tuning==null)return;
            Gizmos.color=new Color(.7f,.05f,.08f,.85f);Gizmos.DrawWireSphere(targetCenter,tuning.targetRadius);Gizmos.DrawLine(launchOrigin,targetCenter);
            int shown=Mathf.Min(12,flights.Count);for(int n=0;n<shown;n++){Flight f=flights[n];Vector3 last=f.start;for(int i=1;i<=24;i++){Vector3 p=Evaluate(f.start,f.target,f.height,f.side,i/24f);Gizmos.DrawLine(last,p);last=p;}}
            Gizmos.color=Color.yellow;for(int i=0;i<gameplayTargets.Count;i++)Gizmos.DrawRay(gameplayTargets[i],Vector3.up*.3f);
        }
    }

    // Embedded shafts outlive their barrage, but stay as a capped, centrally rendered array.
    // No landed arrow owns physics, a collider, a GameObject, or an Update method.
    sealed class SumiEmbeddedArrowField:MonoBehaviour
    {
        struct Entry { public Matrix4x4 matrix;public float expires; }
        static SumiEmbeddedArrowField instance;
        readonly List<Entry> entries=new List<Entry>(96);Mesh mesh;Material material;Matrix4x4[] matrices=new Matrix4x4[128];int capacity=72;

        public static void Add(Vector3 point,Vector3 normal,Vector3 incoming,SumiArrowVolleyTuning tuning,Mesh arrowMesh,Material arrowMaterial)
        {
            if(!instance){var go=new GameObject("Pooled embedded battlefield arrows");instance=go.AddComponent<SumiEmbeddedArrowField>();}
            instance.mesh=arrowMesh;instance.material=arrowMaterial;instance.capacity=Mathf.Max(0,tuning.maxEmbeddedArrows);
            while(instance.entries.Count>=instance.capacity&&instance.entries.Count>0)instance.entries.RemoveAt(0);
            if(instance.capacity==0)return;
            Vector3 direction=Vector3.Slerp(-normal,incoming.normalized,.34f).normalized;
            Quaternion rotation=Quaternion.LookRotation(direction);float width=tuning.arrowWidth;
            Vector3 center=point-direction*(tuning.arrowLength*.48f);
            instance.entries.Add(new Entry{matrix=Matrix4x4.TRS(center,rotation,new Vector3(width/.035f,width/.035f,tuning.arrowLength/1.3f)),expires=Time.time+tuning.embeddedArrowLifetime});
        }

        void Update()
        {
            for(int i=entries.Count-1;i>=0;i--)if(Time.time>=entries[i].expires)entries.RemoveAt(i);
            if(!mesh||!material||entries.Count==0)return;
            if(matrices.Length<Mathf.Min(1023,entries.Count))matrices=new Matrix4x4[Mathf.Min(1023,Mathf.NextPowerOfTwo(entries.Count))];
            int offset=0;while(offset<entries.Count){int count=Mathf.Min(matrices.Length,entries.Count-offset);for(int i=0;i<count;i++)matrices[i]=entries[offset+i].matrix;Graphics.DrawMeshInstanced(mesh,0,material,matrices,count,null,ShadowCastingMode.On,true,gameObject.layer);offset+=count;}
        }

        void OnDestroy(){if(instance==this)instance=null;}
    }
}
