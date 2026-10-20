using UnityEngine;

namespace DarkFantasy
{
    /// <summary>Bola de fuego espectral que lanza el hechicero. Daña al jugador al tocarlo.</summary>
    public class MagicProjectile : MonoBehaviour
    {
        // Velocidad, daño, segundos antes de desaparecer, tamaño de impacto y color.
        public float speed = 6f;
        public float damage = 10f;
        public float lifetime = 3f;
        public float hitRadius = 0.35f;
        public Color color = new Color(0.55f, 0.8f, 1f, 1f);

        // Estado interno: hacia dónde va, tiempo vivo y su sprite.
        Vector2 direction;
        float t;
        SpriteRenderer sr;
        // La imagen de la bola se genera una sola vez y la comparten todas.
        static Sprite orbSprite;

        // Crea una bola nueva en una posición, con dirección, daño y color.
        public static MagicProjectile Spawn(Vector3 position, Vector2 direction, float damage, Color color)
        {
            var go = new GameObject("BolaEspectral");
            go.transform.position = position;
            var p = go.AddComponent<MagicProjectile>();
            p.direction = direction.normalized;
            p.damage = damage;
            p.color = color;
            return p;
        }

        // Al crearse: le pone su imagen y la dibuja por delante de los personajes.
        void Awake()
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrbSprite();
            sr.sortingOrder = 15;
        }

        // Cada frame: avanza, palpita y revisa si tocó al jugador.
        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            // Avanza en línea recta.
            transform.position += (Vector3)(direction * speed * dt);

            // Efecto de palpitar (crece y encoge rápido).
            float pulse = 1f + Mathf.Sin(t * 25f) * 0.12f;
            transform.localScale = Vector3.one * pulse;
            sr.color = color;

            // Busca al jugador del prototipo lateral (PlayerController).
            var player = PlayerController.Instance;
            if (player != null)
            {
                // Mide la distancia al pecho del jugador; si está cerca, le hace daño y desaparece.
                Vector2 body = (Vector2)player.transform.position + Vector2.up * 0.9f;
                if (Vector2.Distance(body, transform.position) < hitRadius + 0.45f)
                {
                    player.TakeDamage(damage, new Vector2(direction.x * 4f, 2.5f), false);
                    Destroy(gameObject);
                    return;
                }
            }

            // Si pasó su tiempo de vida sin tocar nada, desaparece.
            if (t >= lifetime) Destroy(gameObject);
        }

        // Dibuja por código una bola de 12x12 píxeles: centro blanco y borde semitransparente.
        static Sprite GetOrbSprite()
        {
            if (orbSprite != null) return orbSprite;
            const int size = 12;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var c = new Vector2(size / 2f, size / 2f);
            // Recorre cada píxel y le da color según su distancia al centro.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    Color col = Color.clear;
                    if (d < 2.2f) col = Color.white;
                    else if (d < 4f) col = new Color(1f, 1f, 1f, 0.85f);
                    else if (d < 5.5f) col = new Color(1f, 1f, 1f, 0.35f);
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            // Convierte la textura en un sprite (32 píxeles por unidad).
            orbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return orbSprite;
        }
    }
}
