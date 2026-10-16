using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy
{
    /// <summary>Inventario sencillo: cuántos objetos de cada tipo lleva el jugador.</summary>
    public class PlayerInventory : MonoBehaviour
    {
        readonly Dictionary<ItemDefinition, int> counts = new Dictionary<ItemDefinition, int>();
        readonly List<ItemDefinition> order = new List<ItemDefinition>();

        string lastPicked;
        float lastPickedTimer;

        public void Add(ItemDefinition item, int amount = 1)
        {
            if (item == null) return;
            if (!counts.ContainsKey(item)) { counts[item] = 0; order.Add(item); }
            counts[item] += amount;
            lastPicked = $"+{amount} {item.displayName}";
            lastPickedTimer = 2f;
        }

        public int Count(ItemDefinition item) => item != null && counts.TryGetValue(item, out int n) ? n : 0;

        void Update()
        {
            if (lastPickedTimer > 0f) lastPickedTimer -= Time.deltaTime;
        }

        void OnGUI()
        {
            float y = Screen.height - 40f - order.Count * 26f;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            style.normal.textColor = Color.white;

            if (order.Count > 0)
                GUI.Box(new Rect(10, y - 30, 230, order.Count * 26f + 40), "INVENTARIO");

            foreach (var item in order)
            {
                if (item.icon != null)
                {
                    Rect r = item.icon.textureRect;
                    var tex = item.icon.texture;
                    var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
                    GUI.DrawTextureWithTexCoords(new Rect(20, y, 20, 22), tex, uv);
                }
                GUI.Label(new Rect(48, y, 190, 24), $"{item.displayName}  x{counts[item]}", style);
                y += 26f;
            }

            if (lastPickedTimer > 0f)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(0.85f, 0.6f, 1f, Mathf.Clamp01(lastPickedTimer));
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 30), lastPicked, big);
            }
        }
    }
}
