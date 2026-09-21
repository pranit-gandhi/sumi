using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sumi;
using Object=UnityEngine.Object;

public static class SumiShurikenVerification
{
    static IEnumerator suite;static double deadline;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Sumi/Verify Shuriken (Play Mode)")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        if(suite!=null)throw new InvalidOperationException("Shuriken verification is already running.");
        suite=Checks();deadline=EditorApplication.timeSinceStartup+12;EditorApplication.update+=Step;
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/shuriken-verification.txt","RUNNING");
    }
    static void Step()
    {
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended during shuriken verification.");
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Shuriken verification timed out.");
            if(!suite.MoveNext())Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Step;suite=null;string report=error==null?"PASS\nExact supplied ShurikenStar mesh loaded\nThrow uses the off hand\nRelease created a rendered projectile":"FAIL\n"+error;
        File.WriteAllText("Logs/shuriken-verification.txt",report);if(error==null)Debug.Log("SUMI_SHURIKEN_VERIFICATION "+report);else Debug.LogError("SUMI_SHURIKEN_VERIFICATION "+report);
    }
    static IEnumerator Checks()
    {
        var player=Object.FindFirstObjectByType<SumiPlayer>();if(!player||!player.combat)throw new Exception("Player combat was not available.");
        player.enabled=false;player.controllable=true;
        player.combat.GetType().GetMethod("Enter",Private).Invoke(player.combat,new object[]{SumiCombatState.Free,"Locomotion",.05f});
        if(!Resources.Load<GameObject>("Sumi/Projectiles/shuriken_star"))throw new Exception("Converted ShurikenStar resource did not load.");
        player.combat.Tick(Vector2.zero,false,false,false,false,false,true,false);
        if(!player.combat.IsThrowing)throw new Exception("Throw state did not start.");
        player.combat.GetThrowHandPose(out var hand,out _);if(player.transform.InverseTransformPoint(hand).x>=-.08f)throw new Exception("Throw pose is not on the off-hand side.");
        float until=Time.unscaledTime+.42f;while(Time.unscaledTime<until){player.combat.Tick(Vector2.zero,false,false,false,false);yield return null;}
        SumiAutomation.CaptureAt("Logs/shuriken-charge.png",1280,720);
        until=Time.unscaledTime+.26f;while(Time.unscaledTime<until){player.combat.Tick(Vector2.zero,false,false,false,false);yield return null;}
        var star=Object.FindFirstObjectByType<SumiShuriken>();if(!star)throw new Exception("Shuriken was not released.");
        if(star.GetComponentsInChildren<Renderer>(true).Length==0)throw new Exception("Released shuriken has no rendered model.");
        SumiAutomation.CaptureAt("Logs/shuriken-flight.png",1280,720);
        Object.Destroy(star.gameObject);player.combat.GetType().GetMethod("Enter",Private).Invoke(player.combat,new object[]{SumiCombatState.Free,"Locomotion",.05f});player.enabled=true;
    }
}
