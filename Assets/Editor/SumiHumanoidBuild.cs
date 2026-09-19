using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class SumiHumanoidBuild
{
    public const string Model="Assets/Sumi/Characters/Superhero_Male_FullBody.fbx";
    public const string Motions="Assets/Sumi/Characters/UAL1_Standard.fbx";
    public static void Import()
    {
        foreach(string path in new[]{Model,Motions})
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }
        Inspect();
    }
    public static void Inspect()
    {
        string text="";
        foreach(string path in new[]{Model,Motions})
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            text+=path+"\n";
            foreach(var a in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>())text+="AVATAR "+a.name+" valid="+a.isValid+" human="+a.isHuman+"\n";
            foreach(var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())if(!c.name.StartsWith("__"))text+="CLIP "+c.name+" "+c.length+"\n";
            if(path==Model)
            {
                foreach(var t in model.GetComponentsInChildren<Transform>())text+="BONE "+t.name+" "+t.position+"\n";
                foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())text+="MESH "+r.name+" "+r.sharedMesh.vertexCount+" bounds="+r.bounds+" local="+r.sharedMesh.bounds+" scale="+r.transform.lossyScale+"\n";
            }
        }
        File.WriteAllText("Logs/humanoid-import.txt",text);Debug.Log(text);
    }
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Sumi/Resources/Ronin");
        var importer=(ModelImporter)AssetImporter.GetAtPath(Motions);
        var clips=importer.defaultClipAnimations;
        foreach(var c in clips){c.loopTime=c.name.Contains("Loop")||c.name.Contains("Idle");c.lockRootRotation=true;c.lockRootPositionXZ=true;c.lockRootHeightY=true;}
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var source=AssetDatabase.LoadAllAssetsAtPath(Motions).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
        AnimationClip Copy(string suffix)
        {
            string path="Assets/Sumi/Resources/Ronin/"+suffix+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=UnityEngine.Object.Instantiate(source.First(c=>c.name.EndsWith("|"+suffix)));clip.name=suffix;AssetDatabase.CreateAsset(clip,path);}
            return clip;
        }
        string controllerPath="Assets/Sumi/Resources/Ronin/Locomotion.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller)
        {
            controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            var tree=new BlendTree{name="Human locomotion",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);
            tree.AddChild(Copy("Idle_Loop"),0);tree.AddChild(Copy("Walk_Loop"),1.8f);tree.AddChild(Copy("Jog_Fwd_Loop"),4.6f);
            var state=controller.layers[0].stateMachine.AddState("Locomotion");state.motion=tree;controller.layers[0].stateMachine.defaultState=state;
            var dodge=controller.layers[0].stateMachine.AddState("Dodge");dodge.motion=Copy("Crouch_Fwd_Loop");dodge.speed=2;
        }
        var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));obj.name="Sumi articulated ronin";
        var animator=obj.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        Dress(obj);
        PrefabUtility.SaveAsPrefabAsset(obj,"Assets/Sumi/Resources/Ronin/Humanoid.prefab");UnityEngine.Object.DestroyImmediate(obj);
        AssetDatabase.SaveAssets();
    }
}
