using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Reproducible CC0 clip copies and additive combat states; never rebuilds the humanoid prefab.
public static class SumiCombatAuthoring
{
    [MenuItem("Sumi/Author Combat Clips")]
    public static void Apply()
    {
        const string root="Assets/Sumi/Resources/Ronin/";var source=AssetDatabase.LoadAllAssetsAtPath(SumiHumanoidBuild.Motions).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
        AnimationClip Copy(string sourceName,string output){string path=root+output+".anim";var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!c){c=Object.Instantiate(source.First(x=>x.name.EndsWith("|"+sourceName)));c.name=output;AssetDatabase.CreateAsset(c,path);}return c;}
        var attack=Copy("Sword_Attack","Sword_Attack");var guard=Copy("Sword_Idle","Sword_Guard");var hit=Copy("Hit_Chest","Sword_Hit");var death=Copy("Death01","Sword_Death");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(root+"Locomotion.controller");var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;var sm=controller.layers[0].stateMachine;
        Add(sm,"Attack1",attack,1.97f,false);Add(sm,"Attack2",attack,2.30f,true);Add(sm,"Attack3",attack,2.79f,true);Add(sm,"Guard",guard,1,false);Add(sm,"Hit",hit,1,false);Add(sm,"Death",death,1,false);
        AssetDatabase.SaveAssets();Debug.Log("SUMI_COMBAT_AUTHORING copied CC0 clips and added combat states");
    }
    static void Add(AnimatorStateMachine sm,string name,Motion motion,float speed,bool mirror){var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name)??sm.AddState(name);state.motion=motion;state.speed=speed;state.mirror=mirror;}
}
