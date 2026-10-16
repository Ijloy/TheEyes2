using UnityEngine;

namespace DarkFantasy
{
    /// <summary>Tabla de botín: qué puede soltar un enemigo al morir y con qué probabilidad.</summary>
    public class LootDropper : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public ItemDefinition item;
            [Range(0f, 1f)] public float chance = 1f;
            public int min = 1;
            public int max = 1;
        }

        public Entry[] table = new Entry[0];

        [Tooltip("Altura del suelo donde caen los objetos (y del mundo)")]
        public float groundY = -1f;

        public void Drop()
        {
            foreach (var e in table)
            {
                if (e.item == null || Random.value > e.chance) continue;
                int n = Random.Range(e.min, e.max + 1);
                for (int i = 0; i < n; i++)
                    Pickup.Spawn(e.item, transform.position + Vector3.up * 0.5f, groundY);
            }
        }
    }
}
