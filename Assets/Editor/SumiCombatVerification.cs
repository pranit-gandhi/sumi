using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sumi;
using Object=UnityEngine.Object;

// Runs against real Play Mode objects. The check scene is disposable: stop Play Mode afterwards.
public static class SumiCombatVerification
{
    static IEnumerator suite;
    static readonly List<string> results=new List<string>();
    static double deadline;static int lastFrame;
    static SumiPlayer player;
    static SumiRunDirector run;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [MenuItem("Sumi/Verify Enemy Combat (Play Mode)")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        if(suite!=null)throw new InvalidOperationException("Combat verification is already running.");
        results.Clear();deadline=EditorApplication.timeSinceStartup+100;lastFrame=-1;
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/combat-verification.txt","RUNNING");
        player=Object.FindFirstObjectByType<SumiPlayer>();run=Object.FindFirstObjectByType<SumiRunDirector>();
        suite=Checks();EditorApplication.update+=Step;
    }
    static void Step()
    {
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended during verification.");
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Verification timed out.");
            // Editor updates may run several times between game frames.
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            if(!suite.MoveNext())Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Step;suite=null;
        if(player){player.injectedGuard=false;player.injectedParry=false;player.injectedMove=Vector2.zero;player.injectedAttack=false;player.injectedDash=false;player.enabled=true;}
        SumiTime.Reset();
        string report=(error==null?"PASS":"FAIL")+"\n"+string.Join("\n",results)+(error==null?"":"\n"+error);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/combat-verification.txt",report);
        if(error==null)Debug.Log("SUMI_COMBAT_VERIFICATION "+report);else Debug.LogError("SUMI_COMBAT_VERIFICATION "+report);
    }
    static void Check(bool condition,string description)
    {if(!condition)throw new Exception(description);results.Add("PASS "+description);}
    static void Set(object target,string field,object value){target.GetType().GetField(field,Private).SetValue(target,value);}
    static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,Private).Invoke(target,args);}
    static void Place(Transform target,Vector3 position,Quaternion rotation)
    {
        var body=target.GetComponent<CharacterController>();if(body)body.enabled=false;
        target.SetPositionAndRotation(position,rotation);if(body)body.enabled=true;Physics.SyncTransforms();
    }
    static void ResetPlayer()
    {
        SumiTime.Reset();player.enabled=false;player.controllable=true;player.combat.health=player.combat.maxHealth;
        player.injectedAttack=player.injectedHeavy=player.injectedDash=player.injectedGuard=player.injectedParry=false;
        player.injectedMove=Vector2.zero;player.locked=false;player.target=null;player.combat.ClearInput();
        Call(player.combat,"Enter",SumiCombatState.Free,"Locomotion",.05f);
        Set(player.combat,"damageGraceUntil",0f);player.velocity=Vector3.zero;
        Place(player.transform,new Vector3(0,.02f,0),Quaternion.identity);
    }
    static void StartWave(int wave)
    {
        foreach(var e in Object.FindObjectsByType<SumiEnemy>(FindObjectsSortMode.None))if(e)Object.DestroyImmediate(e.gameObject);
        foreach(var a in Object.FindObjectsByType<SumiArrowStrike>(FindObjectsSortMode.None))Object.DestroyImmediate(a.gameObject);
        SumiTime.Reset();Call(run,"BeginWave",wave);Set(run,"nextArrowAt",Time.time+999f);ResetPlayer();
    }
    static void StartEnemy(SumiEnemy enemy,SumiEnemyAttack attack)
    {
        enemy.enabled=false;enemy.health=enemy.maxHealth;Set(enemy,"phasePending",false);
        Place(enemy.transform,new Vector3(0,.02f,2),Quaternion.Euler(0,180,0));
        Call(enemy,"BeginAttack",attack);
    }
    static IEnumerator Wait(float seconds)
    {float end=Time.unscaledTime+seconds;while(Time.unscaledTime<end)yield return null;}

    static IEnumerator Checks()
    {
        if(run.state==SumiRunState.Title)
        {
            run.DebugBeginJourney();yield return null;
            Check(run.state==SumiRunState.Intro&&Time.timeScale==1,"Begin Journey leaves the title screen and restores time");
        }
        StartWave(1);yield return null;
        var retainer=SumiEnemy.Active.First(e=>!e.dead);
        Check(Mathf.Approximately(SumiCombatFeedback.ComboShakeScale(1),1f)&&SumiCombatFeedback.ComboShakeScale(2)<.6f&&SumiCombatFeedback.ComboShakeScale(3)<.4f,"Combo follow-ups progressively reduce camera impulse");
        foreach(var e in SumiEnemy.Active.ToArray())e.enabled=false;
        StartEnemy(retainer,SumiEnemyAttack.RetainerCut);
        player.combat.state=SumiCombatState.GuardHeld;
        float hp=player.combat.health,enemyHp=retainer.health;
        player.combat.ReceiveEnemyHit(16,0,retainer,player.transform.position+Vector3.up);
        Check(Mathf.Approximately(player.combat.health,hp-2.88f),"Held guard takes predictable chip damage");
        Check(retainer.health==enemyHp&&retainer.state==SumiEnemyState.Windup,"Held guard does not damage or interrupt enemy");

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerCut);
        player.combat.Tick(Vector2.zero,false,false,true,false);Set(player.combat,"elapsed",.08f);
        Check(player.combat.state==SumiCombatState.GuardStartup,"Dedicated parry input enters its own timed stance without holding guard");
        player.combat.ReceiveEnemyHit(16,0,retainer,player.transform.position+Vector3.up);
        Check(player.combat.health==player.combat.maxHealth&&retainer.health==enemyHp-12,"Perfect deflection avoids damage and retaliates");
        Check(retainer.state==SumiEnemyState.Recoil&&retainer.Deflected&&Mathf.Approximately((float)retainer.GetType().GetField("recoilDuration",Private).GetValue(retainer),.95f),"Deflection grants a visible knocked-aside punish window");
        Check(((Vector3)retainer.GetType().GetField("velocity",Private).GetValue(retainer)).magnitude>2,"Deflection immediately changes enemy momentum");
        Set(retainer,"elapsed",.20f);retainer.TakeHit(1,Vector3.forward,SumiHitKind.ShoulderCut);
        Check(Mathf.Approximately(retainer.StateTime,.20f),"A hit during recoil does not start another reaction");

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerHeavy);
        retainer.TakeHit(21,Vector3.forward,SumiHitKind.ShoulderCut);
        Check(retainer.state==SumiEnemyState.Windup&&retainer.health==69,"Braced windup takes damage without light stagger");
        retainer.TakeHit(1,Vector3.forward,SumiHitKind.HeavyCut);
        Check(retainer.state==SumiEnemyState.Recoil,"Heavy cut interrupts braced windup");

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerHeavy);Call(retainer,"Enter",SumiEnemyState.Strike);
        retainer.TakeHit(1,Vector3.forward,SumiHitKind.HeavyCut);
        Check(retainer.state==SumiEnemyState.Strike,"Committed braced strike cannot be repeatedly cancelled");
        retainer.Parried(true);Check(retainer.state==SumiEnemyState.Recoil,"Perfect deflection stops a committed braced strike");

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerHeavy);
        Place(retainer.transform,new Vector3(0,.02f,1.65f),Quaternion.Euler(0,180,0));
        retainer.enabled=true;player.combat.Tick(Vector2.zero,false,false,false,false,false,false,true);
        float heavyEnd=Time.time+.65f;
        while(Time.time<heavyEnd){player.combat.Tick(Vector2.zero,false,false,false,false);yield return null;}
        Check(Mathf.Approximately(retainer.health,retainer.maxHealth-player.combat.heavy.damage),$"Actual player heavy blade trace hits exactly once (hp={retainer.health}, player={player.transform.position}, enemy={retainer.transform.position}, confirmed={player.combat.HitConfirmed})");
        Check(retainer.state==SumiEnemyState.Recoil,"Actual heavy input breaks enemy bracing");retainer.enabled=false;

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerCut);
        player.combat.state=SumiCombatState.GuardHeld;
        player.combat.ReceiveEnemyHit(24,0,retainer,Vector3.up,true);
        Check(player.combat.health==76&&player.combat.state==SumiCombatState.HitStun,"Crimson sweep defeats held guard");
        ResetPlayer();player.combat.state=SumiCombatState.GuardStartup;Set(player.combat,"elapsed",.08f);
        player.combat.ReceiveEnemyHit(24,0,retainer,Vector3.up,true);
        Check(player.combat.health==76,"Crimson sweep clearly requires evasion, including against perfect guard");
        ResetPlayer();player.combat.state=SumiCombatState.DashStrike;Set(player.combat,"elapsed",.15f);
        player.combat.ReceiveEnemyHit(24,0,retainer,Vector3.up,true);
        Check(player.combat.health==100,"Brush Flash invulnerability evades crimson sweep");

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerCut);
        Place(player.transform,new Vector3(4,.02f,0),Quaternion.identity);Call(retainer,"TryContact");
        Check(player.combat.health==100,"Leaving the warned attack volume avoids contact");
        Place(player.transform,new Vector3(0,.02f,0),Quaternion.identity);Call(retainer,"TryContact");
        float hitHealth=player.combat.health;Set(player.combat,"damageGraceUntil",0f);
        // The actual update owns the one-contact flag; contact is not retried by the state machine.
        Check(hitHealth==84&&(bool)retainer.GetType().GetField("struck",Private).GetValue(retainer),"Contact applies one hit and records the strike");

        ResetPlayer();StartEnemy(retainer,SumiEnemyAttack.RetainerCut);retainer.enabled=true;
        player.combat.Tick(Vector2.zero,false,true,false,false);
        bool sawReturn=false;float until=Time.time+2.2f;
        while(Time.time<until)
        {
            if(retainer.CurrentAttack==SumiEnemyAttack.Return)sawReturn=true;
            player.combat.Tick(Vector2.zero,false,true,false,false);
            yield return null;
        }
        Check(sawReturn&&player.combat.health<98,"Live Retainer combo continues through held guard");
        player.injectedGuard=false;retainer.enabled=false;

        StartWave(3);var shade=SumiEnemy.Active.First(e=>e.kind==SumiEnemyKind.Shade);
        foreach(var e in SumiEnemy.Active.ToArray())e.enabled=false;
        StartEnemy(shade,SumiEnemyAttack.ShadeLunge);shade.enabled=true;
        Vector3 start=shade.transform.position;bool recovering=false;
        until=Time.time+1.5f;
        while(Time.time<until){if(shade.state==SumiEnemyState.Recovery)recovering=true;yield return null;}
        Check(recovering&&Vector3.Distance(start,shade.transform.position)>.2f,"Shade lunges and visibly repositions after attacking");
        shade.enabled=false;

        ResetPlayer();var arrow=new GameObject("Verification arrow").AddComponent<SumiArrowStrike>();arrow.Init(player,run);
        run.TogglePause();float age=(float)arrow.GetType().GetField("age",Private).GetValue(arrow);
        var wait=Wait(.25f);while(wait.MoveNext())yield return null;
        Check(arrow&&Mathf.Approximately(age,(float)arrow.GetType().GetField("age",Private).GetValue(arrow)),"Pause preserves arrows and freezes their warning clock");
        run.TogglePause();SumiTime.HitStop(.20f);
        wait=Wait(.12f);while(wait.MoveNext())yield return null;
        Check(Mathf.Approximately(age,(float)arrow.GetType().GetField("age",Private).GetValue(arrow)),"Hit stop also freezes arrow warning timing");
        Object.DestroyImmediate(arrow.gameObject);SumiTime.Reset();

        // Exercise actual director handoffs, visibility and group attacks for several seconds.
        StartWave(2);int slot=0;
        foreach(var e in SumiEnemy.Active)
        {Place(e.transform,new Vector3((slot++-1)*1.25f,.02f,2.5f),Quaternion.Euler(0,180,0));}
        player.combat.health=1000;player.combat.maxHealth=1000;
        var seen=new HashSet<int>();int maxAttackers=0;until=Time.time+6;
        while(Time.time<until)
        {
            int count=0;
            foreach(var e in SumiEnemy.Active)if(e.Attacking){count++;seen.Add(e.GetInstanceID());}
            maxAttackers=Math.Max(maxAttackers,count);yield return null;
        }
        Check(maxAttackers==1&&seen.Count>=2,"Group attack handoffs involve multiple enemies without simultaneous melee strikes");
        Check(player.combat.health<1000,"Live group AI damages an idle player");
        player.combat.maxHealth=100;

        // Complete the real run state machine, including the extended wave ladder and spell choices.
        StartWave(1);foreach(var e in SumiEnemy.Active.ToArray())e.Execute();yield return null;yield return null;
        Check(run.state==SumiRunState.Wave&&SumiEnemy.Active.Count(e=>!e.dead)==3,"Wave one advances to gathering wave two");
        foreach(var e in SumiEnemy.Active.ToArray())e.Execute();yield return null;yield return null;
        Check(run.state==SumiRunState.Upgrade,"Wave two advances to spell choice");Call(run,"Choose",0);yield return null;
        Check(run.state==SumiRunState.Wave&&SumiEnemy.Active.Count(e=>!e.dead)==4,"Spell choice starts mixed wave three");
        while(run.state!=SumiRunState.Boss)
        {
            if(run.state==SumiRunState.Wave){foreach(var e in SumiEnemy.Active.ToArray())if(e&&!e.dead)e.Execute();yield return null;yield return null;}
            else if(run.state==SumiRunState.Upgrade){Call(run,"Choose",0);yield return null;}
            else if(run.state==SumiRunState.BossIntro)yield return null;
            else throw new Exception("Unexpected run state before boss: "+run.state);
        }
        while(run.state==SumiRunState.BossIntro)yield return null;
        Check(run.state==SumiRunState.Boss,"Final wave chain starts Oni encounter");
        var oni=SumiEnemy.Active.First(e=>!e.dead&&e.kind==SumiEnemyKind.Oni);ResetPlayer();
        StartEnemy(oni,SumiEnemyAttack.OniHeavy);oni.TakeHit(160,Vector3.forward,SumiHitKind.ShoulderCut);
        Check(oni.state==SumiEnemyState.Windup&&oni.health==180,"Oni keeps its attack when damaged across phase threshold");
        Call(oni,"Enter",SumiEnemyState.Approach);oni.enabled=true;
        until=Time.unscaledTime+1;while(!oni.Enraged&&Time.unscaledTime<until)yield return null;
        Check(oni.Enraged,"Oni enters its second phase at the next safe transition");oni.enabled=false;
        oni.health=50;Check(!oni.CanExecute,"Oni cannot skip its finale with the minor-enemy execution threshold");
        oni.health=40;Check(oni.CanExecute,"Oni becomes executable at twelve percent health");
        ResetPlayer();StartEnemy(oni,SumiEnemyAttack.OniSweep);oni.enabled=true;
        player.combat.Tick(Vector2.zero,false,true,false,false);
        until=Time.time+3f;bool sweepStruck=false,captured=false;
        while(Time.time<until)
        {
            player.combat.Tick(Vector2.zero,false,true,false,false);
            if(!captured&&oni.state==SumiEnemyState.Windup&&oni.StateTime>.55f)
            {SumiAutomation.CaptureAt("Logs/oni-windup.png",1280,720);captured=true;}
            if(player.combat.health<player.combat.maxHealth)sweepStruck=true;
            if(oni.state==SumiEnemyState.Recovery)break;
            yield return null;
        }
        Check(sweepStruck,$"Live Oni sweep connects through passive guard (state={oni.state}, health={player.combat.health}, time={oni.StateTime:0.00})");
        player.injectedGuard=false;oni.Execute();
        float scaleUntil=Time.unscaledTime+1.2f;while(Time.timeScale<1f&&Time.unscaledTime<scaleUntil)yield return null;
        yield return null;
        Check(run.state==SumiRunState.Victory&&Time.timeScale==1,"Boss death reaches victory and restores time");

        StartWave(1);player.combat.ReceiveWorldHit(999,Vector3.up);yield return null;
        Check(run.state==SumiRunState.Death&&player.combat.state==SumiCombatState.Dead,"Lethal damage reaches death and locks combat");
        float settle=Time.unscaledTime+1.15f;while(Time.unscaledTime<settle)yield return null;
        Check(player.transform.position.y>-.5f&&player.transform.position.y<2.2f,"Player death remains on the courtyard");
        Check(player.GetComponent<SumiDeathActor>(),"Player death runs the cinematic actor");
        Check(SumiDeath.CanSkip,"Death skip is available after the mandatory beat");
        run.DebugRestart();yield return null;yield return null;
        run=Object.FindFirstObjectByType<SumiRunDirector>();player=Object.FindFirstObjectByType<SumiPlayer>();
        Check(run&&run.state==SumiRunState.Intro&&player.combat.health==100&&Time.timeScale==1,"Restart creates a fresh run and resets combat time");
    }

    public static void Inspect()
    {
        var p=Object.FindFirstObjectByType<SumiPlayer>();var r=Object.FindFirstObjectByType<SumiRunDirector>();
        string report=$"RUN={r.state} HEALTH={p.combat.health} TIME={Time.time} SCALE={Time.timeScale}\n";
        foreach(var e in SumiEnemy.Active)report+=$"{e.name} {e.state} HP={e.health} POS={e.transform.position} MOVE={e.CurrentAttack?.name}\n";
        File.WriteAllText("Logs/combat-state.txt",report);Debug.Log(report);
    }
}
