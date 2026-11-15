using System;
using UnityEngine;

namespace HavenDuel
{
    public enum Species { Greninja, Charizard }

    [CreateAssetMenu(menuName = "HavenDuel/Pokemon Roster")]
    public class PokemonRoster : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Species species;
            public GameObject model;
            public float height = 1f;
            public float runSpeed = 4f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public PokemonAnimator Spawn(Species species, Vector3 pos, Quaternion rot, float height)
        {
            var entry = Array.Find(entries, e => e.species == species);
            var root = new GameObject(species.ToString());
            root.transform.SetPositionAndRotation(pos, rot);

            var model = Instantiate(entry.model, root.transform).transform;
            Fit(root.transform, model, height);

            var anim = root.AddComponent<PokemonAnimator>();
            anim.runSpeed = entry.runSpeed;
            return anim;
        }

        static void Fit(Transform root, Transform model, float height)
        {
            var anim = model.GetComponentInChildren<Animation>(true);
            var idle = anim ? anim.GetClip("battle_idle") ?? anim.GetClip("idle") : null;
            if (idle) idle.SampleAnimation(anim.gameObject, 0f);

            var b = MeshBounds(model);
            if (b.size.y > 0.0001f)
            {
                model.localScale = Vector3.one * (height / b.size.y);
                b = MeshBounds(model);
            }
            model.localPosition = -root.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
        }

        static Mesh baked;

        public static Bounds MeshBounds(Transform t)
        {
            bool any = false;
            var bounds = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                Bounds local;
                if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                {
                    if (!baked) baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    baked.RecalculateBounds();
                    local = baked.bounds;
                }
                else if (r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                    local = filter.sharedMesh.bounds;
                else
                    continue;

                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
                    var world = r.transform.TransformPoint(corner);
                    if (!any) bounds = new Bounds(world, Vector3.zero);
                    else bounds.Encapsulate(world);
                    any = true;
                }
            }
            return bounds;
        }
    }
}
