using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Objeto tirado en el suelo. Sale disparado al soltarse, cae, flota y brilla.
    /// Cuando el jugador está cerca aparece "E para recoger".
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();

        public ItemDefinition item;
        public float pickupRadius = 0.9f;

        SpriteRenderer sr;
        SpriteRenderer glow;
        Vector2 velocity;
        float groundY;
        bool landed;
        bool collected;
        float t;
        float collectTimer;

        public bool CanBePicked => landed && !collected;

        /// <summary>Crea un objeto en el suelo con un pequeño salto hacia un lado.</summary>
        public static Pickup Spawn(ItemDefinition item, Vector3 position, float groundY)
        {
            var go = new GameObject("Botin_" + item.displayName);
            go.transform.position = position;
            var p = go.AddComponent<Pickup>();
            p.item = item;
            p.groundY = groundY;
            p.velocity = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(4f, 5.5f));
            return p;
        }

        void Awake()
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;

            var g = new GameObject("Brillo");
            g.transform.SetParent(transform, false);
            glow = g.AddComponent<SpriteRenderer>();
            glow.sortingOrder = 7;
        }

        void Start()
        {
            sr.sprite = item != null ? item.icon : null;
            glow.sprite = sr.sprite;
            glow.transform.localScale = Vector3.one * 1.5f;
            All.Add(this);
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;

            if (collected)
            {
                // Sube hacia el jugador y se desvanece
                collectTimer += dt;
                transform.position += Vector3.up * dt * 2f;
                float a = 1f - collectTimer / 0.25f;
                sr.color = new Color(1f, 1f, 1f, a);
                glow.color = new Color(item.glowColor.r, item.glowColor.g, item.glowColor.b, a * 0.5f);
                if (a <= 0f) Destroy(gameObject);
                return;
            }

            if (!landed)
            {
                velocity.y -= 18f * dt;
                transform.position += (Vector3)(velocity * dt);
                if (transform.position.y <= groundY && velocity.y < 0f)
                {
                    transform.position = new Vector3(transform.position.x, groundY, 0f);
                    landed = true;
                }
            }
            else
            {
                // Flota suavemente
                float bob = Mathf.Sin(t * 3f) * 0.06f;
                sr.transform.localPosition = Vector3.zero;
                transform.position = new Vector3(transform.position.x, groundY + 0.04f + bob, 0f);
            }

            // Brillo que late
            float pulse = 0.25f + Mathf.Sin(t * 5f) * 0.15f;
            var c = item != null ? item.glowColor : Color.white;
            glow.color = new Color(c.r, c.g, c.b, pulse);
        }

        public void Collect()
        {
            collected = true;
            collectTimer = 0f;
        }

        /// <summary>El objeto recogible más cercano a una posición, o null.</summary>
        public static Pickup Closest(Vector2 pos)
        {
            Pickup best = null;
            float bestD = float.MaxValue;
            foreach (var p in All)
            {
                if (p == null || !p.CanBePicked) continue;
                float d = Vector2.Distance(pos, p.transform.position);
                if (d <= p.pickupRadius && d < bestD) { best = p; bestD = d; }
            }
            return best;
        }
    }
}
