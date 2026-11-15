using System.Collections.Generic;
using UnityEngine;

namespace HavenDuel
{
    public class Hands : MonoBehaviour
    {
        Fighter owner;
        Transform left, right;
        readonly Vector3 leftRest = new(-0.36f, -0.25f, 0.42f);
        readonly Vector3 rightRest = new(0.36f, -0.25f, 0.42f);
        float bob, punch;
        bool punchRight;

        public static void Attach(Transform cam, Fighter owner)
        {
            var hands = cam.gameObject.AddComponent<Hands>();
            hands.owner = owner;
            var a = hands.Cut("left", out float ax);
            var b = hands.Cut("right", out float bx);
            // Bone names don't say which side of the screen a hand ends up on, so
            // place each piece by where it actually sits after orientation.
            // The right hand comes out posed correctly, the left one doesn't (the idle
            // pose is lopsided), so the left hand is a mirror copy of the right.
            var good = ax <= bx ? b : a;
            Destroy((ax <= bx ? a : b).gameObject);
            hands.right = good;
            hands.left = Instantiate(good.gameObject, good.parent, false).transform;
            hands.left.name = "left hand";
            var ls = good.localScale;
            hands.left.localScale = new Vector3(-ls.x, ls.y, ls.z);
            var e = good.localEulerAngles;
            hands.left.localRotation = Quaternion.Euler(e.x, -e.y, -e.z);
            Fighter.Fired += hands.OnFired;
        }

        void OnDestroy() => Fighter.Fired -= OnFired;

        void OnFired(Fighter f, int move, Vector3 origin, Vector3 dir)
        {
            if (f != owner) return;
            punch = 1f;
            punchRight = !punchRight;
        }

        Transform Cut(string side, out float screenX)
        {
            var piece = new GameObject(side + " hand").transform;
            piece.SetParent(transform, false);
            piece.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            piece.localScale = Vector3.one * 0.6f;

            var parts = new List<(SkinnedMeshRenderer renderer, Mesh mesh, bool[] keep)>();
            var sum = Vector3.zero;
            int count = 0;
            foreach (var skin in owner.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var handBones = new HashSet<int>();
                for (int b = 0; b < skin.bones.Length; b++)
                    if (InHand(skin.bones[b], side)) handBones.Add(b);
                if (handBones.Count == 0) continue;

                var weights = skin.sharedMesh.boneWeights;
                var mesh = new Mesh();
                skin.BakeMesh(mesh, true);
                var verts = mesh.vertices;
                var keep = new bool[verts.Length];
                var toRoot = owner.transform.worldToLocalMatrix * Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                for (int v = 0; v < verts.Length; v++)
                {
                    verts[v] = toRoot.MultiplyPoint3x4(verts[v]);
                    keep[v] = handBones.Contains(weights[v].boneIndex0) && weights[v].weight0 > 0.5f;
                    if (!keep[v]) continue;
                    sum += verts[v];
                    count++;
                }
                mesh.vertices = verts;
                parts.Add((skin, mesh, keep));
            }

            var center = count > 0 ? sum / count : Vector3.zero;

            // Fingers should point away from the camera. If the wrist sits in front of
            // the hand's middle, the model faces backwards, so turn the piece around.
            bool flip = false;
            var wrist = FindBone(owner.transform, side + "_hand");
            if (wrist && count > 0)
                flip = (center - owner.transform.InverseTransformPoint(wrist.position)).z < 0f;
            piece.localRotation = Quaternion.Euler(-15f, flip ? 180f : 0f, 0f);
            screenX = flip ? -center.x : center.x;
            foreach (var (skin, mesh, keep) in parts)
            {
                var verts = mesh.vertices;
                for (int v = 0; v < verts.Length; v++) verts[v] -= center;
                mesh.vertices = verts;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var tris = mesh.GetTriangles(s);
                    var kept = new List<int>();
                    for (int t = 0; t < tris.Length; t += 3)
                        if (keep[tris[t]] && keep[tris[t + 1]] && keep[tris[t + 2]])
                            kept.AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
                    mesh.SetTriangles(kept, s);
                }
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject(skin.name);
                go.transform.SetParent(piece, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = skin.sharedMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return piece;
        }

        static Transform FindBone(Transform root, string prefix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith(prefix)) return t;
            return null;
        }

        static bool InHand(Transform bone, string side)
        {
            for (var t = bone; t; t = t.parent)
                if (t.name.StartsWith(side + "_hand")) return true;
            return false;
        }

        void LateUpdate()
        {
            left.gameObject.SetActive(owner.Alive);
            right.gameObject.SetActive(owner.Alive);

            var v = owner.motor.Velocity;
            float speed = owner.motor.Grounded ? new Vector2(v.x, v.z).magnitude : 0f;
            bob += Time.deltaTime * (4f + speed * 1.6f);
            float amount = Mathf.Min(speed, 8f) * 0.004f;
            var sway = new Vector3(Mathf.Cos(bob) * amount, Mathf.Abs(Mathf.Sin(bob)) * amount, 0f);

            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 6f);
            var thrust = new Vector3(0f, 0.03f, 0.16f) * Mathf.Sin(punch * Mathf.PI);
            left.localPosition = leftRest + sway + (punchRight ? Vector3.zero : thrust);
            right.localPosition = rightRest + new Vector3(-sway.x, sway.y, 0f) + (punchRight ? thrust : Vector3.zero);
        }
    }
}
