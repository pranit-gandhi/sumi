using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
namespace Sumi
{
    public class SumiGame : MonoBehaviour
    {
        public static SumiGame I; public SumiConfig config;public SumiPlayer player; public SumiCamera view;public SumiRunDirector run;
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
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        void Update(){SumiCombatFeedback.Tick();if(UnityEngine.InputSystem.Keyboard.current?.qKey.wasPressedThisFrame==true){if(player.locked){player.locked=false;player.target=null;}else SelectTarget();}if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true)run.TogglePause();}
        void SelectTarget()
        {
            var camera=Camera.main;SumiEnemy best=null;float score=float.MaxValue;
            foreach(var candidate in FindObjectsByType<SumiEnemy>(FindObjectsSortMode.None))
            {if(candidate.dead)continue;var offset=candidate.transform.position-player.transform.position;float distance=offset.magnitude;if(distance>13||Physics.Raycast(player.transform.position+Vector3.up,offset.normalized,distance,1<<8))continue;var viewport=camera.WorldToViewportPoint(candidate.transform.position+Vector3.up);if(viewport.z<0)continue;float center=(new Vector2(viewport.x-.5f,viewport.y-.5f)).sqrMagnitude;float candidateScore=center*26+distance;if(candidateScore<score){score=candidateScore;best=candidate;}}
            if(best){player.target=best.transform;player.locked=true;view.Frame(best.transform,.22f);}
        }
        void OnDestroy(){SumiCombatFeedback.Clear();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;I=null;}
    }
}
