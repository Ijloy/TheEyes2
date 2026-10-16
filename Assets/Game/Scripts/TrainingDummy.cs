using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Muñeco de entrenamiento: recibe daño, parpadea, tiembla y muestra números de daño.
    /// Al llegar a 0 de vida muere, suelta su botín (LootDropper) y reaparece a los pocos segundos.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        public float maxHealth = 120f;
        public float respawnDelay = 3f;

        class Popup { public string text; public Vector3 pos; public float t; public bool heavy; }

        float health;
        SpriteRenderer sr;
        Color baseColor;
        float flashTimer;
        float shakeTimer;
        float shakeAmount;
        Vector3 basePos;
        readonly List<Popup> popups = new List<Popup>();
        Collider2D col;
        bool dead;
        float respawnTimer;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
            basePos = transform.position;
            health = maxHealth;
            col = GetComponent<Collider2D>();
        }

        public void TakeDamage(float amount, Vector2 knockback, bool heavy)
        {
            if (dead) return;
            health -= amount;

            flashTimer = 0.12f;
            shakeTimer = heavy ? 0.25f : 0.1f;
            shakeAmount = heavy ? 0.15f : 0.06f;
            popups.Add(new Popup
            {
                text = Mathf.RoundToInt(amount).ToString(),
                pos = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, 0f),
                heavy = heavy
            });

            if (health <= 0f) Die();
        }

        void Die()
        {
            dead = true;
            health = 0f;
            sr.enabled = false;
            if (col != null) col.enabled = false;
            respawnTimer = respawnDelay;
            GetComponent<LootDropper>()?.Drop();
        }

        void Respawn()
        {
            dead = false;
            health = maxHealth;
            sr.enabled = true;
            if (col != null) col.enabled = true;
            flashTimer = 0.2f;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (dead)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f) Respawn();
            }

            if (flashTimer > 0f) flashTimer -= dt;
            sr.color = flashTimer > 0f ? Color.white : baseColor;

            if (shakeTimer > 0f)
            {
                shakeTimer -= dt;
                transform.position = basePos + new Vector3(Random.Range(-shakeAmount, shakeAmount), 0f, 0f);
            }
            else
            {
                transform.position = basePos;
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].t += dt;
                popups[i].pos += Vector3.up * dt * 1.2f;
                if (popups[i].t > 0.8f) popups.RemoveAt(i);
            }
        }

        void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Barra de vida
            if (!dead) DrawHealthBar(cam);

            // Números de daño
            DrawPopups(cam);
        }

        void DrawHealthBar(Camera cam)
        {
            Vector3 top = cam.WorldToScreenPoint(basePos + Vector3.up * 1.4f);
            float w = 80f;
            float x = top.x - w / 2f;
            float y = Screen.height - top.y;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, 10), Texture2D.whiteTexture);
            GUI.color = new Color(0.75f, 0.15f, 0.2f);
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(health / maxHealth), 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawPopups(Camera cam)
        {
            foreach (var p in popups)
            {
                Vector3 s = cam.WorldToScreenPoint(p.pos);
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = p.heavy ? 26 : 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = p.heavy ? new Color(0.8f, 0.45f, 1f) : Color.white;
                GUI.Label(new Rect(s.x - 50, Screen.height - s.y - 15, 100, 30), p.text, style);
            }
        }
    }
}
