# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Responde siempre en español. El juego está cambiando a vista isométrica 2D estilo Diablo 2 con movimiento en 8 direcciones y cambio de personaje estilo Genshin Impact. La versión actual (side scroller) es un prototipo provisional.

## Proyecto

"The Eye" es un prototipo de acción 2D de fantasía oscura en pixel art, hecho en **Unity 6000.5.0f1** con URP 2D (`com.unity.render-pipelines.universal` 17.5) y el nuevo Input System. El código, los comentarios, los nombres de assets, los menús y los textos del juego están en **español**; el código nuevo debe seguir igual (p. ej. `Suelo`, `Muneco`, `Hechicero`, "Alma en agonía", "El Que Observa").

Todo el contenido del juego está en `Assets/Game/` (`Scripts/`, `Scripts/Editor/`, `Art/`, `Animations/`, `Data/`). La única escena es `Assets/Scenes/SampleScene.unity`. No hay asmdefs (todo compila en `Assembly-CSharp` / `Assembly-CSharp-Editor`), no hay tests y no hay repositorio git.

## Trabajar con el editor

No hay un flujo de compilación ni de tests por línea de comandos; todo se hace a través del Editor de Unity abierto. El servidor MCP `unity-editor-mcp` está conectado y es la forma de compilar, inspeccionar y ejecutar:
- Tras editar scripts: `recompile` y luego revisar `console` en busca de errores de compilación.
- Ejecutar el juego: `editor_play` / `editor_stop`; inspeccionar con `capture_game_view`, `get_scene_hierarchy`, `get_component_properties`.
- Lanzar los menús de montaje (ver abajo) con la herramienta `menu` (p. ej. `Tools/Caballero Maldito/2. Montar escena de prueba`) y después `save_scene`.
- Existe `run_tests` (Unity Test Framework está instalado), pero ahora mismo no hay ensamblados de tests.
- Probar con teclado en Play desde MCP: el Input System ignora el teclado si Unity no tiene el foco (`backgroundBehavior = ResetAndDisableNonBackgroundDevices`, `editorInputBehaviorInPlayMode = PointersAndKeyboardsRespectGameViewFocus`) y `simulate_key` deja teclas "pegadas". Lo fiable es, con `eval`, cambiar esos dos ajustes en memoria (y restaurarlos después) y enviar el estado con `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W, Key.D))`; `new KeyboardState()` suelta todo.

## Arquitectura

**La escena y los assets se generan por código.** `Assets/Game/Scripts/Editor/KnightPrototypeSetup.cs` define el menú `Tools > Caballero Maldito`, que es la fuente de verdad de cómo se monta el prototipo:
1. *Crear animaciones*: reimporta los PNG de los cuadros como sprites con filtro Point (32 PPU, pivote personalizado en los pies) y genera los clips `.anim` y `CaballeroMaldito.controller` a partir de la tabla `Clips`. Cada clip lista carpetas de origen por prioridad: primero la del arte definitivo (p. ej. `Attack_1`) y después un sustituto provisional (p. ej. cuadros de `Cross_Punch`). Para cambiar el provisional basta con poner una carpeta con el nombre esperado en `Assets/Game/Art/CaballeroMaldito/` y volver a ejecutar el paso 1. Los clips existentes se sobrescriben en el mismo asset (`CopySerialized`) para conservar las referencias.
2. *Montar escena de prueba*: borra y vuelve a crear el suelo, el jugador, el muñeco de entrenamiento, el hechicero, la cámara (con PixelPerfectCamera de URP, resolución de referencia 640×360), la Light2D y el fondo con parallax.
3–6. Pasos incrementales (equipar espada, añadir botín, añadir hechicero, añadir fondo) que se pueden ejecutar sobre una escena ya montada.
8. *Montar escena isométrica*: crea desde cero `Assets/Scenes/PlazaIsometrica.unity` (no toca SampleScene). La plaza (`Isometrico/Escenarios/plaza_gotica_1280.png`) es una sola imagen plana con pivote abajo a la izquierda en (0,0); el borde de la zona caminable (`IsoWalkable`, un `EdgeCollider2D`), los obstáculos sueltos (`IsoObstacles`) y los objetos recortados (`IsoCutouts`, guardados en `Isometrico/Recortes/`) se definen en **píxeles de esa imagen** (origen arriba a la izquierda) y se convierten con `ToWorld`. Criterio: lo que está contra una pared al norte solo lleva collider en su base; lo alto que se puede rodear por detrás (estatua, pozo, carro, cajas, faroles, pilares, árboles) va como recorte con su punto de apoyo en el suelo; las verjas y muros del sur no son recortes (sus huecos taparían al caballero), así que el borde caminable pasa por encima de ellos. Los recortes son uniones de polígonos (`Px`) y, para ramas, trazos con grosor (`Stroke`). También activa el orden por Y (Transparency Sort Mode Custom Axis 0,1,0) en el `Renderer2D` del proyecto, lo que afecta a todas las escenas.

**Vista isométrica:** `IsoPlayerController` mueve al caballero en 8 direcciones sin gravedad (entrada normalizada) y usa siempre la imagen fija de cada dirección (índices 0-7 = S, SE, E, NE, N, NO, O, SO; los cuadros de `Caminar_Sur` ya no se usan porque no casan con el sprite parado). La animación es procedural e igual para las 8 direcciones: rebote de 0-2 px por paso (redondeado a píxel), squash & stretch, transición suave al parar y respiración en reposo. Solo se anima el hijo `Visual` (SpriteRenderer); la raíz lleva Rigidbody2D, collider y un `SortingGroup`, que ordena por la Y de la raíz para que el rebote no haga temblar el orden. Los recortes de la plaza usan `spriteSortPoint = Pivot` con el pivote en su base. `CameraFollow` tiene límites opcionales (`useBounds`) para no enseñar fuera del mapa. El menú 8 también pone la vista Game en 1920x1080 fija (`SetGameViewFullHD`, por reflexión), porque la Pixel Perfect Camera avisa si la vista Game tiene una resolución impar. La cámara isométrica usa referencia 640x360 (x3 en 1080p; en 1080p solo hay zooms enteros: 480x270 = x4 se probó y se veía demasiado cerca) con Upscale Render Texture y Pixel Snapping activados, para que los píxeles midan siempre lo mismo con cualquier tamaño de ventana. Los pivotes de los sprites de PixelLab se calculan por cuadro en la fila opaca más baja (`LowestOpaquePivot`), para que el pie apoyado toque siempre el suelo.

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
