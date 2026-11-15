using System;
using UnityEngine;

namespace HavenDuel.Pokemon
{
    /// Model prefab and fitting values for each species. Create one asset
    /// (HavenDuel/Pokemon Roster) and use Spawn() wherever a Pokemon appears.
    [CreateAssetMenu(menuName = "HavenDuel/Pokemon Roster")]
    public class PokemonRoster : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Species species;
            public GameObject model;
            public float height = 1f;
            public float yawOffset;
            public float hover;
            public float runSpeed = 4f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry Get(Species s) => Array.Find(entries, e => e.species == s);

        public PokemonAnimator Spawn(Species s, Vector3 pos, Quaternion rot, Transform parent = null, float heightOverride = 0f)
        {
            var e = Get(s);
            if (e == null || !e.model) { Debug.LogError($"No model for {s}"); return null; }
            var root = new GameObject(s.ToString());
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            var pm = root.AddComponent<PokemonModel>();
            pm.species = s; pm.height = heightOverride > 0f ? heightOverride : e.height; pm.yawOffset = e.yawOffset; pm.hover = e.hover;
            pm.Fit(Instantiate(e.model).transform);
            var an = root.AddComponent<PokemonAnimator>();
            an.runSpeed = e.runSpeed;
            return an;
        }
    }
}
