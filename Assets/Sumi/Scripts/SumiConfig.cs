using UnityEngine;
namespace Sumi
{
    public static class Identity { public const string Title = "Sumi"; public const string Subtitle = "THE NIGHT REMEMBERS"; }
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
    }
}
