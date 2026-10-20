using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Hechicero oscuro (enemigo a distancia).
    /// - Te detecta, se acerca hasta su distancia de ataque y lanza bolas de fuego espectral.
    /// - Recibe daño (parpadea y retrocede), muere desvaneciéndose en humo azul y suelta su botín.
    /// - Para pruebas, reaparece en su posición inicial tras unos segundos.
    /// Los sprites base miran a la IZQUIERDA.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(SpriteFrameAnimator))]
    public class SorcererEnemy : MonoBehaviour, IDamageable
    {
        [Header("Vida")]
        // Vida máxima y segundos hasta reaparecer después de morir.
        public float maxHealth = 80f;
        public float respawnDelay = 5f;

        [Header("Comportamiento")]
        // Distancia a la que te ve, distancia desde la que ataca y velocidad al caminar.
        public float detectRange = 8f;
        public float attackRange = 4.5f;
        public float moveSpeed = 1.6f;

        [Header("Ataque")]
        // Tiempo cargando el hechizo antes de lanzarlo, espera entre ataques, daño y color de la magia.
        public float castWindup = 0.6f;
        public float castCooldown = 2.2f;
        public float projectileDamage = 10f;
        public Color magicColor = new Color(0.55f, 0.8f, 1f, 1f);

        // Estados posibles: quieto, persiguiendo, lanzando hechizo, herido y muerto.
        enum State { Idle, Chase, Casting, Hurt, Dead }

        // Estado interno: estado actual, componentes, vida, temporizadores, hacia dónde mira y posición inicial.
        State state = State.Idle;
        Rigidbody2D rb;
        SpriteRenderer sr;
        SpriteFrameAnimator anim;
        Collider2D col;
        float health;
        float timer;
        float cooldown;
        bool facingRight;
        Vector3 spawnPos;
        float flashTimer;
        bool cast;

        // Números de daño flotantes.
        class Popup { public string text; public Vector3 pos; public float t; public bool heavy; }
        readonly List<Popup> popups = new List<Popup>();

        // Partículas de humo al morir (la imagen se genera una vez y se comparte).
        class Smoke { public Transform tr; public SpriteRenderer sr; public Vector2 vel; public float t; }
        readonly List<Smoke> smoke = new List<Smoke>();
        static Sprite smokeSprite;

        // Atajo para leer o cambiar la velocidad del cuerpo físico.
        Vector2 Velocity
        {
            get => rb.linearVelocity;
            set => rb.linearVelocity = value;
        }

        // Al crearse: guarda los componentes, llena la vida y recuerda dónde apareció.
        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            anim = GetComponent<SpriteFrameAnimator>();
            col = GetComponent<Collider2D>();
            health = maxHealth;
            spawnPos = transform.position;
        }

        // Al empezar el juego.
        void Start()
        {
            // El jugador y el hechicero no se empujan entre sí
            var player = PlayerController.Instance;
            if (player != null && col != null)
                foreach (var pc in player.GetComponents<Collider2D>()) Physics2D.IgnoreCollision(col, pc);
            // Empieza con la animación de reposo.
            anim.Play("Idle");
        }

        // Cada frame: efectos, espera entre ataques y lo que toque según su estado.
        void Update()
        {
            float dt = Time.deltaTime;
            UpdateEffects(dt);
            if (cooldown > 0f) cooldown -= dt;

            // Busca al jugador del prototipo lateral (PlayerController); en la escena isométrica no lo encuentra.
            var player = PlayerController.Instance;

            switch (state)
            {
                // Quieto o persiguiendo: decide si moverse o atacar.
                case State.Idle:
                case State.Chase:
                    UpdateMovement(player);
                    break;

                // Lanzando hechizo: se queda quieto cargando y, al terminar la carga, dispara la bola.
                case State.Casting:
                    timer += dt;
                    Velocity = new Vector2(0f, Velocity.y);
                    // Destello del bastón durante la carga
                    sr.color = Color.Lerp(Color.white, magicColor, Mathf.PingPong(timer * 6f, 1f) * 0.6f);
                    // Una sola vez por ataque: crea la bola a la altura del bastón, hacia donde mira.
                    if (!cast && timer >= castWindup)
                    {
                        cast = true;
                        Vector2 dir = facingRight ? Vector2.right : Vector2.left;
                        Vector3 orb = transform.position + new Vector3(dir.x * 0.5f, 1.6f, 0f);
                        MagicProjectile.Spawn(orb, dir, projectileDamage, magicColor);
                    }
                    // Poco después de disparar vuelve a perseguir y empieza la espera entre ataques.
                    if (timer >= castWindup + 0.35f)
                    {
                        sr.color = Color.white;
                        cooldown = castCooldown;
                        state = State.Chase;
                    }
                    break;

                // Herido: un cuarto de segundo aturdido y vuelve a perseguir.
                case State.Hurt:
                    timer += dt;
                    if (timer >= 0.25f) state = State.Chase;
                    break;

                // Muerto: se desvanece y, pasado el tiempo, reaparece.
                case State.Dead:
                    timer += dt;
                    sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - timer / 0.6f));
                    if (timer >= respawnDelay) Respawn();
                    break;
            }
        }

        // Decide moverse o atacar según la distancia al jugador (solo en horizontal).
        void UpdateMovement(PlayerController player)
        {
            // Si no hay jugador se queda quieto.
            if (player == null) { Velocity = new Vector2(0f, Velocity.y); anim.Play("Idle"); return; }

            // Distancia horizontal al jugador; se gira para mirarlo.
            float dx = player.transform.position.x - transform.position.x;
            float dist = Mathf.Abs(dx);
            SetFacing(dx > 0f);

            // Fuera de su rango de visión: se queda quieto.
            if (dist > detectRange)
            {
                state = State.Idle;
                Velocity = new Vector2(0f, Velocity.y);
                anim.Play("Idle");
                return;
            }

            state = State.Chase;
            // Lejos: camina hacia el jugador. Cerca: se para y, si ya puede, empieza a lanzar.
            if (dist > attackRange)
            {
                Velocity = new Vector2(Mathf.Sign(dx) * moveSpeed, Velocity.y);
                anim.Play("Walk");
            }
            else
            {
                Velocity = new Vector2(0f, Velocity.y);
                anim.Play("Idle");
                if (cooldown <= 0f)
                {
                    state = State.Casting;
                    timer = 0f;
                    cast = false;
                    anim.Play("Attack", true);
                }
            }
        }

        // Recibe un golpe (cumple el contrato IDamageable): resta vida, parpadea y muestra el número.
        public void TakeDamage(float amount, Vector2 knockback, bool heavy)
        {
            if (state == State.Dead) return;
            health -= amount;
            flashTimer = 0.1f;
            popups.Add(new Popup
            {
                text = Mathf.RoundToInt(amount).ToString(),
                pos = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 2f, 0f),
                heavy = heavy
            });

            // Sin vida: muere.
            if (health <= 0f) { Die(); return; }

            // Los golpes fuertes interrumpen el lanzamiento
            if (heavy || state != State.Casting)
            {
                sr.color = Color.white;
                state = State.Hurt;
                timer = 0f;
                Velocity = new Vector2(knockback.x * 0.6f, Velocity.y);
            }
        }

        // Muere: se para, deja de tener física, animación de muerte, humo y botín.
        void Die()
        {
            state = State.Dead;
            timer = 0f;
            health = 0f;
            Velocity = Vector2.zero;
            rb.simulated = false;
            anim.Play("Death", true);
            SpawnSmoke(14);
            GetComponent<LootDropper>()?.Drop();
        }

        // Reaparece en su sitio inicial con la vida llena.
        void Respawn()
        {
            transform.position = spawnPos;
            health = maxHealth;
            rb.simulated = true;
            sr.color = Color.white;
            state = State.Idle;
            cooldown = 1f;
            anim.Play("Idle", true);
        }

        // Gira el sprite para mirar a la derecha o a la izquierda.
        void SetFacing(bool right)
        {
            facingRight = right;
            sr.flipX = right; // sprites base miran a la izquierda
        }

        // ---------------- Efectos ----------------

        // Actualiza destello rojo, números de daño y humo.
        void UpdateEffects(float dt)
        {
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (state != State.Dead) sr.color = flashTimer > 0f ? new Color(1f, 0.55f, 0.55f) : Color.white;
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].t += dt;
                popups[i].pos += Vector3.up * dt * 1.2f;
                if (popups[i].t > 0.8f) popups.RemoveAt(i);
            }

            // El humo sube, crece y se desvanece en 0.8 segundos.
            for (int i = smoke.Count - 1; i >= 0; i--)
            {
                var s = smoke[i];
                s.t += dt;
                s.tr.position += (Vector3)(s.vel * dt);
                s.tr.localScale = Vector3.one * (1f + s.t * 1.5f);
                s.sr.color = new Color(magicColor.r, magicColor.g, magicColor.b, Mathf.Clamp01(0.8f - s.t));
                if (s.t > 0.8f) { Destroy(s.tr.gameObject); smoke.RemoveAt(i); }
            }
        }

        // Crea partículas de humo alrededor del hechicero.
        void SpawnSmoke(int count)
        {
            // La imagen del humo es un cuadrado blanco de 3x3 píxeles (se tiñe con el color de la magia).
            if (smokeSprite == null)
            {
                var tex = new Texture2D(3, 3) { filterMode = FilterMode.Point };
                for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, y, Color.white);
                tex.Apply();
                smokeSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 32f);
            }

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Humo");
                go.transform.position = transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.2f, 1.8f), 0f);
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = smokeSprite;
                s.sortingOrder = 14;
                smoke.Add(new Smoke { tr = go.transform, sr = s, vel = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 1.8f)) });
            }
        }

        // Dibuja la barra de vida azul y los números de daño encima del hechicero.
        void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (state != State.Dead)
            {
                Vector3 top = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.3f);
                float w = 70f, x = top.x - w / 2f, y = Screen.height - top.y;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, 9), Texture2D.whiteTexture);
                GUI.color = new Color(0.35f, 0.6f, 1f);
                GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(health / maxHealth), 7), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            foreach (var p in popups)
            {
                Vector3 s = cam.WorldToScreenPoint(p.pos);
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = p.heavy ? 26 : 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = p.heavy ? new Color(0.8f, 0.45f, 1f) : Color.white;
                GUI.Label(new Rect(s.x - 50, Screen.height - s.y - 15, 100, 30), p.text, style);
            }
        }

        // En el editor, al seleccionarlo, dibuja sus rangos: amarillo = visión, celeste = ataque.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
