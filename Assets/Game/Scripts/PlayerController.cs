using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Prototipo del Caballero Maldito.
    /// - Movimiento lateral, salto y volteo (los sprites base miran a la IZQUIERDA / West).
    /// - Combo de 4 golpes: J encadena golpes; el 4.º es fuerte.
    /// - "Alma en agonía": solo se gana si el 4.º golpe CONECTA con un enemigo (máx. 3).
    /// - Ataque cargado (K): necesita las 3 Almas y las consume.
    /// - Pasiva: al recibir un golpe gana un % de ATK temporal, acumulable hasta 3 veces.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        // Acceso global al jugador (lo usan el hechicero y su bola de fuego).
        public static PlayerController Instance { get; private set; }

        [Header("Vida")]
        public float maxHealth = 100f;

        [Header("Movimiento")]
        // Velocidad al caminar y fuerza del salto.
        public float moveSpeed = 4f;
        public float jumpForce = 11f;

        [Header("Arma equipada")]
        // Arma equipada (datos en un WeaponDefinition).
        public WeaponDefinition weapon;

        [Header("Combate")]
        // Daño base del jugador.
        public float baseAttack = 10f;
        [Tooltip("Multiplicador de daño de cada golpe del combo (1.º al 4.º)")]
        public float[] comboDamage = { 1f, 1f, 1.2f, 2f };
        // Multiplicador del ataque cargado.
        public float chargedDamage = 4f;
        // Posición y tamaño de la caja de golpe delante del jugador.
        public Vector2 hitboxOffset = new Vector2(0.7f, 0.9f);
        public Vector2 hitboxSize = new Vector2(1.2f, 1.2f);

        [Header("Alma en agonía (cargas del combo)")]
        // Máximo de Almas en agonía que se pueden acumular.
        public int maxAlmas = 3;

        [Header("Pasiva: ATK al recibir golpes")]
        // Máximo de acumulaciones, % de ATK por acumulación y cuánto duran.
        public int maxRageStacks = 3;
        [Range(0f, 1f)] public float atkPerStack = 0.10f;
        public float rageDuration = 5f;

        [Header("Recibir golpe")]
        // Segundos aturdido al recibir un golpe.
        public float hurtDuration = 0.3f;

        // Estados posibles: normal, atacando, ataque cargado, herido y recogiendo un objeto.
        enum State { Normal, Attacking, Charged, Hurt, PickingUp }

        // Estado interno: estado actual y componentes.
        State state = State.Normal;
        Rigidbody2D rb;
        Animator anim;
        SpriteRenderer sr;

        // Movimiento: hacia dónde mira, si está en el suelo, aterrizaje y animación actual.
        bool facingRight;
        bool grounded;
        bool wasGrounded = true;
        float landTimer;
        string currentAnim;

        // Combate: golpe del combo, siguiente en cola, si ya golpeó y tiempos del estado.
        int comboStep;
        bool attackQueued;
        bool hitDone;
        float stateTimer;
        float stateDuration;
        float hitTime;

        // Recursos: Almas en agonía y acumulaciones de la pasiva.
        int almas;
        int rageStacks;
        float rageTimer;

        // Efectos y depuración: destello, mensajes en pantalla y última zona de golpe.
        float flashTimer;
        Color baseColor;
        string message;
        float messageTimer;
        Vector2 lastHitCenter;
        Vector2 lastHitSize;
        float hitGizmoTimer;

        // Recoger objetos: inventario y objeto cercano o pendiente.
        PlayerInventory inventory;
        Pickup pendingPickup;
        Pickup nearbyPickup;
        bool pickupGiven;

        // Duración de cada animación, buffers de física y a quién ya se golpeó en este tajo.
        readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        readonly Collider2D[] hitBuffer = new Collider2D[16];
        readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        ContactFilter2D hitFilter;

        // Multiplicador de ATK actual por la pasiva (por ejemplo 1.2 = +20%).
        public float AttackMultiplier => 1f + rageStacks * atkPerStack;
        public bool FacingRight => facingRight;

        /// <summary>Se lanza al empezar un golpe: (paso 1-4 o 5 = cargado, duración, momento del impacto).</summary>
        public event Action<int, float, float> AttackStarted;
        /// <summary>Se lanza cuando termina o se interrumpe un ataque.</summary>
        public event Action AttackEnded;

        // Unity 6: Rigidbody2D.linearVelocity (antes se llamaba velocity)
        Vector2 Velocity
        {
            get => rb.linearVelocity;
            set => rb.linearVelocity = value;
        }

        // Vida actual y punto donde reaparece.
        float health;
        Vector3 spawnPos;

        // Al crearse: se registra como jugador global, guarda componentes y lee la duración de las animaciones.
        void Awake()
        {
            Instance = this;
            health = maxHealth;
            spawnPos = transform.position;
            facingRight = false; // los sprites base miran a la izquierda
            rb = GetComponent<Rigidbody2D>();
            anim = GetComponent<Animator>();
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
            hitFilter = ContactFilter2D.noFilter;
            inventory = GetComponent<PlayerInventory>();
            // Si no tiene inventario, se lo añade.
            if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();

            // Guarda cuánto dura cada animación del Animator.
            if (anim.runtimeAnimatorController != null)
            {
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                    clipLengths[clip.name] = clip.length;
            }
        }

        // Cada frame: suelo, temporizadores, tecla de prueba H y lo que toque según el estado.
        void Update()
        {
            float dt = Time.deltaTime;
            UpdateGrounded();
            UpdateTimers(dt);

            // H = simula recibir un golpe de 0 de daño (para probar la pasiva).
            if (GameInput.TestHitPressed)
            {
                Vector2 away = facingRight ? Vector2.left : Vector2.right;
                TakeDamage(0f, away * 3f + Vector2.up * 2f, false);
            }

            // Ejecuta la lógica del estado actual.
            switch (state)
            {
                case State.Normal: UpdateNormal(dt); break;
                case State.Attacking: UpdateAttack(dt); break;
                case State.Charged: UpdateCharged(dt); break;
                case State.Hurt: UpdateHurt(dt); break;
                case State.PickingUp: UpdatePickup(dt); break;
            }
        }

        // ---------------- Estados ----------------

        // Estado normal: caminar, saltar, atacar, cargado, recoger y elegir animación.
        void UpdateNormal(float dt)
        {
            // Movimiento horizontal y se gira hacia donde camina.
            float h = GameInput.Horizontal;
            Velocity = new Vector2(h * moveSpeed, Velocity.y);
            if (h > 0.01f) SetFacing(true);
            else if (h < -0.01f) SetFacing(false);

            // Salto (solo si está en el suelo).
            if (grounded && GameInput.JumpPressed)
            {
                Velocity = new Vector2(Velocity.x, jumpForce);
                grounded = false;
                landTimer = 0f;
            }

            // J = golpe 1 del combo; K = ataque cargado.
            if (GameInput.AttackPressed) { StartAttack(1); return; }
            if (GameInput.ChargedPressed) { TryChargedAttack(); return; }

            // Si hay un objeto cerca y se pulsa E, lo recoge.
            nearbyPickup = grounded ? Pickup.Closest(transform.position) : null;
            if (nearbyPickup != null && GameInput.InteractPressed) { StartPickup(nearbyPickup); return; }

            // Animación según la situación: saltando, aterrizando, caminando o quieto.
            if (!grounded)
                Play(Velocity.y > 0.1f ? "JumpUp" : "JumpFall");
            else if (landTimer > 0f && Mathf.Abs(h) < 0.01f)
                Play("Land");
            else
                Play(Mathf.Abs(h) > 0.01f ? "Walk" : "Idle");
        }

        // Empieza un golpe del combo (step = 1 a 4).
        void StartAttack(int step)
        {
            // Permite girarse entre golpes del combo
            float h = GameInput.Horizontal;
            if (h > 0.01f) SetFacing(true);
            else if (h < -0.01f) SetFacing(false);

            // Reinicia el estado del golpe.
            state = State.Attacking;
            comboStep = step;
            attackQueued = false;
            hitDone = false;
            stateTimer = 0f;

            // Reproduce la animación AttackN; el impacto llega al 70% (80% en el 4.º) y avisa a WeaponVisual.
            string clip = "Attack" + step;
            stateDuration = ClipLength(clip);
            hitTime = stateDuration * (step == 4 ? 0.8f : 0.7f);
            Play(clip, true);
            AttackStarted?.Invoke(step, stateDuration, hitTime);
        }

        // Cada frame mientras ataca: se queda quieto, cola de golpes, impacto y final.
        void UpdateAttack(float dt)
        {
            stateTimer += dt;
            if (grounded) Velocity = new Vector2(0f, Velocity.y);

            // Buffer: si pulsas J durante el golpe, el siguiente sale al terminar este
            if (GameInput.AttackPressed && comboStep < 4) attackQueued = true;

            // En el momento del impacto: calcula el daño del golpe y golpea.
            if (!hitDone && stateTimer >= hitTime)
            {
                hitDone = true;
                bool heavy = comboStep == 4;
                float mult = comboDamage[Mathf.Clamp(comboStep - 1, 0, comboDamage.Length - 1)];
                bool connected = DoHit(baseAttack * mult, heavy, heavy ? 1.3f : 1f);

                // Regla de diseño: solo el 4.º golpe que CONECTA da un Alma en agonía
                if (heavy && connected) AddAlma();
            }

            // Al terminar: si hay otro golpe en cola sigue; si no, vuelve a la normalidad.
            if (stateTimer >= stateDuration)
            {
                if (attackQueued && comboStep < 4) StartAttack(comboStep + 1);
                else EndAction();
            }
        }

        // K: si tiene las 3 Almas las gasta y lanza el ataque cargado; si no, avisa en pantalla.
        void TryChargedAttack()
        {
            if (almas < maxAlmas)
            {
                ShowMessage($"Necesitas {maxAlmas} Almas en agonía ({almas}/{maxAlmas})");
                return;
            }

            almas = 0;
            state = State.Charged;
            hitDone = false;
            stateTimer = 0f;
            stateDuration = ClipLength("Charged");
            hitTime = stateDuration * 0.8f;
            Play("Charged", true);
            AttackStarted?.Invoke(5, stateDuration, hitTime);
        }

        // Cada frame del ataque cargado: golpe fuerte y grande en el impacto.
        void UpdateCharged(float dt)
        {
            stateTimer += dt;
            if (grounded) Velocity = new Vector2(0f, Velocity.y);

            if (!hitDone && stateTimer >= hitTime)
            {
                hitDone = true;
                DoHit(baseAttack * chargedDamage, true, 1.6f);
            }

            if (stateTimer >= stateDuration) EndAction();
        }

        // Empieza a recoger un objeto: se para, lo mira y hace la animación.
        void StartPickup(Pickup p)
        {
            state = State.PickingUp;
            pendingPickup = p;
            pickupGiven = false;
            nearbyPickup = null;
            stateTimer = 0f;
            stateDuration = ClipLength("PickUp");
            Velocity = new Vector2(0f, Velocity.y);
            SetFacing(p.transform.position.x > transform.position.x);
            Play("PickUp", true);
        }

        // Cada frame recogiendo.
        void UpdatePickup(float dt)
        {
            stateTimer += dt;
            Velocity = new Vector2(0f, Velocity.y);

            // El objeto se recoge cuando la mano llega al suelo (mitad de la animación)
            if (!pickupGiven && stateTimer >= stateDuration * 0.5f)
            {
                pickupGiven = true;
                if (pendingPickup != null && pendingPickup.CanBePicked)
                {
                    inventory.Add(pendingPickup.item);
                    pendingPickup.Collect();
                }
            }

            if (stateTimer >= stateDuration) EndAction();
        }

        // Herido: espera hurtDuration y vuelve a la normalidad.
        void UpdateHurt(float dt)
        {
            stateTimer += dt;
            if (stateTimer >= hurtDuration) EndAction();
        }

        // Termina cualquier acción y vuelve al estado normal (avisa a WeaponVisual si atacaba).
        void EndAction()
        {
            if (state == State.Attacking || state == State.Charged) AttackEnded?.Invoke();
            state = State.Normal;
            comboStep = 0;
            attackQueued = false;
        }

        // ---------------- Combate ----------------

        // Busca enemigos en la caja de golpe delante del jugador y les hace daño. Devuelve true si golpeó a alguien.
        bool DoHit(float damage, bool heavy, float sizeScale)
        {
            Vector2 dir = facingRight ? Vector2.right : Vector2.left;
            Vector2 center = (Vector2)transform.position + new Vector2(hitboxOffset.x * dir.x * sizeScale, hitboxOffset.y);
            Vector2 size = hitboxSize * sizeScale;

            lastHitCenter = center;
            lastHitSize = size;
            hitGizmoTimer = 0.15f;

            // Busca todos los colliders dentro de la caja.
            hitThisSwing.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, hitFilter, hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = hitBuffer[i];
                // Ignora sus propios colliders.
                if (col == null || col.transform.IsChildOf(transform)) continue;
                // Solo daña a lo que implemente IDamageable, y a cada uno una sola vez por golpe.
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || hitThisSwing.Contains(target)) continue;

                hitThisSwing.Add(target);
                // Daño final = daño del golpe x pasiva x arma.
                float weaponMult = weapon != null ? weapon.damageMultiplier : 1f;
                target.TakeDamage(damage * AttackMultiplier * weaponMult, dir * (heavy ? 6f : 2f), heavy);
            }
            return hitThisSwing.Count > 0;
        }

        // Suma un Alma en agonía (máximo 3) y lo avisa en pantalla.
        void AddAlma()
        {
            if (almas >= maxAlmas) return;
            almas++;
            ShowMessage($"Alma en agonía {almas}/{maxAlmas}");
        }

        // Recibe daño (cumple el contrato IDamageable).
        public void TakeDamage(float amount, Vector2 knockback, bool heavy)
        {
            health -= amount;
            // Si muere en el prototipo, reaparece al inicio y pierde Almas y acumulaciones.
            if (health <= 0f)
            {
                // Prototipo: reaparece en el punto inicial con la vida llena
                health = maxHealth;
                transform.position = spawnPos;
                Velocity = Vector2.zero;
                almas = 0;
                rageStacks = 0;
                EndAction();
                ShowMessage("Has caído... El Que Observa sonríe");
                return;
            }

            // Pasiva del Caballero Maldito: cada golpe recibido da % de ATK temporal (máx. 3)
            rageStacks = Mathf.Min(rageStacks + 1, maxRageStacks);
            rageTimer = rageDuration;

            // Interrumpe lo que estaba haciendo, sale empujado, parpadea y hace la animación de golpe.
            if (state == State.Attacking || state == State.Charged) AttackEnded?.Invoke();
            state = State.Hurt;
            stateTimer = 0f;
            comboStep = 0;
            attackQueued = false;
            Velocity = knockback;
            flashTimer = 0.15f;
            Play("Hurt", true);
        }

        // ---------------- Utilidades ----------------

        // Comprueba si está tocando el suelo (un contacto con la normal hacia arriba).
        void UpdateGrounded()
        {
            grounded = false;
            int n = rb.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                if (contacts[i].normal.y > 0.6f) { grounded = true; break; }
            }

            // Al aterrizar activa la animación de aterrizaje.
            if (grounded && !wasGrounded) landTimer = ClipLength("Land");
            wasGrounded = grounded;
        }

        // Descuenta los temporizadores: aterrizaje, mensaje, depuración, pasiva y destello.
        void UpdateTimers(float dt)
        {
            if (landTimer > 0f) landTimer -= dt;
            if (messageTimer > 0f) messageTimer -= dt;
            if (hitGizmoTimer > 0f) hitGizmoTimer -= dt;

            if (rageTimer > 0f)
            {
                rageTimer -= dt;
                // Al acabarse la pasiva se pierden todas las acumulaciones.
                if (rageTimer <= 0f) rageStacks = 0;
            }

            // Destello rojo al recibir daño.
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                sr.color = flashTimer > 0f ? new Color(1f, 0.45f, 0.45f, 1f) : baseColor;
            }
        }

        // Gira el sprite para mirar a la derecha o a la izquierda.
        void SetFacing(bool right)
        {
            facingRight = right;
            // Los sprites base miran a la izquierda (West): se voltean para mirar a la derecha
            sr.flipX = right;
        }

        // Reproduce una animación del Animator (no la reinicia si ya está, salvo restart).
        void Play(string animName, bool restart = false)
        {
            if (!restart && currentAnim == animName) return;
            anim.Play(animName, 0, 0f);
            currentAnim = animName;
        }

        // Duración de una animación (0.3 s si no la encuentra).
        float ClipLength(string clipName)
        {
            return clipLengths.TryGetValue(clipName, out float len) ? len : 0.3f;
        }

        // Muestra un mensaje en pantalla durante 1.5 segundos.
        void ShowMessage(string text)
        {
            message = text;
            messageTimer = 1.5f;
        }

        // ---------------- Depuración ----------------

        // Interfaz de prueba: vida, Almas, pasiva, combo y controles.
        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            string orbs = new string('●', almas) + new string('○', Mathf.Max(0, maxAlmas - almas));
            string text =
                "CABALLERO MALDITO (prototipo)\n" +
                $"Vida: {Mathf.CeilToInt(health)}/{Mathf.RoundToInt(maxHealth)}\n" +
                $"Alma en agonía: {orbs}\n" +
                $"Pasiva: {rageStacks}/{maxRageStacks}  (+{Mathf.RoundToInt((AttackMultiplier - 1f) * 100f)}% ATK)\n" +
                $"Golpe del combo: {(comboStep > 0 ? comboStep.ToString() : "-")}\n\n" +
                "A/D mover · Espacio saltar\nJ atacar (combo) · K cargado\nE recoger · H simular golpe recibido";
            GUI.Box(new Rect(10, 10, 300, 195), text, style);

            // Aviso 'E Recoger' encima del jugador cuando hay un objeto cerca.
            if (state == State.Normal && nearbyPickup != null && Camera.main != null)
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.1f);
                var hint = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect(sp.x - 70, Screen.height - sp.y - 14, 140, 26), "E  Recoger", hint);
            }

            // Mensaje temporal en el centro de la pantalla.
            if (messageTimer > 0f && !string.IsNullOrEmpty(message))
            {
                var msgStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                msgStyle.normal.textColor = new Color(0.8f, 0.5f, 1f);
                GUI.Label(new Rect(0, 60, Screen.width, 30), message, msgStyle);
            }
        }

        // En el editor dibuja la caja de golpe (más fuerte justo después de golpear).
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, hitGizmoTimer > 0f ? 0.9f : 0.25f);
            if (hitGizmoTimer > 0f)
            {
                Gizmos.DrawWireCube(lastHitCenter, lastHitSize);
            }
            else
            {
                Vector2 dir = facingRight ? Vector2.right : Vector2.left;
                Vector2 c = (Vector2)transform.position + new Vector2(hitboxOffset.x * dir.x, hitboxOffset.y);
                Gizmos.DrawWireCube(c, hitboxSize);
            }
        }
    }
}
