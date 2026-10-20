using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkFantasy
{
    /// <summary>
    /// Hace parpadear suavemente una Light2D (vela, antorcha) variando su intensidad y radio con ruido Perlin.
    /// Cada instancia empieza en un punto distinto del ruido para que varias luces no parpadeen a la vez.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class FlickerLight : MonoBehaviour
    {
        [Tooltip("Variación máxima respecto a los valores iniciales (0.15 = ±15%)")]
        [Range(0f, 0.5f)] public float variation = 0.15f;
        [Tooltip("Velocidad del parpadeo")]
        public float speed = 2.5f;

        // Luz, valores de partida y punto del ruido propio de esta luz.
        Light2D light2D;
        float baseIntensity;
        float baseRadius;
        float seed;

        // Guarda los valores iniciales y elige un punto aleatorio del ruido.
        void Awake()
        {
            light2D = GetComponent<Light2D>();
            baseIntensity = light2D.intensity;
            baseRadius = light2D.pointLightOuterRadius;
            seed = Random.Range(0f, 1000f);
        }

        // Cada cuadro: intensidad y radio siguen dos curvas de ruido distintas (valores entre -1 y 1).
        void Update()
        {
            float t = Time.time * speed;
            float n1 = Mathf.PerlinNoise(seed, t) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(seed + 37f, t * 0.7f) * 2f - 1f;
            light2D.intensity = baseIntensity * (1f + n1 * variation);
            light2D.pointLightOuterRadius = baseRadius * (1f + n2 * variation);
        }
    }
}
