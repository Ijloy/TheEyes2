using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Cámara que sigue al jugador con un poco de suavizado.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        // A quién sigue la cámara (el jugador).
        public Transform target;
        // Desplazamiento fijo respecto al jugador (por defecto, un poco más arriba).
        public Vector2 offset = new Vector2(0f, 1.5f);
        // Suavizado: más alto = la cámara tarda más en alcanzar al jugador.
        public float smoothTime = 0.15f;

        // Variable interna del suavizado.
        Vector3 velocity;

        // Se ejecuta después de que el jugador se movió en el frame.
        void LateUpdate()
        {
            if (target == null) return;
            // Punto objetivo = jugador + desplazamiento.
            Vector2 p = (Vector2)target.position + offset;
            Vector3 goal = new Vector3(p.x, p.y, transform.position.z);
            // Mueve la cámara suavemente hacia el objetivo.
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
