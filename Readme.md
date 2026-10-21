# The Eye

**The Eye** es un prototipo de juego de acción 2D de fantasía oscura en pixel art, de desplazamiento lateral (*side scroller*). El jugador controla a un caballero maldito que se abre paso con su espada, acumula almas a base de golpes certeros y las libera en un ataque cargado devastador.

Hecho en **Unity 6000.5.0f1** con **URP 2D** y el nuevo **Input System**.

---

## Jugabilidad

### Combate
- **Combo de 4 golpes:** encadena hasta cuatro ataques seguidos. El juego guarda la pulsación, así que puedes presionar el siguiente golpe antes de que termine el anterior.
- **Almas:** cada vez que el **4.º golpe del combo conecta** con un enemigo, obtienes un *Alma* (máximo 3).
- **Ataque cargado:** consume las 3 Almas para lanzar un golpe mucho más fuerte.
- **Ira (pasiva):** cada vez que recibes daño, tu ataque aumenta. Cuanto más te castigan, más fuerte golpeas.

### Enemigos
- **Muñeco de entrenamiento:** para practicar combos y medir el daño.
- **Hechicero:** enemigo que ataca a distancia con proyectiles.

Cada enemigo muestra su barra de vida y los números de daño que recibe, y reaparece tras ser derrotado.

### Botín
Los enemigos pueden soltar objetos al morir. Los objetos caen al suelo y se recogen acercándose y pulsando **E**; se guardan en el inventario del jugador.

---

## Controles

| Tecla | Acción |
|-------|--------|
| **A / D** | Moverse |
| **J** | Atacar (combo de 4 golpes) |
| **K** | Ataque cargado (requiere 3 Almas) |
| **E** | Recoger objeto |

---

## Cómo ejecutarlo

1. Instala **Unity 6000.5.0f1** desde Unity Hub.
2. Clona el repositorio y ábrelo como proyecto en Unity Hub.
3. Abre la escena `Assets/Scenes/SampleScene.unity`.
4. Pulsa **Play**.

Si la escena está vacía o quieres regenerarla, usa el menú **Tools > Caballero Maldito**:

1. **Crear animaciones:** importa los sprites y genera las animaciones del personaje.
2. **Montar escena de prueba:** crea el suelo, el jugador, los enemigos, la cámara, la iluminación y el fondo con parallax.
3. – 6. Pasos opcionales para equipar la espada, añadir botín, añadir el hechicero y añadir el fondo sobre una escena ya montada.

Después guarda la escena.

### Componentes principales
- **PlayerController:** movimiento, combo, almas, ataque cargado, ira y estados del jugador.
- **WeaponVisual:** anima la espada y la estela de cada tajo. Las estadísticas y el aspecto del arma vienen de un `WeaponDefinition`, así que se puede cambiar de arma sin crear animaciones nuevas.
- **TrainingDummy / SorcererEnemy:** enemigos; gestionan su vida, muerte y reaparición.
- **LootDropper / Pickup / PlayerInventory:** sistema de botín, desde que el objeto cae hasta que se guarda en el inventario.
- **GameInput:** centraliza la lectura del teclado.

---

## Estado del proyecto

Es un **prototipo**: el HUD, las barras de vida y algunos efectos (humo, proyectiles) son provisionales, y algunas animaciones usan arte temporal mientras se termina el definitivo.
