using System;
using UnityEngine;
namespace Sumi
{
    public static class Identity { public const string Title = "Sumi"; public const string Subtitle = "THE NIGHT REMEMBERS"; }

    [Serializable]
    public sealed class SumiArrowVolleyTuning
    {
        [Header("Timing")]
        [Min(0)] public float telegraphDuration=.48f;
        [Min(0)] public float launchDelay=.12f;
        [Min(.1f)] public float flightDuration=1.05f;
        [Min(0)] public float apexHold=.24f;
        [Min(.1f)] public float descentDuration=.78f;
        [Min(0)] public float impactDuration=.42f;
        [Min(0)] public float recoveryDuration=.55f;

        [Header("Trajectory")]
        public Vector3 volleyDirection=new Vector3(0,0,1);
        [Min(5)] public float launchDistance=34f;
        public float launchHeight=1.6f;
        [Min(2)] public float trajectoryHeight=16f;
        [Min(0)] public float trajectoryHeightVariance=2.2f;
        [Min(0)] public float formationWidth=12f;
        [Min(0)] public float formationDepth=3.5f;
        [Min(0)] public float sideDrift=1.6f;
        public Vector3 targetCenterOffset=Vector3.zero;
        [Min(0)] public float targetLead=.32f;

        [Header("Waves")]
        [Range(1,8)] public int waveCount=3;
        [Range(1,64)] public int gameplayArrowsPerWave=12;
        [Range(0,256)] public int visualArrowsPerWave=48;
        [Min(0)] public float timeBetweenWaves=.20f;
        [Range(.05f,1.5f)] public float waveDuration=.34f;
        [Range(0,1)] public float delayVariance=.10f;

        [Header("Spread")]
        [Min(1)] public float targetRadius=5.6f;
        [Range(1,8)] public int clusterCount=4;
        [Range(0,1)] public float clusterStrength=.72f;
        [Min(0)] public float clusterRadius=1.35f;
        [Min(0)] public float minimumGameplaySpacing=.42f;

        [Header("Telegraph")]
        [Min(.01f)] public float coverageLineWidth=.045f;
        [Min(.05f)] public float impactMarkerRadius=.34f;
        [Range(0,1)] public float exactMarkerReveal=.43f;
        [Range(0,1)] public float coverageUrgency=.85f;

        [Header("Gameplay Arrows")]
        [Min(0)] public float damage=11f;
        [Min(.05f)] public float damageRadius=.78f;
        [Min(0)] public float nearMissRadius=1.45f;
        [Range(0,1)] public float nearMissChance=.22f;

        [Header("Visual Arrows")]
        [Min(.1f)] public float arrowLength=1.15f;
        [Min(.005f)] public float arrowWidth=.035f;
        public bool castArrowShadows=true;

        [Header("Camera")]
        [Range(0,1)] public float attentionStrength=.32f;
        [Min(0)] public float attentionDuration=1.25f;
        [Min(0)] public float nearbyImpactRadius=7f;
        [Range(0,.5f)] public float maxImpactKick=.13f;

        [Header("Impact")]
        [Min(0)] public float embeddedArrowLifetime=8f;
        [Range(0,256)] public int maxEmbeddedArrows=72;
        [Range(0,1)] public float embedChance=.72f;

        [Header("Audio Hooks")]
        public AudioClip distantRelease;
        public AudioClip highWhistleLoop;
        public AudioClip descentWhistleLoop;
        public AudioClip nearMiss;
        public AudioClip impact;
        [Range(0,1)] public float volleyVolume=.72f;
        [Range(0,1)] public float impactVolume=.58f;

        [Header("Performance")]
        [Range(16,1023)] public int instancingBatchSize=512;
        [Min(.02f)] public float impactAudioInterval=.065f;

        public float FirstLaunchAt=>telegraphDuration+launchDelay;
        public float LastLaunchAt=>FirstLaunchAt+Mathf.Max(0,waveCount-1)*(waveDuration+timeBetweenWaves)+waveDuration+delayVariance;
        public float LastImpactAt=>LastLaunchAt+flightDuration+apexHold+descentDuration;
        public float TotalDuration=>LastImpactAt+impactDuration+recoveryDuration;
    }

    [CreateAssetMenu(menuName="Sumi/Combat tuning")]
    public class SumiConfig : ScriptableObject
    {
        public float moveSpeed=2.15f, acceleration=8.5f, dodgeSpeed=9, dodgeDuration=.34f, dodgeCooldown=.72f;
        public float parryStartup=.025f, perfectWindow=.16f, parryWindow=.34f, parryRecovery=.55f;
        public float lightDamage=18, finishingDamage=27, perfectPosture=34, normalPosture=16;
        public float normalGold=10, perfectGold=22, damageGoldLoss=20;
        public float[] swingDuration={.57f,.60f,.78f};
        public float[] activeStart={.16f,.20f,.28f};
        public float[] activeEnd={.30f,.34f,.44f};
        public float goldenDuration=3.8f;
        [Header("Battlefield Arrow Volley")]
        public SumiArrowVolleyTuning arrowVolley=new SumiArrowVolleyTuning();
    }
}
