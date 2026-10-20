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
        // A quién sigue la cámara (el jugador).
        public Transform target;
        // Desplazamiento fijo respecto al jugador (por defecto, un poco más arriba).
        public Vector2 offset = new Vector2(0f, 1.5f);
        // Suavizado: más alto = la cámara tarda más en alcanzar al jugador.
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

        // Variables internas del suavizado y del adelanto.
        Vector3 velocity;
        Vector2 currentLookAhead, lookAheadVelocity;
        Camera cam;
        Rigidbody2D targetBody;

        // Guarda la cámara de este objeto.
        void Awake() => cam = GetComponent<Camera>();

        // Se ejecuta después de que el jugador se movió en el frame.
        void LateUpdate()
        {
            if (target == null) return;
            // Calcula cuánto adelantarse según hacia dónde camina.
            UpdateLookAhead();
            // Punto objetivo = jugador + desplazamiento + adelanto.
            Vector2 p = (Vector2)target.position + offset + currentLookAhead;
            Vector3 goal = new Vector3(p.x, p.y, transform.position.z);
            // Si hay límites, no deja que la cámara se salga del mapa.
            if (useBounds) goal = ClampToBounds(goal);
            // Mueve la cámara suavemente hacia el objetivo.
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }

        // Calcula el adelanto de la cámara según la velocidad del jugador.
        void UpdateLookAhead()
        {
            // Si el adelanto está en 0, no hace nada.
            if (lookAhead == Vector2.zero) { currentLookAhead = Vector2.zero; return; }
            // Busca el Rigidbody2D del jugador (para leer su velocidad).
            if (targetBody == null || targetBody.transform != target) targetBody = target.GetComponent<Rigidbody2D>();

            // Si se mueve, el objetivo es adelantarse en su dirección; si está quieto, volver al centro.
            Vector2 v = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
            bool moving = v.sqrMagnitude > 0.01f;
            Vector2 goal = moving ? Vector2.Scale(v.normalized, lookAhead) : Vector2.zero;
            currentLookAhead = Vector2.SmoothDamp(currentLookAhead, goal, ref lookAheadVelocity,
                moving ? lookAheadTime : lookAheadReturnTime);
        }

        // Recorta la posición para que la cámara no muestre nada fuera de 'bounds'.
        public Vector3 ClampToBounds(Vector3 p)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return p;

            // Mitad del alto y del ancho de lo que ve la cámara.
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            // Si el mapa es más pequeño que la pantalla se centra; si no, se limita a los bordes.
            p.x = bounds.width <= halfW * 2f ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW);
            p.y = bounds.height <= halfH * 2f ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH);
            return p;
        }
    }
}
