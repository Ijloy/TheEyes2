# Readme.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Responde siempre en español. El juego es un **side scroller** 2D. Se probó una vista isométrica estilo Diablo 2 (plaza, 8 direcciones, combo con tajos), pero se descartó para simplificar el juego; está guardada en el commit `b935c69` por si se quiere recuperar algo.

## Proyecto

"The Eye" es un prototipo de acción 2D de fantasía oscura en pixel art, hecho en **Unity 6000.5.0f1** con URP 2D (`com.unity.render-pipelines.universal` 17.5) y el nuevo Input System. El código, los comentarios, los nombres de assets, los menús y los textos del juego están en **español**; el código nuevo debe seguir igual (p. ej. `Suelo`, `Muneco`, `Hechicero`, "Alma en agonía", "El Que Observa").

Todo el contenido del juego está en `Assets/Game/` (`Scripts/`, `Scripts/Editor/`, `Art/`, `Animations/`, `Data/`). La única escena es `Assets/Scenes/SampleScene.unity`. No hay asmdefs (todo compila en `Assembly-CSharp` / `Assembly-CSharp-Editor`), no hay tests. El proyecto es un repositorio git (rama `main`) con `.gitignore` de Unity (fuera `Library/`, `Temp/`, `Logs/`, `UserSettings/` y los `.csproj`/`.slnx` generados) y `.gitattributes` que fija LF en los YAML de Unity.

## Trabajar con el editor

No hay un flujo de compilación ni de tests por línea de comandos; todo se hace a través del Editor de Unity abierto. El servidor MCP `unity-editor-mcp` está conectado y es la forma de compilar, inspeccionar y ejecutar:
- Tras editar scripts: `recompile` y luego revisar `console` en busca de errores de compilación.
- Ejecutar el juego: `editor_play` / `editor_stop`; inspeccionar con `capture_game_view`, `get_scene_hierarchy`, `get_component_properties`.
- Lanzar los menús de montaje (ver abajo) con la herramienta `menu` (p. ej. `Tools/Caballero Maldito/2. Montar escena de prueba`) y después `save_scene`.
- Existe `run_tests` (Unity Test Framework está instalado), pero ahora mismo no hay ensamblados de tests.
- Probar con teclado en Play desde MCP: el Input System ignora el teclado si Unity no tiene el foco (`backgroundBehavior = ResetAndDisableNonBackgroundDevices`, `editorInputBehaviorInPlayMode = PointersAndKeyboardsRespectGameViewFocus`) y `simulate_key` deja teclas "pegadas". Lo fiable es, con `eval`, cambiar esos dos ajustes en memoria (y restaurarlos después) y enviar el estado con `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.D, Key.J))`; `new KeyboardState()` suelta todo. Si Unity no tiene el foco, el juego no avanza (`Time.frameCount` se queda quieto): pon `Application.runInBackground = true` con `eval` mientras pruebas y vuelve a dejarlo en `false`.

## Arquitectura

**La escena y los assets se generan por código.** `Assets/Game/Scripts/Editor/KnightPrototypeSetup.cs` define el menú `Tools > Caballero Maldito`, que es la fuente de verdad de cómo se monta el prototipo:
1. *Crear animaciones*: reimporta los PNG de los cuadros como sprites con filtro Point (32 PPU, pivote personalizado en los pies) y genera los clips `.anim` y `CaballeroMaldito.controller` a partir de la tabla `Clips`. Cada clip lista carpetas de origen por prioridad: primero la del arte definitivo (p. ej. `Attack_1`) y después un sustituto provisional (p. ej. cuadros de `Cross_Punch`). Para cambiar el provisional basta con poner una carpeta con el nombre esperado en `Assets/Game/Art/CaballeroMaldito/` y volver a ejecutar el paso 1. Los clips existentes se sobrescriben en el mismo asset (`CopySerialized`) para conservar las referencias.
2. *Montar escena de prueba*: borra y vuelve a crear el suelo, el jugador, el muñeco de entrenamiento, el hechicero, la cámara (con PixelPerfectCamera de URP, resolución de referencia 640×360), la Light2D y el fondo con parallax.
3–6. Pasos incrementales (equipar espada, añadir botín, añadir hechicero, añadir fondo) que se pueden ejecutar sobre una escena ya montada.

