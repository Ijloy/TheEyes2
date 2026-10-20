using UnityEngine;

namespace DarkFantasy
{
    // Contrato: toda clase que implemente esto puede ser golpeada por los ataques.
    /// <summary>Cualquier cosa que pueda recibir daño: jugador y enemigos.</summary>
    public interface IDamageable
    {
        // Recibir daño. amount = cuánta vida quita, knockback = empujón (dirección y fuerza),
        // heavy = golpe fuerte (más efecto visual / empujón mayor).
        void TakeDamage(float amount, Vector2 knockback, bool heavy);
    }
}
