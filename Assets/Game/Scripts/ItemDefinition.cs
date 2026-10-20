using UnityEngine;

namespace DarkFantasy
{
    // Rarezas posibles de un objeto (de menos a más valioso).
    public enum ItemRarity { Comun, Ancestral, Legendaria }

    /// <summary>
    /// Un objeto que se puede soltar y recoger (runas, materiales...).
    /// Clic derecho en Project → Create → Dark Fantasy → Objeto.
    /// </summary>
    [CreateAssetMenu(menuName = "Dark Fantasy/Objeto", fileName = "NuevoObjeto")]
    public class ItemDefinition : ScriptableObject
    {
        // Nombre que se muestra en pantalla.
        public string displayName = "Runa oscura";
        // Imagen del objeto (en el suelo y en el inventario).
        public Sprite icon;
        // Rareza del objeto.
        public ItemRarity rarity = ItemRarity.Comun;

        [Tooltip("Color del brillo del objeto en el suelo")]
        public Color glowColor = new Color(0.7f, 0.3f, 1f, 1f);

        // Texto descriptivo del objeto.
        [TextArea] public string description;
    }
}
