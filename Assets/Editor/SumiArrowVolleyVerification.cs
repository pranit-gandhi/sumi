using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sumi;
using Object=UnityEngine.Object;

public static class SumiArrowVolleyVerification
{
    static IEnumerator suite;static double deadline;static int lastFrame;static readonly List<string> results=new List<string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [MenuItem("Sumi/Preview Arrow Volley (Play Mode)")]
    public static void Preview()
    {
        RequirePlayMode();var player=Object.FindFirstObjectByType<SumiPlayer>();var run=Object.FindFirstObjectByType<SumiRunDirector>();
        EnsureCombat(run);new GameObject("Authoring arrow volley").AddComponent<SumiArrowStrike>().Init(player,run);
    }

    [MenuItem("Sumi/Verify Arrow Volley (Play Mode)")]
    public static void Run()
    {
        RequirePlayMode();if(suite!=null)throw new InvalidOperationException("Volley verification is already running.");
        results.Clear();deadline=EditorApplication.timeSinceStartup+40;lastFrame=-1;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/arrow-volley-verification.txt","RUNNING");
        suite=Checks();EditorApplication.update+=Step;
    }

    static IEnumerator Checks()
    {
        var player=Object.FindFirstObjectByType<SumiPlayer>();var run=Object.FindFirstObjectByType<SumiRunDirector>();EnsureCombat(run);
        foreach(var old in Object.FindObjectsByType<SumiArrowStrike>(FindObjectsSortMode.None))Object.DestroyImmediate(old.gameObject);
        var volley=new GameObject("Verified arrow volley").AddComponent<SumiArrowStrike>();volley.Init(player,run);
        var tuning=player.config.arrowVolley;
        Check(volley.PredictedGameplayLandings.Count==tuning.arrowCount,"landing count matches the warned volley");
        Check(Object.FindObjectsByType<SumiArrowStrike>(FindObjectsSortMode.None).Length==1,"one controller owns the warned circle");
        var seen=new HashSet<SumiVolleyState>();float until=Time.time+tuning.TotalDuration+.2f;
        while(volley&&Time.time<until){seen.Add(volley.State);yield return null;}
        Check(seen.Contains(SumiVolleyState.Telegraph)&&seen.Contains(SumiVolleyState.Falling)&&seen.Contains(SumiVolleyState.Fade),"timeline exposes warning fill, staggered fall and fade");
        Check(!volley,"volley controller cleans up after the fade");
    }

    static void EnsureCombat(SumiRunDirector run)
    {
        if(run.state!=SumiRunState.Wave&&run.state!=SumiRunState.Boss)run.GetType().GetMethod("BeginWave",Private).Invoke(run,new object[]{1});
        run.GetType().GetField("nextArrowAt",Private).SetValue(run,Time.time+999f);
    }
    static void RequirePlayMode(){if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);results.Add("PASS "+message);}
    static void Step()
    {
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended during verification.");
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Volley verification timed out.");
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;if(!suite.MoveNext())Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Step;suite=null;string report=(error==null?"PASS":"FAIL")+"\n"+string.Join("\n",results)+(error==null?"":"\n"+error);
        File.WriteAllText("Logs/arrow-volley-verification.txt",report);if(error==null)Debug.Log("SUMI_ARROW_VOLLEY_VERIFICATION "+report);else Debug.LogError("SUMI_ARROW_VOLLEY_VERIFICATION "+report);
    }
}
