using UnityEngine;

namespace DarkFantasy
{
    public enum ItemRarity { Comun, Ancestral, Legendaria }

    /// <summary>
    /// Un objeto que se puede soltar y recoger (runas, materiales...).
    /// Clic derecho en Project → Create → Dark Fantasy → Objeto.
    /// </summary>
    [CreateAssetMenu(menuName = "Dark Fantasy/Objeto", fileName = "NuevoObjeto")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Runa oscura";
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Comun;

        [Tooltip("Color del brillo del objeto en el suelo")]
        public Color glowColor = new Color(0.7f, 0.3f, 1f, 1f);

        [TextArea] public string description;
    }
}
