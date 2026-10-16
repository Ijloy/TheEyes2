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
        public Sprite sprite;
        [Range(0f, 1f)] public float followFactor = 0.8f;
        public int sortingOrder = -100;
        [Tooltip("Desplazamiento vertical respecto al centro de la cámara")]
        public float verticalOffset = 0f;

        Transform cam;
        Transform[] tiles;
        float width;
        float startCamX;
        float startX;

        void Start()
        {
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

            startCamX = cam.position.x;
            startX = cam.position.x;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (cam == null) return;

            // Posición base de la capa: se desplaza una fracción del movimiento de la cámara
            float camDelta = cam.position.x - startCamX;
            float baseX = startX + camDelta * followFactor;

            // Recoloca las copias para que siempre cubran la pantalla
            float offset = Mathf.Repeat(cam.position.x - baseX, width);
            float centerX = cam.position.x - offset;

            float y = cam.position.y + verticalOffset;
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].position = new Vector3(centerX + (i - 1) * width + width * 0.5f, y, 10f);
        }
    }
}
