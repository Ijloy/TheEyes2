using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Cámara que sigue al jugador con un poco de suavizado.
    /// Con <see cref="lookAhead"/> se adelanta hacia donde camina el jugador, para ver antes lo que viene
    /// (y menos lo que queda detrás) sin alejar la cámara.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new Vector2(0f, 1.5f);
        public float smoothTime = 0.15f;

        [Header("Adelanto hacia donde se camina")]
        [Tooltip("Cuánto se adelanta la cámara (unidades del mundo) en horizontal y en vertical. 0 = sin adelanto")]
        public Vector2 lookAhead = Vector2.zero;
        [Tooltip("Tiempo que tarda el adelanto en llegar a su sitio al empezar a caminar o cambiar de dirección")]
        public float lookAheadTime = 0.6f;
        [Tooltip("Tiempo que tarda la cámara en volver al centro al pararse")]
        public float lookAheadReturnTime = 1.2f;

        [Tooltip("Si está activo, la cámara no enseña nada fuera de estos límites (en unidades del mundo)")]
        public bool useBounds;
        public Rect bounds;

        Vector3 velocity;
        Vector2 currentLookAhead, lookAheadVelocity;
        Camera cam;
        Rigidbody2D targetBody;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null) return;
            UpdateLookAhead();
            Vector2 p = (Vector2)target.position + offset + currentLookAhead;
            Vector3 goal = new Vector3(p.x, p.y, transform.position.z);
            if (useBounds) goal = ClampToBounds(goal);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }

        void UpdateLookAhead()
        {
            if (lookAhead == Vector2.zero) { currentLookAhead = Vector2.zero; return; }
            if (targetBody == null || targetBody.transform != target) targetBody = target.GetComponent<Rigidbody2D>();

            Vector2 v = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
            bool moving = v.sqrMagnitude > 0.01f;
            Vector2 goal = moving ? Vector2.Scale(v.normalized, lookAhead) : Vector2.zero;
            currentLookAhead = Vector2.SmoothDamp(currentLookAhead, goal, ref lookAheadVelocity,
                moving ? lookAheadTime : lookAheadReturnTime);
        }

        public Vector3 ClampToBounds(Vector3 p)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return p;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            p.x = bounds.width <= halfW * 2f ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW);
            p.y = bounds.height <= halfH * 2f ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH);
            return p;
        }
    }
}
