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
        public static PlayerController Instance { get; private set; }

        [Header("Vida")]
        public float maxHealth = 100f;

        [Header("Movimiento")]
        public float moveSpeed = 4f;
        public float jumpForce = 11f;

        [Header("Arma equipada")]
        public WeaponDefinition weapon;

        [Header("Combate")]
        public float baseAttack = 10f;
        [Tooltip("Multiplicador de daño de cada golpe del combo (1.º al 4.º)")]
        public float[] comboDamage = { 1f, 1f, 1.2f, 2f };
        public float chargedDamage = 4f;
        public Vector2 hitboxOffset = new Vector2(0.7f, 0.9f);
        public Vector2 hitboxSize = new Vector2(1.2f, 1.2f);

        [Header("Alma en agonía (cargas del combo)")]
        public int maxAlmas = 3;

        [Header("Pasiva: ATK al recibir golpes")]
        public int maxRageStacks = 3;
        [Range(0f, 1f)] public float atkPerStack = 0.10f;
        public float rageDuration = 5f;

        [Header("Recibir golpe")]
        public float hurtDuration = 0.3f;

        enum State { Normal, Attacking, Charged, Hurt, PickingUp }

        State state = State.Normal;
        Rigidbody2D rb;
        Animator anim;
        SpriteRenderer sr;

        bool facingRight;
        bool grounded;
        bool wasGrounded = true;
        float landTimer;
        string currentAnim;

        int comboStep;
        bool attackQueued;
        bool hitDone;
        float stateTimer;
        float stateDuration;
        float hitTime;

        int almas;
        int rageStacks;
        float rageTimer;

        float flashTimer;
        Color baseColor;
        string message;
        float messageTimer;
        Vector2 lastHitCenter;
        Vector2 lastHitSize;
        float hitGizmoTimer;

        PlayerInventory inventory;
        Pickup pendingPickup;
        Pickup nearbyPickup;
        bool pickupGiven;

        readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        readonly Collider2D[] hitBuffer = new Collider2D[16];
        readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        ContactFilter2D hitFilter;

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

        float health;
        Vector3 spawnPos;

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
            if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();

            if (anim.runtimeAnimatorController != null)
            {
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                    clipLengths[clip.name] = clip.length;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateGrounded();
            UpdateTimers(dt);

            if (GameInput.TestHitPressed)
            {
                Vector2 away = facingRight ? Vector2.left : Vector2.right;
                TakeDamage(0f, away * 3f + Vector2.up * 2f, false);
            }

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

        void UpdateNormal(float dt)
        {
            float h = GameInput.Horizontal;
            Velocity = new Vector2(h * moveSpeed, Velocity.y);
            if (h > 0.01f) SetFacing(true);
            else if (h < -0.01f) SetFacing(false);

            if (grounded && GameInput.JumpPressed)
            {
                Velocity = new Vector2(Velocity.x, jumpForce);
                grounded = false;
                landTimer = 0f;
            }

            if (GameInput.AttackPressed) { StartAttack(1); return; }
            if (GameInput.ChargedPressed) { TryChargedAttack(); return; }

            nearbyPickup = grounded ? Pickup.Closest(transform.position) : null;
            if (nearbyPickup != null && GameInput.InteractPressed) { StartPickup(nearbyPickup); return; }

            if (!grounded)
                Play(Velocity.y > 0.1f ? "JumpUp" : "JumpFall");
            else if (landTimer > 0f && Mathf.Abs(h) < 0.01f)
                Play("Land");
            else
                Play(Mathf.Abs(h) > 0.01f ? "Walk" : "Idle");
        }

        void StartAttack(int step)
        {
            // Permite girarse entre golpes del combo
            float h = GameInput.Horizontal;
            if (h > 0.01f) SetFacing(true);
            else if (h < -0.01f) SetFacing(false);

            state = State.Attacking;
            comboStep = step;
            attackQueued = false;
            hitDone = false;
            stateTimer = 0f;

            string clip = "Attack" + step;
            stateDuration = ClipLength(clip);
            hitTime = stateDuration * (step == 4 ? 0.8f : 0.7f);
            Play(clip, true);
            AttackStarted?.Invoke(step, stateDuration, hitTime);
        }

        void UpdateAttack(float dt)
        {
            stateTimer += dt;
            if (grounded) Velocity = new Vector2(0f, Velocity.y);

            // Buffer: si pulsas J durante el golpe, el siguiente sale al terminar este
            if (GameInput.AttackPressed && comboStep < 4) attackQueued = true;

            if (!hitDone && stateTimer >= hitTime)
            {
                hitDone = true;
                bool heavy = comboStep == 4;
                float mult = comboDamage[Mathf.Clamp(comboStep - 1, 0, comboDamage.Length - 1)];
                bool connected = DoHit(baseAttack * mult, heavy, heavy ? 1.3f : 1f);

                // Regla de diseño: solo el 4.º golpe que CONECTA da un Alma en agonía
                if (heavy && connected) AddAlma();
            }

            if (stateTimer >= stateDuration)
            {
                if (attackQueued && comboStep < 4) StartAttack(comboStep + 1);
                else EndAction();
            }
        }

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

        void UpdateHurt(float dt)
        {
            stateTimer += dt;
            if (stateTimer >= hurtDuration) EndAction();
        }

        void EndAction()
        {
            if (state == State.Attacking || state == State.Charged) AttackEnded?.Invoke();
            state = State.Normal;
            comboStep = 0;
            attackQueued = false;
        }

        // ---------------- Combate ----------------

        bool DoHit(float damage, bool heavy, float sizeScale)
        {
            Vector2 dir = facingRight ? Vector2.right : Vector2.left;
            Vector2 center = (Vector2)transform.position + new Vector2(hitboxOffset.x * dir.x * sizeScale, hitboxOffset.y);
            Vector2 size = hitboxSize * sizeScale;

            lastHitCenter = center;
            lastHitSize = size;
            hitGizmoTimer = 0.15f;

            hitThisSwing.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, hitFilter, hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = hitBuffer[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || hitThisSwing.Contains(target)) continue;

                hitThisSwing.Add(target);
                float weaponMult = weapon != null ? weapon.damageMultiplier : 1f;
                target.TakeDamage(damage * AttackMultiplier * weaponMult, dir * (heavy ? 6f : 2f), heavy);
            }
            return hitThisSwing.Count > 0;
        }

        void AddAlma()
        {
            if (almas >= maxAlmas) return;
            almas++;
            ShowMessage($"Alma en agonía {almas}/{maxAlmas}");
        }

        public void TakeDamage(float amount, Vector2 knockback, bool heavy)
        {
            health -= amount;
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

        void UpdateGrounded()
        {
            grounded = false;
            int n = rb.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                if (contacts[i].normal.y > 0.6f) { grounded = true; break; }
            }

            if (grounded && !wasGrounded) landTimer = ClipLength("Land");
            wasGrounded = grounded;
        }

        void UpdateTimers(float dt)
        {
            if (landTimer > 0f) landTimer -= dt;
            if (messageTimer > 0f) messageTimer -= dt;
            if (hitGizmoTimer > 0f) hitGizmoTimer -= dt;

            if (rageTimer > 0f)
            {
                rageTimer -= dt;
                if (rageTimer <= 0f) rageStacks = 0;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                sr.color = flashTimer > 0f ? new Color(1f, 0.45f, 0.45f, 1f) : baseColor;
            }
        }

        void SetFacing(bool right)
        {
            facingRight = right;
            // Los sprites base miran a la izquierda (West): se voltean para mirar a la derecha
            sr.flipX = right;
        }

        void Play(string animName, bool restart = false)
        {
            if (!restart && currentAnim == animName) return;
            anim.Play(animName, 0, 0f);
            currentAnim = animName;
        }

        float ClipLength(string clipName)
        {
            return clipLengths.TryGetValue(clipName, out float len) ? len : 0.3f;
        }

        void ShowMessage(string text)
        {
            message = text;
            messageTimer = 1.5f;
        }

        // ---------------- Depuración ----------------

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

            if (state == State.Normal && nearbyPickup != null && Camera.main != null)
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.1f);
                var hint = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect(sp.x - 70, Screen.height - sp.y - 14, 140, 26), "E  Recoger", hint);
            }

            if (messageTimer > 0f && !string.IsNullOrEmpty(message))
            {
                var msgStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                msgStyle.normal.textColor = new Color(0.8f, 0.5f, 1f);
                GUI.Label(new Rect(0, 60, Screen.width, 30), message, msgStyle);
            }
        }

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
