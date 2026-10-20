using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>Inventario sencillo: cuántos objetos de cada tipo lleva el jugador.</summary>
    public class PlayerInventory : MonoBehaviour
    {
        // Cuántas unidades hay de cada objeto.
        readonly Dictionary<ItemDefinition, int> counts = new Dictionary<ItemDefinition, int>();
        // Orden en que se recogieron (para mostrarlos siempre en el mismo orden).
        readonly List<ItemDefinition> order = new List<ItemDefinition>();

        // Texto del último objeto recogido ("+1 Runa") y cuánto tiempo sigue visible.
        string lastPicked;
        float lastPickedTimer;

        // Suma objetos al inventario y activa el aviso en pantalla.
        public void Add(ItemDefinition item, int amount = 1)
        {
            if (item == null) return;
            // Si es la primera vez que se recoge este objeto, se crea su entrada.
            if (!counts.ContainsKey(item)) { counts[item] = 0; order.Add(item); }
            counts[item] += amount;
            lastPicked = $"+{amount} {item.displayName}";
            // El aviso dura 2 segundos.
            lastPickedTimer = 2f;
        }

        // Devuelve cuántas unidades hay de un objeto (0 si no tiene).
        public int Count(ItemDefinition item) => item != null && counts.TryGetValue(item, out int n) ? n : 0;

        // Cada frame descuenta el tiempo del aviso.
        void Update()
        {
            if (lastPickedTimer > 0f) lastPickedTimer -= Time.deltaTime;
        }

        // Dibuja el inventario y el aviso en pantalla (interfaz simple de prueba).
        void OnGUI()
        {
            // Posición vertical inicial: la lista se dibuja abajo a la izquierda.
            float y = Screen.height - 40f - order.Count * 26f;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            style.normal.textColor = Color.white;

            // Caja de fondo con el título, solo si hay algo en el inventario.
            if (order.Count > 0)
                GUI.Box(new Rect(10, y - 30, 230, order.Count * 26f + 40), "INVENTARIO");

            // Una fila por objeto: ícono + nombre + cantidad.
            foreach (var item in order)
            {
                if (item.icon != null)
                {
                    // Recorta el ícono de su textura (por si viene de una hoja de sprites).
                    Rect r = item.icon.textureRect;
                    var tex = item.icon.texture;
                    var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
                    GUI.DrawTextureWithTexCoords(new Rect(20, y, 20, 22), tex, uv);
                }
                GUI.Label(new Rect(48, y, 190, 24), $"{item.displayName}  x{counts[item]}", style);
                y += 26f;
            }

            // Aviso grande en el centro que se desvanece al terminar el tiempo.
            if (lastPickedTimer > 0f)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(0.85f, 0.6f, 1f, Mathf.Clamp01(lastPickedTimer));
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 30), lastPicked, big);
            }
        }
    }
}
