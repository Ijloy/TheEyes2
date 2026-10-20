using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Cofre que aparece en el suelo donde muere un enemigo: se abre una sola vez, suelta el botín
    /// del LootDropper cuando sale la luz morada, se queda abierto unos segundos y se desvanece.
    /// Lo crea LootDropper.Drop() si tiene activado useChest.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(SpriteFrameAnimator))]
    public class LootChest : MonoBehaviour
    {
        const string OpenClip = "Abrir";

        [Tooltip("Cuadros por segundo de la animación de abrir")]
        public float fps = 8f;
        [Tooltip("Cuadro (empezando en 0) en el que sale el botín: el 6 es el 7.º, cuando sale la luz morada")]
        public int dropFrame = 6;
        [Tooltip("Segundos que se queda abierto (en el último cuadro) antes de desvanecerse")]
        public float openTime = 3f;
        [Tooltip("Duración del desvanecimiento (s)")]
        public float fadeTime = 0.5f;
        [Tooltip("Altura sobre la base del cofre desde la que salta el botín (unidades)")]
        public float lootHeight = 0.4f;

        // Estado interno: quién tiene la tabla de botín, imagen, tiempos y si ya soltó el botín.
        LootDropper dropper;
        SpriteRenderer sr;
        int frameCount;
        float t;
        bool dropped;

        /// <summary>Crea un cofre con su base en 'position' (ya en el suelo) que soltará el botín de 'dropper'.</summary>
        public static LootChest Spawn(LootDropper dropper, Sprite[] frames, Vector3 position)
        {
            var go = new GameObject("Cofre");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 6; // detrás del botín (7-8) y de los personajes
            var anim = go.AddComponent<SpriteFrameAnimator>();
            var chest = go.AddComponent<LootChest>();
            chest.dropper = dropper;
            chest.frameCount = frames.Length;
            anim.clips = new[] { new SpriteFrameAnimator.Clip { name = OpenClip, frames = frames, fps = chest.fps, loop = false } };
            anim.Play(OpenClip);
            return chest;
        }

        void Awake() => sr = GetComponent<SpriteRenderer>();

        // Cada frame: suelta el botín al llegar a dropFrame y, tras openTime en el último cuadro, se desvanece.
        void Update()
        {
            t += Time.deltaTime;

            if (!dropped && t >= dropFrame / fps)
            {
                dropped = true;
                if (dropper != null) dropper.SpawnLoot(transform.position + Vector3.up * lootHeight);
            }

            // Momento en que se muestra el último cuadro (ya abierto del todo).
            float openedAt = (frameCount - 1) / fps;
            float fade = (t - openedAt - openTime) / Mathf.Max(0.01f, fadeTime);
            if (fade <= 0f) return;
            sr.color = new Color(1f, 1f, 1f, 1f - fade);
            if (fade >= 1f) Destroy(gameObject);
        }
    }
}
