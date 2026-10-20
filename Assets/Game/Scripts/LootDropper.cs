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

        [Header("Cofre")]
        [Tooltip("Al morir aparece un cofre en el suelo que se abre y suelta el botín. Desactivado: el botín sale directamente del enemigo")]
        public bool useChest = true;
        [Tooltip("Cuadros de la animación de abrir el cofre (sin cuadros se usa el comportamiento sin cofre)")]
        public Sprite[] chestFrames = new Sprite[0];

        // Se llama cuando el enemigo muere: aparece el cofre (que soltará el botín) o tira los objetos al suelo.
        public void Drop()
        {
            if (useChest && chestFrames != null && chestFrames.Length > 0)
            {
                // El cofre apoya su base en el suelo, debajo de donde murió el enemigo
                LootChest.Spawn(this, chestFrames, new Vector3(transform.position.x, groundY, 0f));
                return;
            }
            // Crea los objetos un poco por encima del enemigo para que salten y caigan.
            SpawnLoot(transform.position + Vector3.up * 0.5f);
        }

        /// <summary>Tira al suelo los objetos de la tabla desde 'origin' (saltan y caen hasta groundY).</summary>
        public void SpawnLoot(Vector3 origin)
        {
            foreach (var e in table)
            {
                // Si la fila está vacía o falla la probabilidad, se salta esta fila.
                if (e.item == null || Random.value > e.chance) continue;
                // Cuántas unidades caen (entre min y max, ambos incluidos).
                int n = Random.Range(e.min, e.max + 1);
                for (int i = 0; i < n; i++)
                    Pickup.Spawn(e.item, origin, groundY);
            }
        }
    }
}
