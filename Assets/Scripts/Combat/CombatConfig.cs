using System;
using HavenDuel.Pokemon;
using UnityEngine;

namespace HavenDuel.Combat
{
    /// Tunables and asset references for fights. One asset: Assets/Data/CombatConfig.asset
    /// (rebuilt by HavenDuel > Build Combat Assets).
    [CreateAssetMenu(menuName = "HavenDuel/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Serializable]
        public class MoveDef
        {
            public string name = "Move";
            [Tooltip("Damage per hit.")] public float damage = 8f;
            [Tooltip("Projectile speed, m/s.")] public float speed = 35f;
            [Tooltip("Downward pull on the projectile, m/s².")] public float gravity;
            [Tooltip("Projectile collision radius, m.")] public float radius = 0.15f;
            [Tooltip("Seconds between throws.")] public float cooldown = 0.3f;
            [Tooltip("Projectile is removed after this many seconds.")] public float life = 2.5f;
            [Tooltip("Visual scale of the projectile prefab.")] public float visualScale = 1f;
            [Tooltip("Speed of the attack animation (the clips are long).")] public float animSpeed = 3f;
            public GameObject projectile;
            public GameObject hitFx;
            public AudioClip castSound;
            public AudioClip hitSound;
            public AudioClip wallSound;
            [Range(0f, 1f)] public float castVolume = 0.6f;
        }

        [Serializable]
        public class FighterDef
        {
            public Species species;
            public string displayName;
            public float maxHp = 100f;
            [Tooltip("Model height, m. Charizard is the big target.")] public float height = 1.8f;
            [Tooltip("Hurtbox radius, m (independent of the movement capsule).")] public float hitRadius = 0.4f;
            public MoveDef basic = new();
            public Sprite portrait;
            public AudioClip cry;
            public Color color = Color.white;
        }

        public FighterDef[] fighters = Array.Empty<FighterDef>();

        [Header("Round")]
        public float roundTime = 120f;
        public float countdown = 3f;
        public float roundOverTime = 4.5f;

        [Header("HUD")]
        public TMPro.TMP_FontAsset font;
        public Sprite crosshair;
        public Sprite hitmarker;
        public Sprite panel;

        [Header("Sounds")]
        public AudioClip hitmarkerSound;
        public AudioClip countdownSound;
        public AudioClip fightSound;
        public AudioClip koSound;
        public AudioClip winSound;
        public AudioClip loseSound;

        public FighterDef Get(Species s) => Array.Find(fighters, f => f.species == s);
    }
}
