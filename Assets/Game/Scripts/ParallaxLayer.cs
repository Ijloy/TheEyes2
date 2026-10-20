using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Capa de fondo con parallax que se repite sin fin en horizontal.
    /// followFactor: 1 = se mueve con la cámara (parece infinitamente lejos, ej. el cielo),
    ///               0 = fija en el mundo (como el suelo). Las capas intermedias dan profundidad.
    /// Crea 3 copias del sprite una al lado de otra y las va recolocando.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        // Imagen de esta capa del fondo.
        public Sprite sprite;
        // Cuánto sigue a la cámara (ver explicación arriba).
        [Range(0f, 1f)] public float followFactor = 0.8f;
        // Orden de dibujo: números más bajos se dibujan más atrás.
        public int sortingOrder = -100;
        [Tooltip("Desplazamiento vertical respecto al centro de la cámara (solo si no está anclada al suelo)")]
        public float verticalOffset = 0f;
        [Tooltip("Fija la capa en vertical con el borde inferior de la imagen en groundY, para que los edificios apoyen en el suelo y no suban con la cámara al saltar")]
        public bool anchorToGround;
        [Tooltip("Altura del mundo donde apoya el borde inferior de la imagen (borde superior del suelo)")]
        public float groundY = -1f;

        // Referencias internas: cámara, las 3 copias, ancho de la imagen y posiciones iniciales.
        Transform cam;
        Transform[] tiles;
        float width;
        float startCamX;
        float startX;

        // Al empezar: crea las 3 copias de la imagen como hijos de este objeto.
        void Start()
        {
            // Sin cámara o sin imagen no hay nada que hacer: se desactiva.
            if (Camera.main == null || sprite == null) { enabled = false; return; }
            cam = Camera.main.transform;
            width = sprite.bounds.size.x;

            tiles = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"{name}_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortingOrder;
                tiles[i] = go.transform;
            }

            // Guarda dónde estaba la cámara al inicio, para medir cuánto se ha movido.
            startCamX = cam.position.x;
            startX = cam.position.x;
            // Coloca las copias desde el primer frame.
            LateUpdate();
        }

        // Después de que la cámara se mueve, recoloca la capa.
        void LateUpdate()
        {
            if (cam == null) return;

            // Posición base de la capa: se desplaza una fracción del movimiento de la cámara
            float camDelta = cam.position.x - startCamX;
            float baseX = startX + camDelta * followFactor;

            // Recoloca las copias para que siempre cubran la pantalla
            float offset = Mathf.Repeat(cam.position.x - baseX, width);
            float centerX = cam.position.x - offset;

            // Anclada: el borde inferior de la imagen queda en groundY. Si no, sigue a la cámara en vertical.
            float y = anchorToGround ? groundY - sprite.bounds.min.y : cam.position.y + verticalOffset;
            // Pone las 3 copias juntas: izquierda, centro y derecha (z = 10, detrás de todo).
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].position = new Vector3(centerX + (i - 1) * width + width * 0.5f, y, 10f);
        }
    }
}
