using UnityEngine;

namespace DarkFantasy
{
    /// <summary>Cualquier cosa que pueda recibir daño: jugador, enemigos, muñeco de prueba.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector2 knockback, bool heavy);
    }
}
