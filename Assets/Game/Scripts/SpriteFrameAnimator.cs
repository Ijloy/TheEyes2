using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Animador sencillo por cuadros para enemigos: cada animación es una lista de sprites.
    /// Más fácil de mantener que un Animator cuando un enemigo tiene pocas animaciones.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        // Una animación: nombre, lista de imágenes, velocidad y si se repite.
        [System.Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 8f;
            public bool loop = true;
        }

        // Todas las animaciones de este enemigo (se llenan en el Inspector).
        public Clip[] clips = new Clip[0];

        // Estado interno: animación actual, tiempo acumulado y cuadro actual.
        SpriteRenderer sr;
        Clip current;
        float timer;
        int frame;

        // true cuando una animación que no se repite ya llegó a su último cuadro.
        public bool IsFinished => current != null && !current.loop && frame >= current.frames.Length - 1 &&
                                  timer >= 1f / Mathf.Max(1f, current.fps);

        // Guarda el SpriteRenderer donde se dibujan los cuadros.
        void Awake() => sr = GetComponent<SpriteRenderer>();

        // Devuelve cuánto dura una animación en segundos (0.3 si no existe).
        public float Length(string clipName)
        {
            var c = Find(clipName);
            return c == null || c.frames.Length == 0 ? 0.3f : c.frames.Length / Mathf.Max(1f, c.fps);
        }

        // Cambia a otra animación. Si ya está sonando no la reinicia (salvo restart = true).
        public void Play(string clipName, bool restart = false)
        {
            var c = Find(clipName);
            if (c == null || c.frames == null || c.frames.Length == 0) return;
            if (c == current && !restart) return;
            current = c;
            timer = 0f;
            frame = 0;
            sr.sprite = c.frames[0];
        }

        // Cada frame avanza los cuadros según el tiempo que pasó.
        void Update()
        {
            if (current == null || current.frames.Length == 0) return;
            timer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, current.fps);
            // Avanza tantos cuadros como toquen (por si el juego fue lento en un frame).
            while (timer >= step)
            {
                if (frame < current.frames.Length - 1) { frame++; timer -= step; }
                else if (current.loop) { frame = 0; timer -= step; }
                else break;
            }
            sr.sprite = current.frames[frame];
        }

        // Busca una animación por su nombre.
        Clip Find(string clipName)
        {
            foreach (var c in clips) if (c != null && c.name == clipName) return c;
            return null;
        }
    }
}
