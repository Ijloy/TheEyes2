using UnityEngine;

namespace DarkFantasy
{
    /// <summary>Tabla de botín: qué puede soltar un enemigo al morir y con qué probabilidad.</summary>
    public class LootDropper : MonoBehaviour
    {
        // Una fila de la tabla de botín (se edita desde el Inspector).
        [System.Serializable]
        public class Entry
        {
            // Qué objeto puede soltar.
            public ItemDefinition item;
            // Probabilidad de soltarlo (0 = nunca, 1 = siempre).
            [Range(0f, 1f)] public float chance = 1f;
            // Cantidad mínima y máxima si sale.
            public int min = 1;
            public int max = 1;
        }

        // Lista de todo lo que puede soltar este enemigo.
        public Entry[] table = new Entry[0];

        [Tooltip("Altura del suelo donde caen los objetos (y del mundo)")]
        public float groundY = -1f;

        // Se llama cuando el enemigo muere: tira los objetos al suelo.
        public void Drop()
        {
            foreach (var e in table)
            {
                // Si la fila está vacía o falla la probabilidad, se salta esta fila.
                if (e.item == null || Random.value > e.chance) continue;
                // Cuántas unidades caen (entre min y max, ambos incluidos).
                int n = Random.Range(e.min, e.max + 1);
                for (int i = 0; i < n; i++)
                    // Crea cada objeto un poco por encima del enemigo para que salte y caiga.
                    Pickup.Spawn(e.item, transform.position + Vector3.up * 0.5f, groundY);
            }
        }
    }
}
