using UnityEngine;

namespace HavenDuel
{
    public class Decoy : MonoBehaviour
    {
        Vector3 velocity;

        public static void Spawn(Transform model, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var clone = Instantiate(model.gameObject, model.position, model.rotation);
                clone.transform.localScale = model.lossyScale;
                foreach (var r in clone.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var anim = clone.GetComponentInChildren<Animation>();
                if (anim.GetClip("run")) anim.Play("run");
                float side = i % 2 == 0 ? 1f : -1f;
                var decoy = clone.AddComponent<Decoy>();
                decoy.velocity = model.right * side * (4f + i) + model.forward * 3f;
                Destroy(clone, 2.5f);
            }
        }

        void Update() => transform.position += velocity * Time.deltaTime;
    }
}
