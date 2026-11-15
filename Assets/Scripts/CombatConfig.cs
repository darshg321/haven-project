using System;
using UnityEngine;

namespace HavenDuel
{
    [CreateAssetMenu(menuName = "HavenDuel/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Serializable]
        public class MoveDef
        {
            public string name = "Move";
            public Sprite icon;
            public float damage = 8f;
            public float speed = 35f;
            public float gravity;
            public float radius = 0.15f;
            public float cooldown = 0.3f;
            public int charges = 1;
            public int count = 1;
            public float spread;
            public float homing;
            public int clones;
            public float recharge;
            public float life = 2.5f;
            [Tooltip("Seconds the move keeps firing as a stream (0 = single shot).")]
            public float stream;
            public float streamInterval = 0.06f;
            public float visualScale = 1f;
            public float animSpeed = 3f;
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
            public float height = 1.8f;
            public float hitRadius = 0.4f;
            public MoveDef basic = new();
            public MoveDef[] abilities = Array.Empty<MoveDef>();
            public float ultCost = 120f;
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
        public Font menuFont;
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
