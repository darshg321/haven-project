using UnityEngine;

namespace HavenDuel.Pokemon
{
    /// Sits on the root of a spawned Pokemon model. Scales the imported mesh to its
    /// in-game height, puts its feet on the root's origin and faces it along +Z.
    public class PokemonModel : MonoBehaviour
    {
        public Species species;
        [Tooltip("Visual height in metres after scaling.")]
        public float height = 1f;
        [Tooltip("Extra yaw if the source model doesn't face +Z.")]
        public float yawOffset;
        [Tooltip("Lift off the ground, for floaters like Mimikyu.")]
        public float hover;

        public Transform Visual { get; private set; }

        /// Wraps an instantiated glb under this transform and fits it.
        public void Fit(Transform visual)
        {
            Visual = visual;
            visual.SetParent(transform, false);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            visual.localScale = Vector3.one;

            // Bind poses are often nothing like the in-game pose (Eevee's lies on its side),
            // so measure the first frame of idle instead.
            var anim = visual.GetComponentInChildren<Animation>(true);
            if (anim)
            {
                var clip = anim.GetClip("battle_idle") ?? anim.GetClip("idle");
                if (clip) clip.SampleAnimation(anim.gameObject, 0f);
            }

            var b = Bounds(visual);
            if (b.size.y > 1e-4f)
            {
                float k = height / b.size.y;
                visual.localScale = Vector3.one * k;
                b = Bounds(visual);
            }
            var feet = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
            visual.localPosition = -feet + Vector3.up * hover;
        }

        static Mesh scratch;

        /// World bounds of the posed mesh. Skinned renderer bounds lag a frame behind, so bake.
        public static Bounds MeshBounds(Transform t) => Bounds(t);

        static Bounds Bounds(Transform t)
        {
            bool any = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                Bounds local; Matrix4x4 m;
                if (r is SkinnedMeshRenderer s && s.sharedMesh)
                {
                    if (!scratch) scratch = new Mesh();
                    s.BakeMesh(scratch, true);
                    scratch.RecalculateBounds();
                    local = scratch.bounds; m = s.transform.localToWorldMatrix;
                }
                else if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh)
                { local = f.sharedMesh.bounds; m = r.transform.localToWorldMatrix; }
                else continue;
                for (int i = 0; i < 8; i++)
                {
                    var c = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2f - 1));
                    var w = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
                }
            }
            return b;
        }
    }
}