Si cambias la configuración de componentes, el tamaño de los colliders, los ajustes de importación de sprites o los valores por defecto de los ScriptableObject, actualiza también este script; si no, al volver a ejecutar el menú se revertirá el cambio. Los tipos de URP se buscan por reflexión (`FindType`) y se configuran mediante `SerializedObject`.

**El Animator no tiene transiciones.** `PlayerController` cambia de animación directamente con `Animator.Play(nombreDelEstado)`; los nombres de estado deben coincidir con los de los clips (`Idle`, `Walk`, `JumpUp`, `JumpFall`, `Land`, `Hurt`, `Attack1`–`Attack4`, `Charged`, `PickUp`). La duración de los clips se lee en `Awake` para sincronizar ataques y momentos de impacto. Los enemigos no usan Animator, sino el más ligero `SpriteFrameAnimator` (clips con nombre formados por arrays de sprites).

**Núcleo de jugabilidad** (namespace `DarkFantasy`):
- `PlayerController`: máquina de estados hecha a mano (`Normal/Attacking/Charged/Hurt/PickingUp`), singleton `PlayerController.Instance`. Combo de 4 golpes con J y buffer de entrada; solo un 4.º golpe que *conecta* da un "Alma" (máx. 3); el ataque cargado con K consume las 3 Almas; la pasiva de ira acumula +ATK al recibir daño. Los golpes usan `Physics2D.OverlapBox` y llaman a `IDamageable.TakeDamage(amount, knockback, heavy)`. Lanza los eventos `AttackStarted(step, duration, hitTime)` / `AttackEnded` (step 5 = ataque cargado).
- `WeaponVisual`: se suscribe a esos eventos y anima por código un sprite de arma separado y la estela del tajo en cada golpe (ángulos definidos mirando a la derecha). Las estadísticas y el aspecto del arma salen de un ScriptableObject `WeaponDefinition`; cambiar de arma no debería requerir animaciones nuevas del personaje.
- `IDamageable` lo implementan `PlayerController`, `TrainingDummy` y `SorcererEnemy`. Cada enemigo gestiona su propia muerte/reaparición, números de daño y barra de vida.
- Botín: `LootDropper` (tabla de `ItemDefinition` + probabilidad) → `Pickup.Spawn` (arco simulado por el propio objeto hasta un `groundY` fijo; registrados en la lista estática `Pickup.All`) → el jugador pulsa E → `PlayerInventory.Add`.
- `GameInput`: envoltorio estático del teclado compatible con el Input System y con el Input clásico (`#if ENABLE_INPUT_SYSTEM`). Los controles nuevos se añaden aquí en vez de leer la entrada en otros sitios. El asset `InputSystem_Actions.inputactions` es el que trae por defecto la plantilla de Unity y no se usa.

**Convenciones:**
- Los sprites base (caballero y hechicero) miran a la **izquierda**; para mirar a la derecha se usa `SpriteRenderer.flipX = true`.
- API de Unity 6: usar `Rigidbody2D.linearVelocity` (envuelto en una propiedad `Velocity`) y `FindAnyObjectByType`.
- El HUD, las barras de vida y los números de daño son código `OnGUI` de prototipo; los efectos (humo, proyectiles, texturas provisionales) se generan en tiempo de ejecución con `Texture2D`/`Sprite.Create`.
- Orden de dibujo (sortingOrder): fondo −100…−80, muñeco 5, objetos recogibles 7–8, hechicero 9, jugador 10, arma 11, estela 12, humo 14.
