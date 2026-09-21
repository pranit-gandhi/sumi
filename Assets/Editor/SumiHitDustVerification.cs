using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sumi;
using Object=UnityEngine.Object;

// Disposable Play Mode check of accepted-damage presentation; stop after running.
public static class SumiHitDustVerification
{
 static LineRenderer[] Marks()=>Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Pooled calligraphy contact").ToArray();
 static void ResetMarks(){foreach(var r in Marks())r.enabled=false;}
 static bool ColorIs(bool enemy){
  return Marks().Count(r=>r.enabled&&r.sharedMaterial&&
   (enemy?r.sharedMaterial.color.r>.8f&&r.sharedMaterial.color.g<.15f:r.sharedMaterial.color.r>.8f&&r.sharedMaterial.color.g>.5f))==7;
 }
 public static void Run(){
  if(!EditorApplication.isPlaying)throw new InvalidOperationException("Requires Play Mode.");
  var results=new List<string>();var go=new GameObject("Dust verification target");var enemy=go.AddComponent<SumiEnemy>();
  try{
   enemy.health=10000;enemy.state=SumiEnemyState.Recovery;
   var kinds=new[]{SumiHitKind.ShoulderCut,SumiHitKind.ShoulderCut,SumiHitKind.Finisher,SumiHitKind.HeavyCut,SumiHitKind.DashCut,SumiHitKind.Shuriken};
   for(int i=0;i<kinds.Length;i++){
    ResetMarks();enemy.TakeHit(1,Vector3.forward,kinds[i],Vector3.up,SumiSwordArc.Descending,Vector3.right);
    if(!ColorIs(false))throw new Exception("Yellow dust missing for hit "+i+" "+kinds[i]);
    results.Add("PASS yellow dust: "+i+" "+kinds[i]+" while target is already recovering");
   }
   var combat=SumiGame.I.player.combat;
   var grace=typeof(SumiPlayerCombat).GetField("damageGraceUntil",BindingFlags.NonPublic|BindingFlags.Instance);
   enemy.transform.position=combat.transform.position+combat.transform.forward*1.5f;
   combat.health=100;combat.state=SumiCombatState.Free;grace.SetValue(combat,-1f);
   ResetMarks();combat.ReceiveEnemyHit(1,0,enemy,combat.transform.position+Vector3.up);
   if(!ColorIs(true))throw new Exception("Red dust missing for accepted enemy damage");
   results.Add("PASS red dust: enemy damage");
   combat.state=SumiCombatState.GuardHeld;grace.SetValue(combat,-1f);
   ResetMarks();combat.ReceiveEnemyHit(1,0,enemy,combat.transform.position+Vector3.up);
   if(!ColorIs(true))throw new Exception("Red dust missing for guard chip");
   results.Add("PASS red dust: guard chip");
   ResetMarks();combat.ReceiveEnemyHit(1,0,enemy,combat.transform.position+Vector3.up);
   if(Marks().Any(r=>r.enabled))throw new Exception("Rejected grace-period hit emitted effects");
   results.Add("PASS rejected damage creates no dust");
   File.WriteAllText("Logs/hit-dust-verification.txt",string.Join("\n",results));
   Debug.Log("SUMI_HIT_DUST_PASS "+results.Count);
  }finally{Object.DestroyImmediate(go);}
 }
}
