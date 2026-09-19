using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Sumi;

public static class SumiBuild
{
    public static void ShaderErrors(){foreach(var name in new[]{"Sumi/Ink Wash","Sumi/Ink Contour"}){var s=Shader.Find(name);foreach(var m in ShaderUtil.GetShaderMessages(s))Debug.LogError(name+" "+m.message+" line="+m.line);}}
    [MenuItem("Sumi/Rebuild authored shrine")]
    public static void CreateScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before building scene.");
        Directory.CreateDirectory("Assets/Sumi/Resources/Sumi");Directory.CreateDirectory("Assets/Sumi/Art");
        var contour=AssetDatabase.LoadAssetAtPath<Material>("Assets/Sumi/Resources/Sumi/Contour.mat");
        if(!contour){contour=new Material(Shader.Find("Sumi/Ink Contour"));AssetDatabase.CreateAsset(contour,"Assets/Sumi/Resources/Sumi/Contour.mat");}
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Sumi/Resources"}))
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(mat.shader.name.Contains("Simple Lit")){mat.shader=Shader.Find("Sumi/Ink Wash");EditorUtility.SetDirty(mat);}
            if(mat.name=="Ground")mat.SetColor("_BaseColor",new Color(.88f,.89f,.86f));
            if(mat.name=="Paper")mat.SetColor("_BaseColor",SumiArt.Paper);
            if(mat.name=="Stone")mat.SetColor("_BaseColor",new Color(.67f,.68f,.65f));
            if(mat.name.StartsWith("Paving"))mat.SetColor("_BaseColor",new Color(.80f,.81f,.78f));
            if(mat.name=="DistantInk")mat.SetColor("_BaseColor",new Color(.64f,.66f,.65f));
        }
        var config=AssetDatabase.LoadAssetAtPath<SumiConfig>("Assets/Sumi/Resources/Sumi/Combat.asset");
        if(!config){config=ScriptableObject.CreateInstance<SumiConfig>();AssetDatabase.CreateAsset(config,"Assets/Sumi/Resources/Sumi/Combat.asset");}
        CreateLocomotion();
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Sumi/Resources/Sumi/Atmosphere.asset");
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,"Assets/Sumi/Resources/Sumi/Atmosphere.asset");var bloom=profile.Add<Bloom>();bloom.intensity.Override(.18f);bloom.threshold.Override(.95f);var vig=profile.Add<Vignette>();vig.intensity.Override(.23f);vig.smoothness.Override(.5f);foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Sumi/Resources/Sumi/DrawnInk.mat");
        if(!ink){ink=new Material(Shader.Find("Sumi/Drawn Ink"));AssetDatabase.CreateAsset(ink,"Assets/Sumi/Resources/Sumi/DrawnInk.mat");}
        var paper=AssetDatabase.LoadAssetAtPath<Material>("Assets/Sumi/Resources/Sumi/BarePaper.mat");
        if(!paper){paper=new Material(Shader.Find("Sumi/Paper"));AssetDatabase.CreateAsset(paper,"Assets/Sumi/Resources/Sumi/BarePaper.mat");}
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Sumi/Resources/Drawings"}))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            // Ink extraction happens in the shader. Preserve fine source strokes and prevent atlas wrapping.
            importer.sRGBTexture=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.SaveAndReimport();
        }
        var world=SumiSketchWorld.Build();
        int drawingIndex=0;
        foreach(var drawing in world.GetComponentsInChildren<SumiDrawing>())
        {
            var mesh=drawing.GetComponent<MeshFilter>().sharedMesh;
            string meshPath="Assets/Sumi/Art/Drawing"+(drawingIndex++)+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
            else {AssetDatabase.CreateAsset(mesh,meshPath);saved=mesh;}
            drawing.GetComponent<MeshFilter>().sharedMesh=saved;
        }
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.transform.SetPositionAndRotation(new Vector3(0,4,-11),Quaternion.Euler(20,0,0));camera.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var game=new GameObject("Sumi",typeof(SumiGame)).GetComponent<SumiGame>();game.config=config;
        // Prewarm character materials and geometry so shader stripping cannot remove their shaders.
        var preview=new GameObject("Rig authoring temporary",typeof(SumiRig));preview.GetComponent<SumiRig>().Construct();UnityEngine.Object.DestroyImmediate(preview);
        foreach(var pair in SumiArt.Materials){if(!AssetDatabase.Contains(pair.Value))AssetDatabase.CreateAsset(pair.Value,"Assets/Sumi/Resources/Sumi/"+pair.Key+".mat");}
        foreach(var pair in SumiArt.Meshes){if(!AssetDatabase.Contains(pair.Value))AssetDatabase.CreateAsset(pair.Value,"Assets/Sumi/Art/"+pair.Key+".asset");}
        PlayerSettings.productName=Identity.Title;PlayerSettings.companyName="Sumi Studio";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.runInBackground=true;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        var ps=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=ps.FindProperty("activeInputHandler");if(input!=null)input.intValue=1;ps.ApplyModifiedPropertiesWithoutUndo();
        QualitySettings.shadowDistance=28;QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.Medium;QualitySettings.antiAliasing=2;
        var rp=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if(rp){rp.renderScale=1;rp.msaaSampleCount=2;rp.shadowDistance=28;rp.mainLightShadowmapResolution=1024;rp.supportsHDR=false;EditorUtility.SetDirty(rp);}
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/SumiShrine.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/SumiShrine.unity",true)};
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("SUMI_SCENE_READY "+Application.dataPath);
    }
    static void CreateLocomotion()
    {
        const string path="Assets/Sumi/Resources/Sumi/RoninLocomotion.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path))return;
        var controller=AnimatorController.CreateAnimatorControllerAtPath(path);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
        var tree=new BlendTree{name="Grounded locomotion",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);
        for(int mode=0;mode<3;mode++)
        {
            var clip=new AnimationClip{name=new[]{"Stillness","Measured walk","Run"}[mode],frameRate=30};float duration=mode==2?.62f:1.0f;float amplitude=mode==0?0:mode==1?22:34;
            foreach(string side in new[]{"Right","Left"})
            {
                var thigh=new AnimationCurve();var shin=new AnimationCurve();
                for(int k=0;k<=30;k++){float f=k/30f;float wave=Mathf.Sin(f*Mathf.PI*2+(side=="Right"?0:Mathf.PI));thigh.AddKey(f*duration,wave*amplitude);shin.AddKey(f*duration,Mathf.Max(0,-wave)*amplitude*1.3f);}
                clip.SetCurve(side+"Leg",typeof(Transform),"localEulerAnglesRaw.x",thigh);clip.SetCurve(side+"Leg/"+side+"Shin",typeof(Transform),"localEulerAnglesRaw.x",shin);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,"Assets/Sumi/Art/"+clip.name+".anim");tree.AddChild(clip,mode==0?0:mode==1?2.0f:4.6f);
        }
        var state=controller.layers[0].stateMachine.AddState("Locomotion");state.motion=tree;controller.layers[0].stateMachine.defaultState=state;
    }
    [MenuItem("Sumi/Build WebGL")]
    public static void WebGL()
    {
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))
            throw new Exception("WebGL build support is not registered in this Unity Editor installation. Repair/reinstall the Unity 6000.2.14f1 WebGL Build Support module, restart the Editor, then retry.");
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.WebGL&&!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL,BuildTarget.WebGL))
            throw new Exception("Unity could not switch the active build target to WebGL.");
        Directory.CreateDirectory("Builds/WebGL");
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.dataCaching=true;
        PlayerSettings.WebGL.initialMemorySize=128;PlayerSettings.WebGL.maximumMemorySize=512;
        PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SumiShrine.unity"},locationPathName="Builds/WebGL",target=BuildTarget.WebGL,options=BuildOptions.None});
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/build-result.txt",report.summary.result+" bytes="+report.summary.totalSize+" errors="+report.summary.totalErrors+" duration="+report.summary.totalTime);
        if(report.summary.result!=BuildResult.Succeeded||report.summary.totalErrors>0||!File.Exists("Builds/WebGL/index.html"))
            throw new Exception("WebGL build failed acceptance: result="+report.summary.result+" errors="+report.summary.totalErrors+" index="+File.Exists("Builds/WebGL/index.html"));
    }
}
