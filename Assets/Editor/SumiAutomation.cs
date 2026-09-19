using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Local editor mailbox for reproducible verification and builds. Never shipped to players.
[InitializeOnLoad]
public static class SumiAutomation
{
    static double next;
    static string Root => Path.GetDirectoryName(Application.dataPath);
    static SumiAutomation() { EditorApplication.update += Tick; EditorApplication.delayCall += StartBridge; }
    static void StartBridge()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.AI.MCP.Editor.UnityMCPBridge")).FirstOrDefault(t => t != null);
        type?.GetMethod("Start", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
        next = EditorApplication.timeSinceStartup + .25;
        string file = Path.Combine(Root, "Tools/editor-command.txt");
        if (!File.Exists(file)) return;
        string command = File.ReadAllText(file).Trim(); File.Delete(file);
        try
        {
            switch(command)
            {
                case "state": Reply("PROJECT="+Application.dataPath+" VERSION="+Application.unityVersion+" PLAY="+EditorApplication.isPlaying+" PIPELINE="+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline+" SCENE="+SceneManager.GetActiveScene().path); break;
                case "save": EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets(); Reply("Saved"); break;
                case "play":
                    if(EditorApplication.isPlaying){Reply("Already playing "+SceneManager.GetActiveScene().path);break;}
                    if(SceneManager.GetActiveScene().path!="Assets/Scenes/SumiShrine.unity")EditorSceneManager.OpenScene("Assets/Scenes/SumiShrine.unity",OpenSceneMode.Single);
                    EditorSceneManager.SaveOpenScenes();EditorApplication.isPlaying=true;Reply("Play requested from "+SceneManager.GetActiveScene().path);break;
                case "stop": EditorApplication.isPlaying = false; Reply("Stop requested"); break;
                case "refresh": AssetDatabase.Refresh(); Reply("Refreshed"); break;
                case "capture": Capture(); break;
                default:
                    var split=command.LastIndexOf('.');
                    var target = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(command.Substring(0,split))).First(t=>t!=null);
                    target.GetMethod(command.Substring(split+1), BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
                    Reply("Completed "+command); break;
            }
        }
        catch(Exception e) { Debug.LogException(e); Reply("ERROR: "+e); }
    }
    static void Reply(string text) { Directory.CreateDirectory(Path.Combine(Root,"Logs")); File.WriteAllText(Path.Combine(Root,"Logs/editor-result.txt"),text); Debug.Log("SUMI_AUTOMATION "+text); }
    public static void Capture()
    {
        CaptureAt("Logs/game.png",1280,720);
        Reply("Captured Logs/game.png");
    }
    public static void CaptureAt(string path,int width,int height)
    {
        var camera = Camera.main;
        if(camera==null) camera=SceneView.lastActiveSceneView.camera;
        var rt = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var old=camera.targetTexture; var active=RenderTexture.active;
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        File.WriteAllBytes(Path.Combine(Root,path),tex.EncodeToPNG());
        camera.targetTexture=old; RenderTexture.active=active; UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
    }
}
