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
        [System.Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 8f;
            public bool loop = true;
        }

        public Clip[] clips = new Clip[0];

        SpriteRenderer sr;
        Clip current;
        float timer;
        int frame;

        public bool IsFinished => current != null && !current.loop && frame >= current.frames.Length - 1 &&
                                  timer >= 1f / Mathf.Max(1f, current.fps);

        void Awake() => sr = GetComponent<SpriteRenderer>();

        public float Length(string clipName)
        {
            var c = Find(clipName);
            return c == null || c.frames.Length == 0 ? 0.3f : c.frames.Length / Mathf.Max(1f, c.fps);
        }

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

        void Update()
        {
            if (current == null || current.frames.Length == 0) return;
            timer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, current.fps);
            while (timer >= step)
            {
                if (frame < current.frames.Length - 1) { frame++; timer -= step; }
                else if (current.loop) { frame = 0; timer -= step; }
                else break;
            }
            sr.sprite = current.frames[frame];
        }

        Clip Find(string clipName)
        {
            foreach (var c in clips) if (c != null && c.name == clipName) return c;
            return null;
        }
    }
}
