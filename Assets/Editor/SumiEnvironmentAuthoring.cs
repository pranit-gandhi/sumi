using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Sumi;

// Reproducible environmental art pass. It replaces only the generated village root;
// combat, camera, the runtime ronin prefab and all gameplay authoring remain untouched.
public static class SumiEnvironmentAuthoring
{
    const string ScenePath="Assets/Scenes/SumiShrine.unity";
    const string MaterialRoot="Assets/Sumi/Resources/Sumi/";

    [MenuItem("Sumi/Apply Burnt-Orange Environment Pass")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before applying the environment pass.");
        if(SceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);

        CreateMaterials();
        ConfigurePostProcessing();

        var oldWorld=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go=>go.name.StartsWith("Sumi — dimensional ink village",StringComparison.Ordinal));
        if(oldWorld)UnityEngine.Object.DestroyImmediate(oldWorld);
        var world=SumiSketchWorld.Build();
        SaveGeneratedMeshes(world);

        var camera=Camera.main;
        if(camera)
        {
            camera.clearFlags=CameraClearFlags.Skybox;
            camera.backgroundColor=RenderSettings.fogColor;
            camera.allowHDR=true;
            camera.farClipPlane=115f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            EditorUtility.SetDirty(camera);
        }

        var rp=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if(rp)
        {
            rp.supportsHDR=true;
            rp.shadowDistance=32f;
            rp.mainLightShadowmapResolution=2048;
            EditorUtility.SetDirty(rp);
        }
        QualitySettings.shadowDistance=32f;

        foreach(var material in SumiArt.Materials.Values.Where(m=>m))EditorUtility.SetDirty(material);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();

        int renderers=world.GetComponentsInChildren<Renderer>(true).Length;
        int lights=world.GetComponentsInChildren<Light>(true).Length;
        int grass=world.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Pale susuki cluster",StringComparison.Ordinal));
        int huts=world.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Repeated ink dwelling",StringComparison.Ordinal));
        Debug.Log($"SUMI_ENVIRONMENT_READY renderers={renderers} lights={lights} grassClusters={grass} huts={huts} fog={RenderSettings.fogDensity:0.000}");
    }

    static void CreateMaterials()
    {
        Material Ensure(string name,string shaderName,Color color)
        {
            string path=MaterialRoot+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find(shaderName);
            if(!shader)throw new InvalidOperationException("Missing environment shader: "+shaderName);
            if(!material){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }

        var ground=Ensure("WetCharcoalGround","Sumi/Wet Charcoal Ground",new Color(.13f,.125f,.118f,1));
        ground.SetFloat("_Wetness",.60f);ground.SetFloat("_Smoothness",.58f);
        var grass=Ensure("PaleGrass","Sumi/Ink Wash",new Color(.78f,.72f,.61f,1));grass.SetFloat("_Porosity",.10f);
        Ensure("DampPatch","Sumi/Damp Patch",new Color(.018f,.016f,.015f,.20f));
        Ensure("LanternGlow","Sumi/Lantern Glow",new Color(2.15f,.74f,.14f,1));
        Ensure("AmberReflection","Sumi/Amber Reflection",new Color(1.75f,.58f,.10f,.52f));
        Ensure("LanternLightPool","Sumi/Lantern Light Pool",new Color(.72f,.26f,.065f,.22f));

        var sky=AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+"Evening Gradient Sky.mat");
        if(sky)
        {
            sky.SetColor("_HorizonColor",new Color(.58f,.245f,.10f,1));
            sky.SetColor("_MiddleColor",new Color(.29f,.09f,.036f,1));
            sky.SetColor("_ZenithColor",new Color(.068f,.028f,.021f,1));
            sky.SetColor("_MoonColor",new Color(1.15f,.58f,.22f,1));EditorUtility.SetDirty(sky);
        }
    }

    static void ConfigurePostProcessing()
    {
        string path=MaterialRoot+"Atmosphere.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
        foreach(var component in profile.components.ToArray())UnityEngine.Object.DestroyImmediate(component,true);
        profile.components.Clear();

        T Add<T>() where T:VolumeComponent
        {
            var component=profile.Add<T>(true);
            if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);
            return component;
        }

        var bloom=Add<Bloom>();bloom.threshold.Override(.82f);bloom.intensity.Override(.27f);bloom.scatter.Override(.42f);bloom.highQualityFiltering.Override(false);
        var vignette=Add<Vignette>();vignette.intensity.Override(.16f);vignette.smoothness.Override(.72f);vignette.color.Override(new Color(.055f,.02f,.012f));
        var grade=Add<ColorAdjustments>();grade.postExposure.Override(.34f);grade.contrast.Override(14f);grade.saturation.Override(-22f);grade.colorFilter.Override(new Color(1f,.98f,.95f));
        var balance=Add<WhiteBalance>();balance.temperature.Override(2f);balance.tint.Override(-2f);
        var tone=Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
        EditorUtility.SetDirty(profile);
    }

    static void SaveGeneratedMeshes(Transform world)
    {
        Directory.CreateDirectory("Assets/Sumi/Art");
        int drawingIndex=0;
        foreach(var drawing in world.GetComponentsInChildren<SumiDrawing>(true))
        {
            var filter=drawing.GetComponent<MeshFilter>();var source=filter.sharedMesh;if(!source)continue;
            string path=$"Assets/Sumi/Art/EnvironmentDrawing{drawingIndex++}.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(source,saved);UnityEngine.Object.DestroyImmediate(source);}else{AssetDatabase.CreateAsset(source,path);saved=source;}
            filter.sharedMesh=saved;EditorUtility.SetDirty(filter);
        }

        foreach(var pair in SumiArt.Meshes.ToArray())
        {
            var source=pair.Value;if(!source||AssetDatabase.Contains(source))continue;
            string safe=string.Concat(pair.Key.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
            string path="Assets/Sumi/Art/Environment_"+safe+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var users=world.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh==source).ToArray();
            if(saved){EditorUtility.CopySerialized(source,saved);UnityEngine.Object.DestroyImmediate(source);}else{AssetDatabase.CreateAsset(source,path);saved=source;}
            foreach(var filter in users){filter.sharedMesh=saved;EditorUtility.SetDirty(filter);}SumiArt.Meshes[pair.Key]=saved;
        }
    }
}
