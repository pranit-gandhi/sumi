using System;
using UnityEngine;
namespace Sumi
{
    public static class Identity { public const string Title = "Sumi"; public const string Subtitle = "THE NIGHT REMEMBERS"; }

    [Serializable]
    public sealed class SumiArrowVolleyTuning
    {
        [Min(1)] public float targetRadius=5.6f;
        [Min(.5f)] public float telegraphDuration=3f;
        [Min(.2f)] public float fallDuration=.65f;
        [Min(.5f)] public float fallStagger=2.5f;
        [Min(0)] public float fadeDuration=1.5f;
        [Range(8,48)] public int arrowCount=28;
        [Min(0)] public float damage=8f;
        [Min(.5f)] public float dwellInterval=1.5f;
        [Min(8)] public float spawnHeight=26f;
        [Min(0)] public float centerOffset=2.2f;
        [Min(0)] public float cameraLift=2.6f;
        [Min(1)] public float waveInterval=10f,waveJitter=.7f,bossInterval=8.5f,bossJitter=.6f;
        public Color WarningColor=new Color(.42f,.045f,.07f,1);
        public float FirstLandAt=>telegraphDuration+fallDuration;
        public float LastLandAt=>FirstLandAt+fallStagger;
        public float TotalDuration=>LastLandAt+fadeDuration;
        public float NextDelay(bool boss)
        {
            float span=boss?bossInterval:waveInterval,jitter=boss?bossJitter:waveJitter;
            return span+UnityEngine.Random.Range(-jitter,jitter);
        }
    }

    [Serializable]
    public sealed class SumiDeathTuning
    {
        [Header("Hit-stop")]
        [Min(0)] public float enemyFatalHitstop=.055f;
        [Min(0)] public float playerFatalHitstop=.10f;
        [Min(0)] public float bossFatalHitstop=.08f;
        [Min(0)] public float heavyFatalHitstopBonus=.018f;
        [Header("Camera")]
        [Min(0)] public float enemyDeathCameraImpulse=.38f;
        [Min(0)] public float playerDeathCameraImpulse=.58f;
        [Min(0)] public float bodyLandImpulse=.12f;
        [Header("Time")]
        [Range(.05f,1)] public float playerDeathSlowmo=.46f;
        [Min(0)] public float playerSlowmoHold=.24f;
        [Min(0)] public float playerSlowmoFade=.52f;
        [Range(.05f,1)] public float enemyDeathSlowmo=.48f;
        [Min(0)] public float enemyDeathSlowmoHold=.14f;
        [Min(0)] public float enemyDeathSlowmoFade=.32f;
        [Range(.05f,1)] public float parrySlowmo=.58f;
        [Min(0)] public float parrySlowmoHold=.05f;
        [Min(0)] public float parrySlowmoFade=.16f;
        [Header("Body")]
        [Min(0)] public float deathPoseHold=.12f;
        [Min(0)] public float playerRealization=.34f;
        [Min(0)] public float enemyRagdollDelay=.18f;
        [Min(0)] public float bodyImpactStrength=1f;
        [Min(0)] public float weaponDropForce=1.85f;
        [Min(0)] public float enemyWeaponDropDelay=.26f;
        [Min(0)] public float playerWeaponDropDelay=.48f;
        [Min(0)] public float enemyLandDelay=.58f;
        [Min(0)] public float playerLandDelay=.82f;
        [Min(0)] public float enemyVaporDelay=.26f;
        [Min(.2f)] public float enemyVaporDuration=.66f;
        [Header("Ink / particles")]
        [Min(.1f)] public float deathParticleScale=1f;
        [Min(0)] public float inkFadeDelay=.85f;
        [Min(0)] public float inkSpreadDuration=1.65f;
        [Min(0)] public float restartInputDelay=.55f;
        [Min(0)] public float skipRestartHold=.12f;
    }

    [CreateAssetMenu(menuName="Sumi/Combat tuning")]
    public class SumiConfig : ScriptableObject
    {
        public float moveSpeed=2.15f, acceleration=8.5f, dodgeSpeed=9, dodgeDuration=.34f, dodgeCooldown=.72f;
        public float parryStartup=.025f, perfectWindow=.24f, parryWindow=.48f, parryRecovery=.55f;
        public float lightDamage=18, finishingDamage=27, perfectPosture=34, normalPosture=16;
        public float normalGold=10, perfectGold=22, damageGoldLoss=20;
        public float[] swingDuration={.57f,.60f,.78f};
        public float[] activeStart={.16f,.20f,.28f};
        public float[] activeEnd={.30f,.34f,.44f};
        public float goldenDuration=3.8f;
        [Header("Battlefield Arrow Volley")]
        public SumiArrowVolleyTuning arrowVolley=new SumiArrowVolleyTuning();
        [Header("Death feel")]
        public SumiDeathTuning death=new SumiDeathTuning();
    }
}
