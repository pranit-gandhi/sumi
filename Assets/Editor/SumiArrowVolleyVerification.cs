using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sumi;
using Object=UnityEngine.Object;

// Focused authoring/verification entry points for the runtime-built shrine scene.
public static class SumiArrowVolleyVerification
{
    static IEnumerator suite;static double deadline;static int lastFrame;static readonly List<string> results=new List<string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [MenuItem("Sumi/Preview Arrow Volley (Play Mode)")]
    public static void Preview()
    {
        RequirePlayMode();var player=Object.FindFirstObjectByType<SumiPlayer>();var run=Object.FindFirstObjectByType<SumiRunDirector>();
        EnsureArrowWave(run);new GameObject("Authoring arrow volley").AddComponent<SumiArrowStrike>().Init(player,run);
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
        var player=Object.FindFirstObjectByType<SumiPlayer>();var run=Object.FindFirstObjectByType<SumiRunDirector>();EnsureArrowWave(run);
        foreach(var old in Object.FindObjectsByType<SumiArrowStrike>(FindObjectsSortMode.None))Object.DestroyImmediate(old.gameObject);
        var volley=new GameObject("Verified arrow volley").AddComponent<SumiArrowStrike>();volley.Init(player,run);
        var tuning=player.config.arrowVolley;int expected=tuning.waveCount*tuning.gameplayArrowsPerWave;
        Check(volley.PredictedGameplayLandings.Count==expected,"gameplay landing count is committed from wave tuning");
        var committed=new Vector3[expected];for(int i=0;i<expected;i++)committed[i]=volley.PredictedGameplayLandings[i];
        Check(Object.FindObjectsByType<SumiArrowStrike>(FindObjectsSortMode.None).Length==1,"one controller owns the full barrage");
        var seen=new HashSet<SumiVolleyState>();float until=Time.time+tuning.LastImpactAt+.12f;
        while(volley&&Time.time<until){seen.Add(volley.State);yield return null;}
        Check(volley&&seen.Contains(SumiVolleyState.Telegraph)&&seen.Contains(SumiVolleyState.Ascending)&&seen.Contains(SumiVolleyState.Apex)&&seen.Contains(SumiVolleyState.Descending),"timeline exposes anticipation, ascent, apex and descent");
        bool unchanged=volley&&volley.PredictedGameplayLandings.Count==committed.Length;
        if(unchanged)for(int i=0;i<committed.Length;i++)if(volley.PredictedGameplayLandings[i]!=committed[i]){unchanged=false;break;}
        Check(unchanged,"landing positions never home or re-randomize during flight");
        Check(GameObject.Find("Pooled embedded battlefield arrows")!=null,"landed arrows convert into the capped embedded field");
        until=Time.time+tuning.impactDuration+tuning.recoveryDuration+.3f;while(volley&&Time.time<until)yield return null;
        Check(!volley,"volley controller cleans up after recovery while embedded arrows remain pooled");
    }

    static void EnsureArrowWave(SumiRunDirector run)
    {
        if(run.state!=SumiRunState.Wave)run.GetType().GetMethod("BeginWave",Private).Invoke(run,new object[]{3});
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
