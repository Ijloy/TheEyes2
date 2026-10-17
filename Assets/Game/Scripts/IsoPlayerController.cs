using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Caballero en vista isométrica (estilo Diablo 2): movimiento en 8 direcciones sin gravedad.
    /// - La diagonal se normaliza para ir a la misma velocidad que en recto.
    /// - Animación por cuadros (generada en PixelLab): una lista de cuadros de caminar por dirección.
    /// - Solo hacen falta 5 direcciones (S, SE, E, NE, N): SO, O y NO se sacan en espejo de SE, E y NE.
    /// - Si una dirección no tiene cuadros de caminar, usa su imagen fija de reposo.
    /// - Combo de 4 golpes con J (buffer de entrada): cada golpe da un paso adelante, saca su tajo
    ///   orientado hacia donde mira el caballero y golpea a ras de suelo a cualquier IDamageable.
    /// Orden de las direcciones (índice 0-7): S, SE, E, NE, N, NO, O, SO.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsoPlayerController : MonoBehaviour
    {
        public static readonly string[] DirectionNames =
            { "sur", "sureste", "este", "noreste", "norte", "noroeste", "oeste", "suroeste" };

        /// <summary>Dirección de la que se saca el espejo (-1 = no tiene espejo).</summary>
        static readonly int[] MirrorOf = { -1, -1, -1, -1, -1, 3, 2, 1 };

        [Serializable]
        public class DirectionFrames
        {
            public Sprite[] frames = new Sprite[0];
            public bool HasFrames => frames != null && frames.Length > 0 && frames[0] != null;
        }

        [Header("Movimiento")]
        public float moveSpeed = 3.5f;

        [Header("Sprites")]
        [Tooltip("Hijo con el SpriteRenderer (pivote en los pies).")]
        public SpriteRenderer visual;
        [Tooltip("Imagen de reposo de cada dirección: S, SE, E, NE, N, NO, O, SO. Las vacías de NO, O y SO se sacan en espejo.")]
        public Sprite[] directionSprites = new Sprite[8];

        [Header("Animación de caminar (cuadros)")]
        [Tooltip("Cuadros de caminar por dirección: S, SE, E, NE, N, NO, O, SO. Basta con rellenar las 5 primeras.")]
        public DirectionFrames[] walkFrames = new DirectionFrames[8];
        [Tooltip("Cuadros por segundo de la animación de caminar")]
        public float walkFps = 8f;

        [Header("Animación de ataque (cuadros)")]
        [Tooltip("Cuadros de ataque por dirección: S, SE, E, NE, N, NO, O, SO. Si una dirección no tiene, se usa su imagen de reposo.")]
        public DirectionFrames[] attackFrames = new DirectionFrames[8];
        [Tooltip("Cuadros por segundo de la animación de ataque (se repite desde el principio en cada golpe del combo)")]
        public float attackFps = 15f;

        [Tooltip("Píxeles por unidad del arte (lo usa el montaje de la escena)")]
        public float pixelsPerUnit = 32f;

        /// <summary>Un golpe del combo. Los cuadros del tajo están dibujados mirando a la derecha (este).</summary>
        [Serializable]
        public class AttackStep
        {
            public string label;
            public Sprite[] slashFrames = new Sprite[0];
            [Tooltip("Cuadros por segundo del tajo")]
            public float fps = 20f;
            [Tooltip("Duración del golpe (s); al acabar sale el siguiente si se pulsó J")]
            public float duration = 0.3f;
            [Tooltip("Momento del impacto (s desde el inicio del golpe)")]
            public float hitTime = 0.08f;
            public float damage = 10f;
            [Tooltip("Distancia de los pies al centro de la zona de golpe (unidades)")]
            public float reach = 0.75f;
            [Tooltip("Radio de la zona de golpe (unidades)")]
            public float radius = 0.7f;
            [Tooltip("Velocidad del paso adelante al golpear")]
            public float lunge = 2.5f;
            [Tooltip("Golpe fuerte: más empuje, números grandes y más hit-stop")]
            public bool heavy;
        }

        [Header("Combate (combo con J)")]
        public AttackStep[] combo = DefaultCombo();
        [Tooltip("Hijo con el SpriteRenderer del tajo")]
        public SpriteRenderer slashRenderer;
        [Tooltip("Altura del centro del tajo sobre los pies (unidades): a la altura del pecho")]
        public float slashHeight = 1.2f;
        [Tooltip("Separación del tajo respecto al cuerpo, hacia donde se ataca (unidades)")]
        public float slashDistance = 0.55f;
        [Tooltip("Separación extra al atacar hacia el norte, para que el tajo asome por encima de la cabeza")]
        public float slashExtraUp = 0.2f;
        [Tooltip("Duración del paso adelante de cada golpe (s)")]
        public float lungeTime = 0.08f;
        [Tooltip("Pausa al conectar un golpe (s, tiempo real); el golpe fuerte la duplica")]
        public float hitStop = 0.045f;

        Rigidbody2D rb;
        Vector2 input;
        int direction; // 0 = sur
        float walkTime;

        int comboIndex = -1; // -1 = no está atacando
        bool attackQueued;
        bool hitDone;
        float attackTimer;
        Vector2 attackDir;
        float slashTimer = -1f;
        AttackStep slashStep;
        bool hitStopping;
        Vector2 lastHitCenter;
        float lastHitRadius, hitGizmoTimer;
        readonly Collider2D[] hitBuffer = new Collider2D[16];
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

        public int Direction => direction;
        public bool IsMoving { get; private set; }
        public bool IsAttacking => comboIndex >= 0;
        /// <summary>Golpe del combo en curso (1-4) o 0 si no ataca.</summary>
        public int ComboStep => comboIndex + 1;

        static AttackStep[] DefaultCombo() => new[]
        {
            new AttackStep { label = "1. Diagonal baja", fps = 20f, duration = 0.30f, hitTime = 0.08f, damage = 10f, reach = 0.75f, radius = 0.7f, lunge = 2.5f },
            new AttackStep { label = "2. Diagonal sube", fps = 20f, duration = 0.30f, hitTime = 0.08f, damage = 10f, reach = 0.75f, radius = 0.7f, lunge = 2.5f },
            new AttackStep { label = "3. Vertical", fps = 20f, duration = 0.32f, hitTime = 0.09f, damage = 12f, reach = 0.8f, radius = 0.7f, lunge = 3f },
            new AttackStep { label = "4. Horizontal fuerte", fps = 16f, duration = 0.46f, hitTime = 0.14f, damage = 20f, reach = 0.9f, radius = 0.95f, lunge = 4.5f, heavy = true },
        };

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
            if (visual != null)
            {
                // Quita cualquier resto de la animación por código anterior
                visual.transform.localScale = Vector3.one;
                visual.transform.localPosition = Vector3.zero;
            }
            if (slashRenderer != null) slashRenderer.enabled = false;
        }

        void OnDisable()
        {
            if (hitStopping) { Time.timeScale = 1f; hitStopping = false; }
        }

        void Update()
        {
            input = new Vector2(GameInput.Horizontal, GameInput.Vertical);
            // En diagonal la magnitud sería 1,41: se normaliza para ir igual de rápido que en recto
            if (input.sqrMagnitude > 1f) input.Normalize();

            if (IsAttacking) UpdateAttack(Time.deltaTime);
            else if (GameInput.AttackPressed && combo != null && combo.Length > 0) StartAttack(0);

            IsMoving = !IsAttacking && input.sqrMagnitude > 0.0001f;
            if (IsMoving) direction = DirectionFromVector(input);

            UpdateSlash(Time.deltaTime);
            if (hitGizmoTimer > 0f) hitGizmoTimer -= Time.deltaTime;

            if (visual == null) return;
            if (IsMoving) walkTime += Time.deltaTime;
            else walkTime = 0f; // al volver a caminar empieza desde el primer cuadro

            UpdateSprite();
        }

        void FixedUpdate()
        {
            if (IsAttacking)
                rb.linearVelocity = attackTimer < lungeTime ? attackDir * combo[comboIndex].lunge : Vector2.zero;
            else
                rb.linearVelocity = input * moveSpeed;
        }

        // ---------------- Combate ----------------

        void StartAttack(int index)
        {
            // Entre golpes se puede reorientar con la dirección pulsada
            if (input.sqrMagnitude > 0.0001f) direction = DirectionFromVector(input);
            attackDir = DirectionVector(direction);

            comboIndex = index;
            attackQueued = false;
            hitDone = false;
            attackTimer = 0f;
            ShowSlash(combo[index]);
        }

        void UpdateAttack(float dt)
        {
            var step = combo[comboIndex];
            attackTimer += dt;

            // Buffer: si pulsas J durante el golpe, el siguiente sale al terminar este
            if (GameInput.AttackPressed && comboIndex < combo.Length - 1) attackQueued = true;

            if (!hitDone && attackTimer >= step.hitTime)
            {
                hitDone = true;
                if (DoHit(step)) StartCoroutine(HitStop(step.heavy ? hitStop * 2f : hitStop));
            }

            if (attackTimer >= step.duration)
            {
                if (attackQueued) StartAttack(comboIndex + 1);
                else comboIndex = -1;
            }
        }

        bool DoHit(AttackStep step)
        {
            // Zona de golpe a ras de suelo, delante de los pies (donde están los colliders)
            Vector2 center = (Vector2)transform.position + attackDir * step.reach;
            lastHitCenter = center;
            lastHitRadius = step.radius;
            hitGizmoTimer = 0.15f;

            hitThisSwing.Clear();
            int count = Physics2D.OverlapCircle(center, step.radius, ContactFilter2D.noFilter, hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = hitBuffer[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !hitThisSwing.Add(target)) continue;
                target.TakeDamage(step.damage, attackDir * (step.heavy ? 6f : 2f), step.heavy);
            }
            return hitThisSwing.Count > 0;
        }

        IEnumerator HitStop(float seconds)
        {
            if (hitStopping || seconds <= 0f) yield break;
            hitStopping = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            hitStopping = false;
        }

        /// <summary>
        /// Coloca el tajo delante del caballero. Los cuadros miran al este: hacia el oeste se voltean
        /// y para el resto de direcciones se giran. Hacia el norte se dibuja detrás del caballero.
        /// </summary>
        void ShowSlash(AttackStep step)
        {
            if (slashRenderer == null || step.slashFrames == null || step.slashFrames.Length == 0) return;

            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            bool flip = attackDir.x < -0.01f;
            var t = slashRenderer.transform;
            float distance = slashDistance + Mathf.Max(0f, attackDir.y) * slashExtraUp;
            t.localPosition = new Vector3(0f, slashHeight, 0f) + (Vector3)(attackDir * distance);
            t.localRotation = Quaternion.Euler(0f, 0f, flip ? angle - 180f : angle);
            slashRenderer.flipX = flip;
            slashRenderer.sortingOrder = attackDir.y > 0.1f ? -1 : 1;
            slashRenderer.sprite = step.slashFrames[0];
            slashRenderer.enabled = true;
            slashStep = step;
            slashTimer = 0f;
        }

        void UpdateSlash(float dt)
        {
            if (slashRenderer == null || slashTimer < 0f || slashStep == null) return;
            slashTimer += dt;
            int frame = Mathf.FloorToInt(slashTimer * slashStep.fps);
            if (frame >= slashStep.slashFrames.Length)
            {
                slashRenderer.enabled = false;
                slashTimer = -1f;
                return;
            }
            slashRenderer.sprite = slashStep.slashFrames[frame];
        }

        /// <summary>Vector unitario de una de las 8 direcciones (0 = sur, en sentido antihorario).</summary>
        public static Vector2 DirectionVector(int dir)
        {
            float a = (-90f + dir * 45f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        void UpdateSprite()
        {
            bool flip;
            if (IsAttacking)
            {
                var clip = GetFrames(attackFrames, direction, out flip);
                if (clip != null)
                {
                    // Un pase por golpe; si el golpe dura más que la animación, se queda en el último cuadro
                    int frame = Mathf.Min(Mathf.FloorToInt(attackTimer * attackFps), clip.frames.Length - 1);
                    visual.sprite = clip.frames[frame];
                    visual.flipX = flip;
                    return;
                }
            }
            else if (IsMoving)
            {
                var clip = GetFrames(walkFrames, direction, out flip);
                if (clip != null)
                {
                    int frame = Mathf.FloorToInt(walkTime * walkFps) % clip.frames.Length;
                    visual.sprite = clip.frames[frame];
                    visual.flipX = flip;
                    return;
                }
            }
            var idle = GetIdle(direction, out flip);
            if (idle != null)
            {
                visual.sprite = idle;
                visual.flipX = flip;
            }
        }

        /// <summary>Cuadros de una dirección, o los de su espejo (volteados) si no los tiene.</summary>
        static DirectionFrames GetFrames(DirectionFrames[] set, int dir, out bool flip)
        {
            flip = false;
            if (set != null && dir < set.Length && set[dir] != null && set[dir].HasFrames)
                return set[dir];
            int m = MirrorOf[dir];
            if (m >= 0 && set != null && m < set.Length && set[m] != null && set[m].HasFrames)
            {
                flip = true;
                return set[m];
            }
            return null;
        }

        Sprite GetIdle(int dir, out bool flip)
        {
            flip = false;
            if (directionSprites != null && dir < directionSprites.Length && directionSprites[dir] != null)
                return directionSprites[dir];
            int m = MirrorOf[dir];
            if (m >= 0 && directionSprites != null && m < directionSprites.Length && directionSprites[m] != null)
            {
                flip = true;
                return directionSprites[m];
            }
            return null;
        }

        /// <summary>Convierte un vector en una de las 8 direcciones (0 = sur, en sentido antihorario).</summary>
        public static int DirectionFromVector(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // sur = -90
            int i = Mathf.RoundToInt((angle + 90f) / 45f);
            return ((i % 8) + 8) % 8;
        }

        // ---------------- HUD y depuración ----------------

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            string text = "CABALLERO MALDITO (vista isométrica)\n" +
                          $"Golpe del combo: {(IsAttacking ? $"{ComboStep}/{combo.Length}" : "-")}\n\n" +
                          "WASD / flechas: moverse\nJ: atacar (pulsa durante el golpe para encadenar)";
            GUI.Box(new Rect(10, 10, 330, 92), text, style);
        }

        void OnDrawGizmos()
        {
            if (hitGizmoTimer <= 0f) return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(lastHitCenter, lastHitRadius);
        }
    }
}
