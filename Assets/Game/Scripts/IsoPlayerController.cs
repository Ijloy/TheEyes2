using System;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Caballero en vista isométrica (estilo Diablo 2): movimiento en 8 direcciones sin gravedad.
    /// - La diagonal se normaliza para ir a la misma velocidad que en recto.
    /// - Animación por cuadros (generada en PixelLab): una lista de cuadros de caminar por dirección.
    /// - Solo hacen falta 5 direcciones (S, SE, E, NE, N): SO, O y NO se sacan en espejo de SE, E y NE.
    /// - Si una dirección no tiene cuadros de caminar, usa su imagen fija de reposo.
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

        [Tooltip("Píxeles por unidad del arte (lo usa el montaje de la escena)")]
        public float pixelsPerUnit = 32f;

        Rigidbody2D rb;
        Vector2 input;
        int direction; // 0 = sur
        float walkTime;

        public int Direction => direction;
        public bool IsMoving { get; private set; }

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
        }

        void Update()
        {
            input = new Vector2(GameInput.Horizontal, GameInput.Vertical);
            // En diagonal la magnitud sería 1,41: se normaliza para ir igual de rápido que en recto
            if (input.sqrMagnitude > 1f) input.Normalize();

            IsMoving = input.sqrMagnitude > 0.0001f;
            if (IsMoving) direction = DirectionFromVector(input);

            if (visual == null) return;
            if (IsMoving) walkTime += Time.deltaTime;
            else walkTime = 0f; // al volver a caminar empieza desde el primer cuadro

            UpdateSprite();
        }

        void FixedUpdate()
        {
            rb.linearVelocity = input * moveSpeed;
        }

        void UpdateSprite()
        {
            bool flip;
            if (IsMoving)
            {
                var clip = GetWalk(direction, out flip);
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

        DirectionFrames GetWalk(int dir, out bool flip)
        {
            flip = false;
            if (walkFrames != null && dir < walkFrames.Length && walkFrames[dir] != null && walkFrames[dir].HasFrames)
                return walkFrames[dir];
            int m = MirrorOf[dir];
            if (m >= 0 && walkFrames != null && m < walkFrames.Length && walkFrames[m] != null && walkFrames[m].HasFrames)
            {
                flip = true;
                return walkFrames[m];
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
    }
}
