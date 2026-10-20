using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Dibuja el arma separada del personaje y la mueve en cada golpe por código.
    /// - Se materializa con un destello al empezar a atacar y desaparece al terminar.
    /// - Cada golpe tiene su arco (ángulo inicial → final) y la estela del tajo sale en el momento del impacto.
    /// Los ángulos están en grados mirando a la DERECHA: 0 = hacia delante, 90 = arriba, -90 = abajo.
    /// </summary>
    public class WeaponVisual : MonoBehaviour
    {
        [System.Serializable]
        public class Swing
        {
            public string label;
            public float startAngle;
            public float endAngle;
            [Tooltip("Desplazamiento de la mano al inicio y al final (x = hacia delante, y = arriba)")]
            public Vector2 startOffset;
            public Vector2 endOffset;
            [Tooltip("Segundos que dura el barrido justo antes del impacto")]
            public float sweepTime = 0.08f;
            public float slashScale = 1f;
            [Tooltip("Aplasta la estela verticalmente (útil para estocadas)")]
            public float slashSquash = 1f;
        }

        public PlayerController player;
        public SpriteRenderer weaponRenderer;
        public SpriteRenderer slashRenderer;

        [Tooltip("Posición de la mano respecto a los pies (x = hacia delante, y = arriba), en unidades")]
        public Vector2 handOffset = new Vector2(0.2f, 0.75f);
        public float fadeOutTime = 0.12f;
        public float slashTime = 0.14f;
        [Tooltip("Escala base de la estela para que llegue a la punta de la espada")]
        public float slashRadius = 1.7f;

        // 1-4 = combo, 5 = ataque cargado
        public Swing[] swings =
        {
            new Swing { label = "Golpe 1: tajo horizontal", startAngle = 150f, endAngle = -20f,
                startOffset = new Vector2(-0.15f, 0.15f), endOffset = new Vector2(0.15f, 0f), slashScale = 1f },
            new Swing { label = "Golpe 2: tajo ascendente", startAngle = -50f, endAngle = 120f,
                startOffset = new Vector2(0.1f, -0.1f), endOffset = new Vector2(0.05f, 0.2f), slashScale = 1f },
            new Swing { label = "Golpe 3: estocada", startAngle = 0f, endAngle = 0f,
                startOffset = new Vector2(-0.25f, 0.05f), endOffset = new Vector2(0.35f, 0.05f), sweepTime = 0.06f, slashScale = 1f, slashSquash = 0.3f },
            new Swing { label = "Golpe 4: golpe fuerte", startAngle = 110f, endAngle = -70f,
                startOffset = new Vector2(-0.05f, 0.45f), endOffset = new Vector2(0.25f, -0.1f), sweepTime = 0.1f, slashScale = 1.35f },
            new Swing { label = "Ataque cargado", startAngle = 170f, endAngle = -10f,
                startOffset = new Vector2(-0.35f, 0.2f), endOffset = new Vector2(0.4f, 0.05f), sweepTime = 0.09f, slashScale = 1.7f },
        };

        WeaponDefinition weapon;
        Swing current;
        float timer;
        float duration;
        float hitTime;
        float materializeTimer;
        float slashTimer;
        bool active;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (slashRenderer != null && slashRenderer.sprite == null)
                slashRenderer.sprite = CreateSlashSprite();
        }

        void OnEnable()
        {
            if (player == null) return;
            player.AttackStarted += OnAttackStarted;
            player.AttackEnded += OnAttackEnded;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.AttackStarted -= OnAttackStarted;
            player.AttackEnded -= OnAttackEnded;
        }

        void Start()
        {
            ApplyWeapon(player != null ? player.weapon : null);
        }

        public void ApplyWeapon(WeaponDefinition def)
        {
            weapon = def;
            if (weaponRenderer != null)
            {
                weaponRenderer.sprite = def != null ? def.sprite : null;
                weaponRenderer.enabled = def != null && def.visibleWhenIdle;
            }
            if (slashRenderer != null) slashRenderer.enabled = false;
        }

        void OnAttackStarted(int step, float attackDuration, float attackHitTime)
        {
            if (weapon == null || weaponRenderer == null) return;
            current = swings[Mathf.Clamp(step - 1, 0, swings.Length - 1)];
            duration = attackDuration;
            hitTime = attackHitTime;
            timer = 0f;
            active = true;

            if (!weaponRenderer.enabled) materializeTimer = 0.1f; // destello al aparecer
            weaponRenderer.enabled = true;
        }

        void OnAttackEnded()
        {
            active = false;
            timer = 0f;
        }

        void LateUpdate()
        {
            if (player == null || weaponRenderer == null || weapon == null) return;
            float dt = Time.deltaTime;
            bool right = player.FacingRight;

            if (active)
            {
                timer += dt;

                // Mantiene la pose inicial y barre rápido justo antes del impacto
                float sweepStart = Mathf.Max(0f, hitTime - current.sweepTime);
                float t = Mathf.InverseLerp(sweepStart, hitTime, timer);
                t = 1f - (1f - t) * (1f - t); // ease-out

                float angle = Mathf.Lerp(current.startAngle, current.endAngle, t);
                Vector2 offset = handOffset + Vector2.Lerp(current.startOffset, current.endOffset, t);
                Place(angle, offset, right);

                if (timer >= hitTime && slashTimer <= 0f && timer - dt < hitTime)
                    ShowSlash(right);

                SetAlpha(1f);
            }
            else if (weaponRenderer.enabled && !weapon.visibleWhenIdle)
            {
                // Desaparece poco a poco al terminar el ataque
                timer += dt;
                float a = 1f - Mathf.Clamp01(timer / fadeOutTime);
                SetAlpha(a);
                if (a <= 0f) weaponRenderer.enabled = false;
            }
            else if (weapon.visibleWhenIdle)
            {
                Place(-75f, handOffset, right);
                SetAlpha(1f);
            }

            if (materializeTimer > 0f)
            {
                materializeTimer -= dt;
                weaponRenderer.color = Color.Lerp(weaponRenderer.color, new Color(0.9f, 0.7f, 1f, weaponRenderer.color.a), 0.6f);
            }

            UpdateSlash(dt);
        }

        void Place(float angle, Vector2 offset, bool right)
        {
            float dir = right ? 1f : -1f;
            Transform w = weaponRenderer.transform;
            w.localPosition = new Vector3(offset.x * dir, offset.y, 0f);
            // El sprite apunta hacia arriba (90°): se resta para que 0° sea "hacia delante"
            float z = right ? angle - 90f : 90f - angle;
            w.localRotation = Quaternion.Euler(0f, 0f, z);
            weaponRenderer.flipX = !right;
        }

        void SetAlpha(float a)
        {
            weaponRenderer.color = new Color(1f, 1f, 1f, a);
        }

        void ShowSlash(bool right)
        {
            if (slashRenderer == null) return;
            float dir = right ? 1f : -1f;
            float mid = (current.startAngle + current.endAngle) * 0.5f;
            Transform s = slashRenderer.transform;

            // El pivote de la media luna es su centro: se coloca en la mano y se orienta
            // hacia la mitad del barrido, así el arco cubre el recorrido de la hoja.
            Vector2 hand = handOffset + current.endOffset * 0.5f;
            s.localPosition = new Vector3(hand.x * dir, hand.y, 0f);
            float baseScale = current.slashScale * slashRadius;
            s.localScale = new Vector3(baseScale, baseScale * current.slashSquash, 1f);
            s.localRotation = Quaternion.Euler(0f, 0f, right ? mid : 180f - mid);

            slashRenderer.color = weapon.slashColor;
            slashRenderer.enabled = true;
            slashTimer = slashTime;
        }

        void UpdateSlash(float dt)
        {
            if (slashRenderer == null || slashTimer <= 0f) return;
            slashTimer -= dt;
            float a = Mathf.Clamp01(slashTimer / slashTime);
            var c = weapon.slashColor;
            slashRenderer.color = new Color(c.r, c.g, c.b, c.a * a);
            slashRenderer.transform.localScale *= 1f + dt * 1.5f; // se abre un poco al desvanecerse
            if (slashTimer <= 0f) slashRenderer.enabled = false;
        }

        /// <summary>Genera en tiempo de ejecución una media luna pixelada para la estela del tajo.</summary>
        static Sprite CreateSlashSprite()
        {
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[size * size];
            Vector2 c = new Vector2(size * 0.3f, size * 0.5f);
            float outer = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                    float d = p.magnitude;
                    float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg; // -180..180, 0 = derecha
                    float span = 1f - Mathf.Abs(ang) / 80f;           // arco de ±80°
                    if (span <= 0f) { px[y * size + x] = Color.clear; continue; }

                    float thickness = 2f + 7f * span;                  // más grueso en el centro
                    float inner = outer - thickness;
                    if (d <= outer && d >= inner)
                    {
                        float edge = (d - inner) / thickness;          // 0 dentro, 1 borde exterior
                        float a = edge > 0.7f ? 1f : 0.55f;
                        px[y * size + x] = new Color(1f, 1f, 1f, a * Mathf.Clamp01(span * 2f));
                    }
                    else px[y * size + x] = Color.clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.3f, 0.5f), 32f);
        }
    }
}
