using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
namespace Sumi
{
    public class SumiGame : MonoBehaviour
    {
        public static SumiGame I; public SumiConfig config;public SumiPlayer player; public SumiCamera view;public SumiRunDirector run;
        AudioSource music;bool musicPaused;
        void Awake()
        {
            I=this;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            if(!config)config=Resources.Load<SumiConfig>("Sumi/Combat");
            var p=new GameObject("Ronin");p.transform.position=new Vector3(0,.10f,-3);player=p.AddComponent<SumiPlayer>();player.Init(config);
            var cam=Camera.main;if(!cam){cam=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cam.tag="MainCamera";}
            cam.backgroundColor=RenderSettings.fogColor;cam.clearFlags=CameraClearFlags.SolidColor;cam.fieldOfView=48;cam.nearClipPlane=.08f;cam.farClipPlane=95;
            var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            view=cam.gameObject.AddComponent<SumiCamera>();view.player=player;cam.transform.position=new Vector3(0,4,-11);
            // SumiCamera owns the gameplay transform. A second live camera driver caused feedback
            // and occasional close-up snaps when the hand-authored framing moved.
            view.virtualCamera=null;
            run=gameObject.AddComponent<SumiRunDirector>();run.Init(player);
            var track=Resources.Load<AudioClip>("Sumi/Audio/Samurai");
            if(track){music=gameObject.AddComponent<AudioSource>();music.clip=track;music.loop=true;music.playOnAwake=false;music.spatialBlend=0;music.volume=.22f;music.Play();}
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        // Acquisition stays tighter than the range a held lock survives, so a foe drifting around
        // the edge cannot make the lock flicker on and off.
        const float LockRange=13f,LockDrop=16f;
        void Update()
        {
            SumiCombatFeedback.Tick();
            if(music&&run.Paused!=musicPaused){musicPaused=run.Paused;if(musicPaused)music.Pause();else music.UnPause();}
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(keyboard?.qKey.wasPressedThisFrame==true){if(player.locked)ReleaseLock();else SelectTarget(0);}
            var mouse=UnityEngine.InputSystem.Mouse.current;
            if(player.locked&&mouse!=null){float wheel=mouse.scroll.ReadValue().y;if(Mathf.Abs(wheel)>.01f)SelectTarget(wheel>0?1:-1);}
            if(player.locked)MaintainLock();
            if(keyboard?.escapeKey.wasPressedThisFrame==true)run.TogglePause();
        }
        void ReleaseLock(){player.locked=false;player.target=null;}
        // A lock that outlives its target, or clings to something across the courtyard, reads as a
        // bug. Drop it deliberately instead of letting the camera quietly stop steering.
        void MaintainLock()
        {
            var held=player.target;
            if(!held){ReleaseLock();return;}
            var enemy=held.GetComponentInParent<SumiEnemy>();
            if(!enemy||enemy.dead){ReleaseLock();return;}
            Vector3 offset=held.position-player.transform.position;offset.y=0;
            if(offset.magnitude>LockDrop)ReleaseLock();
        }
        // step 0 acquires, +1/-1 walks to the next foe on that side of the current one.
        void SelectTarget(int step)
        {
            var camera=Camera.main;if(!camera)return;
            var held=step==0?null:player.target;
            Vector3 heldView=held?camera.WorldToViewportPoint(held.position+Vector3.up):Vector3.zero;
            SumiEnemy best=null;float score=float.MaxValue;
            var live=SumiEnemy.Active;
            for(int i=live.Count-1;i>=0;i--)
            {
                var candidate=live[i];
                if(!candidate){live.RemoveAt(i);continue;}
                if(candidate.dead||candidate.transform==player.target)continue;
                var offset=candidate.transform.position-player.transform.position;float distance=offset.magnitude;
                if(distance>LockRange||Physics.Raycast(player.transform.position+Vector3.up,offset.normalized,distance,1<<8))continue;
                var viewport=camera.WorldToViewportPoint(candidate.transform.position+Vector3.up);if(viewport.z<0)continue;
                float center=(new Vector2(viewport.x-.5f,viewport.y-.5f)).sqrMagnitude;
                float candidateScore=center*26+distance;
                if(held){float side=viewport.x-heldView.x;if(side*step<=.01f)candidateScore+=64;}
                if(candidateScore<score){score=candidateScore;best=candidate;}
            }
            // No cinematic yank on acquire; SumiCamera eases onto the new bearing under its own limit.
            if(best){player.target=best.transform;player.locked=true;if(view)view.EngageLock();}
        }
        void OnDestroy(){SumiCombatFeedback.Clear();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;I=null;}
    }
}
