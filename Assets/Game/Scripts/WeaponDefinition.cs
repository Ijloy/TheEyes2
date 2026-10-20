using UnityEngine;

namespace DarkFantasy
{
    /// <summary>
    /// Datos de un arma. Cada arma del juego será un asset de este tipo
    /// (clic derecho en Project → Create → Dark Fantasy → Arma).
    /// Cambiar de arma = cambiar este asset; las animaciones del personaje no se tocan.
    /// </summary>
    [CreateAssetMenu(menuName = "Dark Fantasy/Arma", fileName = "NuevaArma")]
    public class WeaponDefinition : ScriptableObject
    {
        // Nombre del arma.
        public string displayName = "Espada maldita";

        [Tooltip("Sprite del arma apuntando hacia ARRIBA, con el pivote en la empuñadura")]
        public Sprite sprite;

        [Header("Combate")]
        [Tooltip("Multiplica el daño de todos los golpes")]
        public float damageMultiplier = 1f;

        [Header("Visual")]
        [Tooltip("Color de la estela del tajo")]
        public Color slashColor = new Color(0.75f, 0.35f, 1f, 0.9f);

        [Tooltip("Si está desactivado, el arma solo aparece al atacar (se materializa)")]
        public bool visibleWhenIdle = false;

        [TextArea]
        [Tooltip("Pasiva del arma (pendiente de implementar)")]
        // Texto de la pasiva del arma (todavía solo descriptivo).
        public string passiveDescription;
    }
}
