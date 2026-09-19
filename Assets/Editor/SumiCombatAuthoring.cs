using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Sumi;

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
        if(!controller.parameters.Any(p=>p.name=="AttackRate")){controller.AddParameter("AttackRate",AnimatorControllerParameterType.Float);var ps=controller.parameters;foreach(var p in ps)if(p.name=="AttackRate")p.defaultFloat=2.2f;controller.parameters=ps;}
        Add(sm,"Attack1",attack,1,false);Add(sm,"Attack2",attack,1,false);Add(sm,"Attack3",attack,1,false);Add(sm,"HeavyAttack",attack,1,false);Add(sm,"Guard",guard,1,false);Add(sm,"Hit",hit,1,false);Add(sm,"Death",death,1,false);
        foreach(var entry in sm.states)if(entry.state.name.StartsWith("Attack")||entry.state.name=="HeavyAttack"){entry.state.speedParameter="AttackRate";entry.state.speedParameterActive=true;}
        AuthorMoves();
        AssetDatabase.SaveAssets();Debug.Log("SUMI_COMBAT_AUTHORING copied CC0 clips and added combat states");
    }
    static void Add(AnimatorStateMachine sm,string name,Motion motion,float speed,bool mirror){var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name)??sm.AddState(name);state.motion=motion;state.speed=speed;state.mirror=mirror;}

    static void AuthorMoves()
    {
        const string folder="Assets/Sumi/Resources/Sumi/Attacks";
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Sumi/Resources/Sumi","Attacks");
        SumiAttackDefinition Move(string name,string state,SumiSwordArc arc,float duration,Vector2 active,Vector2 link,float damage,float lunge,bool finisher)
        {
            string path=folder+"/"+name+".asset";var move=AssetDatabase.LoadAssetAtPath<SumiAttackDefinition>(path);
            // Re-running clip authoring preserves designer tuning.
            if(move)return move;
            move=ScriptableObject.CreateInstance<SumiAttackDefinition>();move.name=name;move.animationState=state;move.arc=arc;move.duration=duration;move.activeWindow=active;move.linkWindow=link;move.damage=damage;move.lunge=lunge;move.finisher=finisher;
            move.guardCancel=active.y+.025f;move.dodgeCancel=active.y+.015f;move.hitDodgeCancel=active.x+.04f;
            move.hitStop=finisher?.065f:.04f;move.cameraKick=finisher?.22f:.11f;
            move.Validate();AssetDatabase.CreateAsset(move,path);return move;
        }
        var opening=Move("Opening","Attack1",SumiSwordArc.Descending,.48f,new Vector2(.105f,.255f),new Vector2(.255f,.42f),21,.48f,false);
        var returning=Move("Returning","Attack2",SumiSwordArc.Returning,.46f,new Vector2(.09f,.24f),new Vector2(.24f,.40f),25,.40f,false);
        var finisher=Move("Finisher","Attack3",SumiSwordArc.Sweep,.62f,new Vector2(.16f,.34f),new Vector2(.48f,.57f),40,.62f,true);
        var heavy=Move("Heavy","HeavyAttack",SumiSwordArc.Overhead,.73f,new Vector2(.23f,.43f),new Vector2(.56f,.68f),49,.72f,true);
        var flash=Move("Flash","Attack3",SumiSwordArc.Sweep,.52f,new Vector2(.12f,.32f),new Vector2(.34f,.46f),30,3.95f,true);
        if(!opening.lightFollowUp)opening.lightFollowUp=returning;if(!opening.heavyFollowUp)opening.heavyFollowUp=heavy;
        if(!returning.lightFollowUp)returning.lightFollowUp=finisher;if(!returning.heavyFollowUp)returning.heavyFollowUp=heavy;
        if(!flash.lightFollowUp)flash.lightFollowUp=opening;if(!flash.heavyFollowUp)flash.heavyFollowUp=heavy;
        foreach(var move in new[]{opening,returning,finisher,heavy,flash})EditorUtility.SetDirty(move);
    }
}
