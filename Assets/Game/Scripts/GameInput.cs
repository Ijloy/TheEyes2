using UnityEngine;
// Solo se usa la librería del Input System nuevo si el proyecto la tiene activada.
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DarkFantasy
{
    /// <summary>
    /// Lectura de teclado compatible con el Input System nuevo (por defecto en Unity 6)
    /// y con el Input Manager clásico.
    /// Controles: A/D o flechas = moverse (W/S también en la vista isométrica), Espacio = saltar, J = atacar,
    /// K = ataque cargado, E = recoger, H = simular que te golpean (para probar la pasiva).
    /// </summary>
    public static class GameInput
    {
        // Eje horizontal: -1 = izquierda (A / flecha izq.), +1 = derecha (D / flecha der.), 0 = nada.
        public static float Horizontal
        {
            get
            {
// Versión con el Input System nuevo.
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return 0f;
                float h = 0f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) h -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) h += 1f;
                return h;
// Versión con el Input Manager clásico.
#else
                return Input.GetAxisRaw("Horizontal");
#endif
            }
        }

        // Eje vertical: -1 = abajo (S / flecha abajo), +1 = arriba (W / flecha arriba).
        /// <summary>W/S o flechas arriba/abajo (para la vista isométrica).</summary>
        public static float Vertical
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return 0f;
                float v = 0f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v += 1f;
                return v;
#else
                return Input.GetAxisRaw("Vertical");
#endif
            }
        }

        // Saltar: true solo en el frame en que se presiona Espacio, W o flecha arriba (prototipo lateral).
        public static bool JumpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame);
#else
                return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
#endif
            }
        }

        // Atacar: true en el frame en que se presiona J.
        public static bool AttackPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.jKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.J);
#endif
            }
        }

        // Ataque cargado: true en el frame en que se presiona K.
        public static bool ChargedPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.kKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.K);
#endif
            }
        }

        // Interactuar / recoger: true en el frame en que se presiona E.
        public static bool InteractPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }

        // Prueba: H simula que el jugador recibe un golpe (para probar la pasiva).
        public static bool TestHitPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.hKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.H);
#endif
            }
        }
    }
}
