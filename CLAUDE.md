# CLAUDE.md — Juego VR de isla pirata (Meta Quest, Unity 6)

Guía de contexto para Claude Code. Léela entera antes de tocar código. Si algo de aquí contradice lo que ves en el proyecto, **avisa y pregunta**; no lo "arregles" en silencio.

## 1. Qué es el proyecto

Juego de realidad virtual para **Meta Quest 3** hecho por un equipo de 2 estudiantes: práctica de SGI (MIRI/MEI, UPC). El enunciado está en `LAB_SGI_MEI_Q1.md`: entorno interactivo, navegación (2+ métodos y configuración), selección y manipulación (rotar, mover, escalar), control con interfaz 3D, documento con manual y *disclosure notice* de uso de LLMs, y vídeo. Entrega el **4 de noviembre**.

### Historia (decidida el 27/10/2026, versión definitiva)
> Eras el **grumete** del capitán Calavera. La tripulación encontró un **cofre maldito**; la leyenda decía que solo podía abrirse **bajo la luna**, pero el capitán lo abrió a mediodía. **Todos los que estaban a bordo se convirtieron en esqueletos**, condenados a vigilar el cofre. **Tú te salvaste porque estabas en tierra** (te habían mandado en la barca a por agua). En su último momento humano, el capitán cerró el cofre y te lanzó la llave: *"¡Escóndela! Ábrelo solo bajo la luna y nos liberarás"*. Huiste a la isla y la escondiste de noche en una palmera, sin fijarte en cuál.
>
> Al empezar llevas días escondido en la isla; al amanecer decides volver a liberar a tus compañeros.

Por qué existe cada pieza (mantener esta coherencia al añadir cosas):
| Pieza | Motivo en la historia | Mecánica / requisito |
|---|---|---|
| Barca en el muelle | Con la que huiste del barco | Remar con los brazos (navegación propia) |
| ~~Catalejo~~, ~~Mapa / carta~~ | Quitados el 29/10 (ver 5.8) | Sin escalado (decidido el 30/10: no se hace; la manipulación se cubre con traslación y rotación) |
| **Palmeras / llave** | La escondiste de noche y no recuerdas en cuál | Sacudir palmeras (selección + feedback háptico) → `llave` |
| **Arañas, cocos** | Peligros de la isla | Combate, coger y lanzar |
| **Espada** | Para defenderte de la tripulación maldita | Combate |
| **Esqueletos** (DungeonCharacters) | Tu tripulación, maldita: protegen el cofre | Enemigos del barco; al morir o al romperse la maldición **se desmoronan** |
| **Tótems** (StaffOfPain) | Marcan el poder de la maldición | Luz roja que late; se apagan al romperla (feedback) |
| **Reloj de arena** | La leyenda: solo bajo la luna | **Control**: darle la vuelta = anochece / amanece (cambia el estado de la app) |
| **Cofre maldito** | De día la llave no gira ("solo bajo la luna…") | De noche brilla la cerradura → `cofre` → maldición rota → victoria |

Recorrido: muelle al amanecer (el tótem narrador cuenta la historia) → palmeras (`llave`) → remar al barco fantasma (`barco`) → esqueletos → la llave no gira → reloj de arena → noche → cofre (`cofre`) → los esqueletos se desmoronan y descansan en paz.

Enemigos: **arañas en la isla**, **esqueletos en el barco fantasma**. Descartado: los leñadores de `FreeCharacter` como piratas, la `ZonaIsla` (objetivo `isla`) y las lianas.

- **Idioma:** el equipo habla español. Responde, comenta y escribe los mensajes de log en español. Los identificadores de código en inglés o como ya estén en el proyecto.
- **Nivel del equipo:** estudiantes. Prefieren pasos concretos y explicados ("en la Hierarchy, selecciona X, en el Inspector pon Y") antes que descripciones abstractas.

### 1.1 Estado actual: todo está montado sobre la *Demo Scene* del asset Island

**Escena del juego: `Assets/Scenes/IslaPirata.unity`.** Es una copia de `Assets/Island/Scenes/Demo.unity` (la escena de demostración del asset de la isla) creada con **Isla Pirata > Crear escena propia**. Esa herramienta:
- duplica el TerrainData en `Assets/Scenes/IslaPirata_Terrain.asset`;
- pone la escena la primera en Build Profiles.

**`Demo.unity` ya no se toca.** Si `IslaPirata.unity` aún no existe, créala antes de montar nada. `SampleScene` y `BasicScene` no tienen nada nuestro.

El contenido de la escena (terreno, agua, muelle, vegetación, luces y la barca) sigue viniendo de la demo. Eso condiciona cómo trabajamos:

- Muchos objetos (agua, terreno, barca, muelle) tienen **scripts, shaders, animators, colliders y capas propios de la demo** que no hemos escrito nosotros y que pueden tener configuraciones raras.
- **Objetivo a medio plazo: recrear el juego en una escena propia, fuera de la demo.** La demo es un punto de partida, no el destino. Por tanto:
  1. **Mantén nuestro código desacoplado de la demo.** Nuestros scripts no deben depender de nombres de objetos, jerarquías o scripts de la demo. Usa campos asignables en el Inspector (`Transform`, `Rigidbody`…), no `GameObject.Find` por nombre.
  2. **No modifiques los objetos ni los scripts originales de la demo.** Si hay que cambiar algo, trabaja sobre una copia o un prefab propio en `Assets/Prefabs/`, para poder llevarlo a la escena nueva.
  3. **Todo lo nuestro debe poder moverse como prefab:** XR Origin con locomoción, barca (con sus scripts y `BowRef`), llave, cofre, enemigos, `GameManager`. Prefiere prefabs a objetos sueltos en la escena.
  4. **Anota qué viene de la demo y qué es nuestro** (tabla 4.1). Cuando algo dependa de la demo, escribe un comentario `// DEMO:` en el código y documéntalo aquí.
  5. **Ante cualquier problema, distingue si es nuestro o de la demo** antes de arreglarlo. Por ejemplo, si la barca no se mueve, comprueba primero con *Isla Pirata > Barca > Diagnóstico* si la bloquea un script, joint o collider de la demo.
- **Al migrar** a la escena propia hay que volver a montar: terreno y agua (con los prefabs y materiales del asset, no la escena), muelle, iluminación, NavMesh, capas y matriz de colisiones. La lista de comprobación está en la hoja de ruta (paso 8).

## 2. Stack técnico (no cambiar sin preguntar)

| Elemento | Valor |
|---|---|
| Unity | 6 (6000.5.0f1) |
| Render | URP 17.5 |
| XR | OpenXR 1.17 + Meta OpenXR 2.5 + **XR Interaction Toolkit 3.5.1** (Starter Assets) |
| Input | Input System 1.19 (no usar `UnityEngine.Input` legacy) |
| Navegación | **AI Navigation** (`com.unity.ai.navigation`): hay que instalarlo, lo hace el menú *Isla Pirata > Aranas*. Sin él no se puede hornear el NavMesh |
| Dispositivo | Meta Quest 3, probado con **Quest Link** desde el editor en Windows |
| Rig | `XR Origin (XR Rig)` > `Camera Offset` > `Main Camera`, `Left Controller`, `Right Controller` |

### APIs de Unity 6 que hay que usar
- `rb.linearVelocity` / `rb.angularVelocity` (no `rb.velocity`).
- `rb.linearDamping` / `rb.angularDamping` (no `drag` / `angularDrag`).
- `FindObjectsByType<T>(FindObjectsInactive.Exclude/Include)` y `FindAnyObjectByType<T>()`. En 6000.5 la sobrecarga con `FindObjectsSortMode` está **obsoleta** (da *warning*). Nunca `FindObjectsOfType`.
- Con `using UnityEngine.XR;` y `using UnityEngine.InputSystem;` a la vez, `CommonUsages` es ambiguo: escribe **`UnityEngine.XR.CommonUsages`** completo.
- Namespaces de XRI 3.x:
  - `...Toolkit.Interactables` (`XRGrabInteractable`, `XRSimpleInteractable`);
  - `...Toolkit.Interactors` (`XRSocketInteractor`, `XRRayInteractor`, `InteractorHandedness`);
  - `...Toolkit.Locomotion(.Teleportation/.Turning)`.
- No uses `??` ni `?.` con componentes de Unity (`GetComponent<T>() ?? ...`). Un componente que falta puede ser un "null falso". Usa `c != null ? c : ...`.

## 3. Reglas para trabajar en este repo

1. **Una clase `MonoBehaviour` por fichero y el fichero se llama igual que la clase.** (`BoatHandRowing` → `BoatHandRowing.cs`.) Ya nos pasó pegar una clase dentro del fichero de otra y perder el componente original (ver lecciones, sección 6).
2. **Nunca sobrescribas un script existente entero** si solo hay que cambiar unas líneas. Haz una edición puntual y di qué cambiaste. Si hay que reemplazar un script completo, dilo antes.
3. **No renombres ni muevas** scripts, prefabs o escenas: Unity guarda las referencias por GUID en los `.meta`. No borres ni regeneres `.meta`.
4. **No edites a mano** `.unity`, `.prefab`, `.asset`, `ProjectSettings/` ni `Packages/manifest.json` salvo que se pida.
   - Esos cambios se hacen en el editor y se explican paso a paso al usuario.
   - **Alternativa preferida para montajes repetitivos:** una herramienta de editor (un `EditorWindow` en `Assets/scripts/Editor/`, menú **Isla Pirata**). El usuario pulsa el botón y Unity guarda los cambios correctamente, con Undo. Si toca Project Settings, debe pedir confirmación con un diálogo.
   - Leer la escena (`grep`/parsear el YAML) para diagnosticar **sí** está permitido. Avisa de que solo se ve lo **guardado**.
5. **Claude Code no puede ejecutar el juego ni ver el editor.** Tras escribir código, no digas "ya funciona": di qué debe ver el usuario en la Console y qué debe comprobar en el Inspector. Para comprobar que compila se puede usar el Roslyn de Unity (ver sección 7).
6. Todo comportamiento nuevo lleva **logs de diagnóstico** con prefijo `[SISTEMA]` (ver la lista en la sección 7) y una casilla `debugLogs` en el Inspector.
7. Usa `[SerializeField]`/campos públicos con `[Header]` y `[Tooltip]` para que los parámetros se ajusten en el Inspector sin recompilar.
8. Al terminar una tarea: qué ficheros cambiaste, qué debe configurar el usuario en el editor, y **cómo comprobar que funciona**. Actualiza `GUIA_MONTAJE.md` y este fichero si cambia el montaje.
9. Si falta información (qué objeto tiene el Rigidbody, qué capa usa algo), **pregunta o añade un log que lo averigüe**. No lo supongas.
10. Evita `RequireComponent(typeof(Rigidbody))` en scripts de prueba: añade un segundo Rigidbody si el script se pone en un hijo.
11. Para leer los mandos en scripts de juego usa el helper `XRHandInput` (lee directamente de `InputDevices`). Así funciona aunque el `XR Input Modality Manager` desactive los objetos Controller.

## 4. Estructura del proyecto

```
Assets/
  scripts/                 ← nuestros scripts de juego (ver 4.2)
  scripts/Editor/          ← herramientas del menú "Isla Pirata" (solo editor)
  Prefabs/                 ← nuestros prefabs, creados por las herramientas (Arana, Esqueleto, TotemMaldito, RelojArena, Espada, Coco, PalmeraSacudible; Pirata ya no se usa)
  Audio/Historia/          ← Historia_completa.mp3 (voz del tótem narrador)
  UI/                      ← texturas generadas por *Estilo pirata* (paneles: Placa, Banda, Filete, Madera; menú de la muñeca: Carta_Marco, Carta_Lineas, Pildora, Fila, Medallon, Segmento_*; Boton, Circulo, Icono_*; Pergamino ya no se usa) y Fuentes/ (Pirata One, IM Fell English SC, Libre Baskerville: .ttf + OFL + SDF.asset; Alegreya ya no se usa)
  Resources/EstiloPirata.asset ← estilo común de la interfaz (PirateStyle.Get())
  Scenes/IslaPirata.unity  ← ESCENA DEL JUEGO (copia de la demo) + IslaPirata_Terrain.asset (terreno propio)
  Island/                  ← asset de la isla: Scenes/Demo.unity (original, no tocar), Terrain, Prefabs (Tree_a/b, Dock, Coconut…)
  Boats/                   ← barca OldWoodenRowboat_Complete (remos Paddle / Paddle (1))
  ChestFree/               ← cofre: chest_close, chest_open, coins (modelos separados, sin animación)
  Rust Key/                ← llave rust_key
  Spiders - characters with animations/  ← arañas (Generic; clips idle, walk, attack_1, attack_2, dead)
  DungeonCharacters/       ← esqueletos = la tripulación maldita (Generic, huesos Bip001; clips idle, walk, onehand_attack en FBX separados; sin muerte)
  StaffOfPain/             ← palo con calaveras = tótems de la maldición (material Standard, se convierte a URP; trae una Reflection Probe que se quita)
  FreeCharacter/           ← YA NO SE USA (leñadores); se puede borrar
  Maritime Telescope/      ← YA NO SE USA (el catalejo se quitó el 29/10)
  Danvil/                  ← espada Rusty Sword
  RockFREE/                ← rocas de los nidos de arañas
  Stylized_Pirate_Ship/ (barco fantasma), NekoLegends/, AS_Casual_Island_Game_Sounds_FREE/
  XR/Settings/             ← OpenXR (el aviso "Importer generated inconsistent result" es inofensivo)
  Samples/XR Interaction Toolkit/3.5.1/Starter Assets/   ← rig, locomotion, input actions
GUIA_MONTAJE.md            ← montaje en el editor, paso a paso, de todos los sistemas
```

### 4.1 Objetos clave de la escena (`IslaPirata.unity`, igual que la demo)
- **`XR Origin (XR Rig)`** (capa Player): `Locomotion` (Turn, Move [Dynamic Move Provider], Grab Move, Teleportation, Climb, Gravity, Jump), `CharacterController`, `XR Input Modality Manager`, `Input Action Manager`. Lleva nuestro `Health` + `PlayerDamageFeedback`, que añaden las herramientas.
- **`OldWoodenRowboat_Complete`** (capa Boat): la barca. **Tiene el Rigidbody**: masa 100, gravedad off, congela Pos Y y Rot X/Z. Hijos:
  - `BowRef` (flecha azul = proa) y `anclaBarco` (TeleportationAnchor);
  - los remos `Paddle` / `Paddle (1)`: su origen es la chumacera y sus colliders son trigger.
  - Los remos **no** llevan XR Grab Interactable. Se agarran con nuestro `Oar` (proximidad + grip leídos del casco). `BoatHandRowing.requireOarGrab` hace que solo se reme con un remo agarrado.
- **`anclaMuelle`**: es una instancia de **`Assets/Island/Prefabs/Dock.prefab`**. Es un **prefab del asset que se modificó**: se le añadió un hijo `anclaBarco` con TeleportationAnchor y en la escena se renombró. Al migrar, saca el ancla a un prefab propio.
- `Terrain` (las palmeras originales están **pintadas en el Terrain**, no son GameObjects), `rust_key` (ya tenía Rigidbody, Capsule Collider y XR Grab Interactable), `chest_close` (Box Collider + Rigidbody kinematic).
- Capas: Default, TransparentFX, Ignore Raycast, **Player (3)**, Water (4), UI (5), **Boat (6)** y **`TeleportAnchor`**, que la crea *Isla Pirata > Barca* y no choca con nada. La Raycast Mask del Teleport Interactor tenía Default + Boat + bit 31.
- El agua de la demo **no tiene collider**. Por eso el NavMesh se hornea desde colliders, así no queda NavMesh sobre el mar.
- Ojo: el NavMesh horneado que usa `IslaPirata.unity` está guardado en `Assets/Island/Scenes/Demo/NavMesh-NavMesh.asset` (carpeta de la demo). Al migrar, hay que volver a hornearlo.

### 4.2 Qué es de la demo/asset y qué es nuestro

| Elemento | Origen | ¿Se llevará a la escena propia? |
|---|---|---|
| Terreno, agua, vegetación, muelle, skybox/luces | Demo scene del asset Island | Reconstruir con los prefabs/materiales del asset |
| `OldWoodenRowboat_Complete` (modelo, colliders, remos) | Asset Boats | Sí, como prefab propio (copia) |
| XR Origin y locomoción | XRI Starter Assets (configurado por nosotros) | Sí, prefab |
| `BoatRowing`, `BoatHandRowing`, `Oar` (+ `OverAnchor`, de prueba, desactivado en `anclaBarco`) | Nuestros | Sí |
| `Curse`, `CurseTotem`, `SkeletonCrumble`, `Hourglass`, `StoryTotem` | Nuestros | Sí, prefabs en `Assets/Prefabs/` (Esqueleto, TotemMaldito, RelojArena) |
| `KeyPocket`, `SwordHolster`, `SpiderNest`, `RopeLadder`, `PlayerStart`, `PlayerRescue`, `WristMenu`, `TutorialHints`, `ProceduralSounds`, `GameIntro`, `HealthBar`, `ObjectiveToast`, `PirateStyle` (+ `Assets/UI`, `Assets/Resources/EstiloPirata.asset`) | Nuestros | Sí |
| Fuentes *Pirata One*, *IM Fell English SC* y *Libre Baskerville* (`Assets/UI/Fuentes`) | Google Fonts, licencia SIL OFL 1.1 (`OFL.txt`, `OFL_IMFellEnglishSC.txt`, `OFL_LibreBaskerville.txt` al lado): se pueden usar y redistribuir; citarlas en el documento | Sí |
| `anclaBarco`, `anclaMuelle`, `BowRef`, `Empunadura` (hijo de cada remo) | Nuestros (colocados en la demo; `anclaMuelle` dentro de `Dock.prefab` del asset) | Sí, recolocar en la nueva escena |
| `XRHandInput` (helper estático), `Health`, `PlayerDamageFeedback`, `GameManager`, `ObjectiveZone` | Nuestros | Sí |
| `KeyItem`, `ChestLock` (sobre `rust_key` y `chest_close`/`chest_open`/`coins` de los assets) | Nuestros | Sí, como prefab |
| `EnemyAI` (arañas y piratas), `Coconut`, `CoconutTree`, `ShakeablePalm`, `Sword` | Nuestros | Sí, prefabs en `Assets/Prefabs/` |
| `SwimmingLocomotion` (opcional, sin montar), `DayNightCycle` | Nuestros (`// DEMO:` agua y Directional Light) | Sí, reasignar agua/sol |
| Herramientas `Assets/scripts/Editor/*` | Nuestras (solo editor) | Sí |

**Assets modificados por las herramientas** (solo ajustes de importación): `tarantula.fbx` (o el modelo de araña elegido) y los FBX de animación de DungeonCharacters, con *Loop Time* en idle/walk. El material `PBS_STAFFOFPAIN.mat` se convierte a URP/Lit.

## 5. Sistemas del juego

Estado (revisado el 30/10 leyendo la escena guardada): **todo el código está escrito**. Probado con el casco: barca, remos, teletransporte, palmeras y bolsillo de la llave. **La mayoría ya está montada en `IslaPirata.unity`**: palmeras, llave, cofre, bolsillo, espada, nidos de arañas, esqueletos, tótems, reloj de arena, maldición, `DiaNoche`, `GameManager` (`llave` → `barco` → `cofre`), barco fantasma (`ZonaBarcoPirata`, `anclaBarcoPirata`), escala de cuerda, tótem narrador, menú de la muñeca e `Inicio jugador`. **No aparecen** en la escena guardada: `PlayerRescue` y `SwordHolster` (y los opcionales `SwimmingLocomotion` y `CoconutTree`). El montaje se hace con el menú *Isla Pirata* o con `GUIA_MONTAJE.md`.

### 5.1 Llave, palmeras y cofre
- **Palmeras sacudibles** (`ShakeablePalm` + `XRSimpleInteractable` en la raíz):
  - las coloca *Isla Pirata > Palmeras sacudibles* con el modelo `Tree_a`/`Tree_b`;
  - se agarra el tronco con el grip (como mucho a 1,2 m) y se sacude la mano en horizontal: se balancea, vibra y caen cocos de la copa (`Copa`);
  - los cocos vuelven a crecer.
- **Llave**:
  - si ninguna palmera la tiene en `hiddenKey`, `KeyItem.hideInRandomPalm` la esconde en una al azar al empezar (`ShakeablePalm.HideKey`);
  - lleva `KeyItem` (id `cofre`);
  - va escondida en una palmera (`ShakeablePalm.hiddenKey`). Al empezar sube sola a la copa y se ve entre los cocos;
  - cae tras `dropKeyAfterCoconuts` cocos;
  - al cogerla por primera vez completa el objetivo `llave`.
- **Cofre** (`ChestLock` en un objeto `Cofre`):
  - hijos `chest_close` (cerrado), `chest_open` (abierto, oculto), `coins` (recompensa, oculta) y `Cerradura`, con `XRSocketInteractor` y una esfera trigger. *Llave y cofre > Poner la cerradura en el candado* la mueve al candado del frente (busca en la malla el lado donde lo que sobresale está centrado);
  - al encajar la llave correcta cambia de modelo, muestra las monedas, gasta la llave y completa el objetivo `cofre`;
  - para probarlo sin casco: menú contextual **Abrir ahora**.
- **Bolsillo (`KeyPocket`, *Llave y cofre > 4*):** para llevar la llave mientras remas o trepas (hacen falta las dos manos y el grip, y el fondo de la barca no tiene collider). Se suelta junto a la cadera derecha (bolita verde) y se queda ahí (65 cm bajo la cabeza), kinematic. **No** se usa trigger (el rayo de XRI ignora los triggers): se hace `Physics.IgnoreCollision` con la barca, el CharacterController y la escala. Así se puede **sacar con el rayo desde lejos** sin agacharse. Si cae por debajo de `lostBelowY` (el agua) vuelve sola. Prefijo `[BOLSILLO]`.
- La llave necesita su Mesh Collider en **Convex** (si no, Unity lo ignora con Rigidbody dinámico y atraviesa el suelo): lo hace *Preparar llave*.
- Lo monta *Isla Pirata > Llave y cofre*.

### 5.2 Barca y remo
- **Maniobras con botones** (`BoatHandRowing.buttonManeuvers`, 29/10): **B** = empujón hacia atrás (`backPush`), **A / X** = girar 90° derecha / izquierda (`turnStep`) a `buttonTurnSpeed` (45°/s) con `MoveRotation`; pulsar otra vez mientras gira = 180°. Para cuando no hay sitio para remar.
Arquitectura:
- **La barca es un `Rigidbody` dinámico** (no kinematic), gravedad off, constraints: congelar Position Y y Rotation X/Z, masa ≈ 100. Se mueve con fuerzas (`ForceMode.Acceleration`) hacia la proa (`BowRef.forward` proyectado en horizontal).
- **Embarcar:**
  - `BoatRowing.Board()` hace al `XR Origin` **hijo** de la barca (`SetParent(barca, true)`);
  - desactiva su `CharacterController` y los providers de `disableWhileOnBoard`: todos los `LocomotionProvider` salvo Teleportation y Turn;
  - **si el XR Origin no cuelga de la barca, ningún script de remo hace nada**;
  - `Disembark()` lo restaura todo.
- **Remar (versión 27/10, tras probar con el casco):**
  1. **Coger:** acercar cualquier mano a cualquier empuñadura (radio 0,3 m) y apretar el **grip** → se cogen **los dos remos a la vez**, cada uno con la mano que tiene más cerca (nunca cruzados), y se **embarca automáticamente** (`BoatRowing.Board()`).
     - Antes, sin embarcar, el **Grab Move** de XRI movía al jugador al tirar del grip y "te salías de la barca".
  2. **Sujetar:** los remos **se quedan en las manos** hasta desembarcar (`BoatRowing.onDisembarked` → `ReleaseBoth`).
  3. **Remar:** el **grip = pala en el agua**. Grip apretado y tirar hacia el cuerpo = avanzar (la velocidad de la mano hacia atrás, respecto a donde mira el jugador, en espacio de la barca). Sin grip, se recupera sin frenar. Desde el 27/10 (`allowReverse` desmarcado, por defecto) volver las manos **con** el grip apretado tampoco frena: en las pruebas los jugadores no soltaban el grip y perdían el 60 % de cada remada. Un solo remo = girar.
  - Bolita en la empuñadura: gris = libre, amarilla = mano cerca, verde = cogido, azul = pala en el agua.
  - La posición de las manos sale de los objetos `Left/Right Controller` y el grip de las acciones `XRI Left/Right Interaction/Select Value`. Leerlos solo de `InputDevices` no funcionaba con OpenXR. Lo asigna *Isla Pirata > Barca > 2*.
- **Física de la barca:** el collider del fondo del casco está desactivado en la escena; las paredes empiezan en Y ≈ 3,98. *Isla Pirata > Barca > Diagnóstico* (sección FÍSICA) comprueba el terreno bajo el casco y los colliders que lo tocan, y el botón **4** mueve la barca a agua más profunda si está varada.
- **No usamos joystick** para remar.
- *Isla Pirata > Barca*: **Diagnóstico** (informe `[BARCA]`), **Preparar barca y remos**, **Arreglar teletransporte**.

Scripts:
- `BoatRowing.cs`: embarque/desembarque, amortiguación (`linearDamping`/`angularDamping` se aplican en `Awake`), quilla (`keelDamping`), eventos `onBoarded`/`onDisembarked`. **No empuja la barca.**
- `BoatHandRowing.cs`: el **único** script que empuja la barca. Lee grip y posición de las manos **directamente de `InputDevices`**, da vibración, escribe `[MANO] ...` y avisa `NO ESTAS A BORDO`. Valores que funcionaban: Push Force 30, Reverse 0.6, Turn Force 3, Max Angular Speed 0.6.
- `Oar.cs`: remo agarrable. Una mano solo puede coger un remo. `Oar.HeldByHand(XRNode)` dice qué remo tiene cada mano.
- `BoatDoctor.cs` (diagnóstico en Play con postes rojos y empuje forzado) **se borró el 29/10**: ya no hacía falta. Para diagnosticar la barca queda *Isla Pirata > Barca > Diagnóstico* (en el editor, sin Play).

### 5.3 Teletransporte muelle ↔ barca
- El collider de un `Teleportation Anchor` va en el **mismo objeto o en un hijo** y **no** puede ser trigger: el rayo ignora triggers.
- Tienen que coincidir:
  - las *Interaction Layers* del ancla y del Teleport Interactor;
  - la capa física del collider con la *Raycast Mask* del Teleport Interactor.
- Los colliders de las anclas van en la capa **`TeleportAnchor`**, que **no choca con nada**. Si no, crean una pared invisible en el muelle y la barca roza.
- `anclaBarco`:
  - evento **Teleporting → `BoatRowing.Board`**. En *Select Exited* no: así estaba antes y no es lo correcto;
  - colocada en el asiento del remero, entre las empuñaduras, mirando a proa;
  - su caja sobresale del casco para que el rayo la toque antes que la barca.
- `anclaMuelle`: evento **Teleporting → `BoatRowing.Disembark`**. Va a ras de las tablas: antes estaba unos 0,37 m por debajo.
- Todo esto lo hace *Isla Pirata > Barca > Arreglar teletransporte*. La matriz de colisiones se escribe en `ProjectSettings/DynamicsManager.asset`.

### 5.4 Enemigos y combate
- **`Health`** (comunes):
  - vida, invulnerabilidad breve y regeneración opcional;
  - eventos `onDamaged(float)` y `onDeath`, flash rojo y `destroyOnDeath`;
  - quien hace daño busca `GetComponentInParent<Health>()`, así que va en la raíz del personaje.
- **Jugador:** `Health` en el XR Origin (`flashChildrenIfEmpty` desmarcado). `PlayerDamageFeedback` vibra los dos mandos y muestra un velo rojo (un Quad delante de la cámara).
- **`EnemyAI`** (arañas y piratas):
  - `NavMeshAgent`; estados Patrol → Chase → Attack → Dead;
  - no ataca si el jugador está más de 2,5 m por encima o por debajo (va en la barca);
  - Animator con `Speed` (float) y los triggers `Attack` y `Die`; los parámetros que falten se ignoran;
  - si no hay animación de muerte, se congela y cae de espaldas (`fallOverIfNoDieAnim`);
  - `objectiveOnDeath` suma un objetivo en el GameManager.
- **Nidos (`SpiderNest`):** las arañas **salen de las rocas** (asset `RockFREE`, que trae BoxCollider y LODGroup).
  - Cuando el jugador está a menos de `triggerRadius`, cada `interval` segundos aparece una araña en `hiddenPoint` (dentro de la roca) y camina hasta `exitPoint` (sobre el NavMesh). Entonces se activan su agente y su IA (`EnemyAI.SetHome` = la salida).
  - Hay un máximo de vivas a la vez (`maxAlive`, `nightMaxAlive` de noche) y un total por nido (`totalSpiders`).
  - Los crea *Isla Pirata > Aranas > 4. Nidos en rocas*: convertir rocas seleccionadas o colocar rocas nuevas.
- **Arañas:**
  - `Assets/Prefabs/Arana.prefab` (variante de `tarantula`) con `AranaAnimator.controller`;
  - lo crea *Isla Pirata > Aranas*, que también instala AI Navigation y crea el `NavMesh`.
- **Esqueletos (la tripulación maldita):**
  - `Assets/Prefabs/Esqueleto.prefab` (DungeonCharacters, desempaquetado), con `EsqueletoAnimator.controller` (idle, walk, onehand_attack) y `applyRootMotion` off;
  - ataque con *windup* de 0,4 s (se puede esquivar), vida 50, `Health.destroyOnDeath` **off**;
  - `SkeletonCrumble`: al morir **o al romperse la maldición** añade en tiempo real Rigidbody + collider + CharacterJoint a los huesos `Bip001` (pelvis, columna, cabeza, brazos, piernas), cae como un muñeco de trapo, se hunde y se destruye. `EnemyAI.fallOverIfNoDieAnim` va desactivado;
  - lo crea *Isla Pirata > Esqueletos piratas*, que también los coloca alrededor del cofre y quita los antiguos leñadores (`Pirata.prefab`).
- **Maldición** (*Isla Pirata > Maldición*):
  - `Curse` (objeto `Maldicion`, `Curse.Instance`): `Break()` dispara el evento `Broke`, al que se suscriben los tótems y los esqueletos;
  - `CurseTotem` (`TotemMaldito.prefab` = StaffOfPain + luz roja que late, más fuerte de noche) se apaga al romperse;
  - `ChestLock.requireNight`: **hay que hacer de noche antes de meter la llave** (decidido el 28/10). De día la llave se rechaza (texto *"solo bajo la luna…"*, vibración) y vuelve al bolsillo (`KeyPocket.StoreNow`); la cerradura no acepta nada durante 2 s. Con `openWhenNightFallsWithKeyIn` se vuelve al comportamiento antiguo (la llave se queda y se abre al anochecer). Al acercarse a menos de `approachRadius` (4 m) sale un mensaje según sea de día o de noche; si no hay `hintText`, el cofre crea un cartel TMP encima. `breaksCurse` llama a `Curse.Break()`;
  - `Hourglass` (`RelojArena.prefab`, hecho con primitivas): al darle la vuelta llama a `DayNightCycle.TransitionTo` (9 h ↔ 23 h en 4 s) y la arena se vacía;
  - `DayNightCycle` (objeto `DiaNoche`, `dayLengthMinutes = 0`: el tiempo solo pasa con el reloj o el menú). Menú contextual *Hacer de noche / Hacer de día* para probar sin casco.
- **Cocos:**
  - `Coconut` hace daño según la velocidad del impacto; no hace daño mientras lo tienes en la mano;
  - puede darte en la cabeza al caer;
  - `Coco.prefab` sale del `Coconut` del asset Island;
  - `CoconutTree` deja caer cocos automáticamente en las palmeras pintadas en el Terrain (*Isla Pirata > Cocos*);
  - `ShakeablePalm` los suelta al sacudir.
- **Espada (`Sword`, *Isla Pirata > Espada*):** `Espada.prefab` con el modelo *Rusty Sword* (Danvil), escalado a 0,95 m. La herramienta detecta en la malla la punta (el extremo estrecho) y la guarda (lo más ancho), y crea `Agarre` (attach: la hoja sale hacia delante de la mano), `Hoja` (Box Collider trigger) y `Punta`. XR Grab Interactable *Instantaneous* (no choca con el mundo en la mano), con el rayo viene a la mano. Solo daña si la tienes en la mano y la punta va a más de 2 m/s (daño = velocidad × 8, tope 50). Se coloca junto al ancla del muelle. **Funda (`SwordHolster`, *Espada > 3*):** en la cadera izquierda (la derecha es el bolsillo de la llave, `KeyPocket`; son clases distintas para que el cofre no las confunda). Se envaina soltando la empuñadura (`attachTransform`) a < 0,35 m; cuelga punta abajo; sin triggers, con `IgnoreCollision` con barca, jugador y escala; vuelve sola si cae al agua. Prefijo `[FUNDA]`.

### 5.5 Movimiento avanzado
- **Natación (`SwimmingLocomotion`, en el XR Origin):**
  - se nada a partir de 1,3 m de profundidad (raycast al suelo desde la superficie del agua `// DEMO:`);
  - se bracea con el grip y hay flotación;
  - no actúa a bordo, porque el CC está apagado.
- **Escala de cuerda (`RopeLadder`, *Isla Pirata > Escala de cuerda*):** en el costado del barco pirata. Grip cerca de la escala y tirar hacia abajo (manos leídas del casco con `XRHandInput`, sin Climb Provider, porque a bordo está desactivado). Al agarrarla desde la barca hace `Disembark`; al asomar la cabeza por encima de `top` te pone en `deckPoint`; si sueltas abajo vuelves a la barca (`Board`). **Desde lejos:** la raíz lleva BoxCollider + `XRSimpleInteractable`; apuntar con el rayo de XRI y apretar el grip (`selectEntered`) te pone al pie de la escala mirando al casco (sin gravedad) y desde ahí **se trepa con los mandos** (no sube sola, decidido el 28/10). Si no la agarras en `grabWait` (15 s) o sueltas abajo, vuelves a tu sitio en la barca.
- **Escalada genérica:** sin código, con `Climb Interactable` de XRI.

### 5.5e Inicio del jugador (`PlayerStart`)
- Componente en un objeto vacío (p. ej. `Inicio jugador`): en `Awake` coloca el XR Origin ahí, apoyado en el suelo de debajo y mirando a su Z (gizmo azul en la Scene).
- Sin `PlayerStart` en la escena, `BoatRowing.startOnDock` te pone en el ancla del muelle (la que tiene *Teleporting → Disembark* de esa barca). Con `startOnBoard`, se empieza en la barca y ninguno de los dos hace nada.
- `BoatRowing` sigue activando al empezar el CharacterController y los providers de `disableWhileOnBoard` (se guardaron desactivados en la escena).

### 5.5c Tótem narrador (`StoryTotem`, *Isla Pirata > Totem narrador*)
- Un `TotemMaldito` a 2,5 m del inicio (`PlayerStart` o el ancla del muelle), mirando al jugador: el **capitán Calavera, atrapado en el tótem**, cuenta la historia (8 páginas en `pages`) al acercarte a menos de `talkRadius` (4 m).
- Texto TMP flotando sobre el tótem (se crea solo); cada página dura `secondsPerPage` (7 s) o pasa con X. Si te alejas se calla y luego sigue. `voiceClips` opcional (voz por página) o **`fullStoryClip`** (toda la historia en un audio: `Assets/Audio/Historia/Historia_completa.mp3`; las páginas se sincronizan repartiendo el audio según la longitud del texto; al alejarse se pausa). `nightDuringStory`: mientras habla anochece (`storyHour` 23 h) y al acabar o alejarse vuelve la hora de antes. Prefijo `[TOTEM]`.

- (30/10) **Subtítulos:** si `text` está vacío crea un Canvas (banda oscura `subtitleBand` sobre el tótem, `speakerName` en dorado con la fuente de títulos). Con `fullStoryClip`, la línea que se está diciendo sale en dorado, las dichas normales y las que faltan apagadas (reparto del tramo de la página según la longitud de cada línea, `Render(line)`).
- (30/10) `Tell()` la empieza aunque estés lejos (lo usa `GameIntro`). **Mantener B** (`skipHoldSeconds`, 1 s) la salta (`Skipped`). Al acabar no vuelve a empezar hasta que te alejas (`waitForLeave`). Mientras habla se encienden 3 luces de fuego que parpadean (`fireLights`) y, opcional, `fireEffectPrefab`.

### 5.5f Intro (`GameIntro`, *Isla Pirata > Intro (historia y titulo)*)
- Objeto `Intro`. En `Awake` pone `DayNightCycle.hour = storyHour` (antes del `Start` del ciclo, así empieza ya de noche). Para la música (`music` = `GameManager > Musica`, que pasa a `playOnAwake = false`).
- Un frame después de empezar (tras los `Start` de `BoatRowing`/`PlayerRescue`, que encienden el movimiento) desactiva todos los `LocomotionProvider` del XR Origin **menos el giro**.
- `storyTotem.Tell()` y espera a `Finished`. Durante la intro pone `nightDuringStory = false` en el tótem (la noche la lleva la intro).
- Luego: amanecer a `gameHour` en `dawnSeconds` y título TMP delante del jugador (`gameTitle`, `subtitle`, fundido). Después enciende el movimiento y la música (fundido) y `Running = false`.
- **Niebla** (`storyFog`): `RenderSettings.fog` Exp2 oscura durante la historia; se disipa en el amanecer y vuelve a los valores de la escena. *Isla Pirata > Intro* activa la niebla de la escena con densidad 0.0001: con *Graphics > Fog Modes = Automatic*, URP quita del build las variantes con niebla si la escena no la tiene.
- **Cartel del título** (30/10, si `titleText` está vacío): Canvas de madera con marco dorado (`PirateStyle.darkPanel`), filetes dorados con rombo (Images sin sprite) arriba y abajo, título con la fuente pirata en `titleColor` con contorno y sombra (*underlay* del material de TMP, instancia propia) y subtítulo en cursiva. Fijo en el mundo a `titleDistance` (3,5 m) y `titleHeight`. Entrada con crecimiento (*ease out back*) y letras que se juntan (`characterSpacing`), brillo que late, vaivén suave; gong (`ProceduralSounds.Gong`, o `titleClip`) y vibración al aparecer. Con `titleText` asignado se usa el modo sencillo (solo texto). Con **`titleImage`** (Sprite) el cartel es esa imagen (proporciones de la imagen, `titleImageWidth` m de ancho, sin adornos) y el texto solo se escribe encima si `textOverImage`. **`titleTextImage`** (PNG transparente del mismo tamaño que el panel) se pone encima, estirado a todo el cartel, y aparece animado con `Image.Type.Filled` (`textReveal`: **Pisoton** por defecto, como el efecto de Canva: cae desde `stompScale` en `stompSeconds` hacia `stompCenter`, golpe `ProceduralSounds.Thud`, vibración fuerte, aplastamiento con rebote y temblor del cartel `stompShake`; o izquierda a derecha, arriba abajo, circular o fundido; `textRevealDelay`, `textRevealSeconds`; `revealGlow` = destello dorado con el sprite `circle` en el borde del barrido). El título dura lo necesario para que el texto salga entero. Imágenes del equipo: `Assets/UI/Titulo/tablero-fondo-titulo1.png` (tablero con "La maldición del capitán") y `texto-titulo1.png` ("Calavera" en rojo), 797x500; *Isla Pirata > Intro* las importa como Sprite (alfa, mipmaps) y las asigna si los campos están vacíos, con `revealGlowColor` naranja.
- `storyOnlyFirstTime`: tras `GameManager.Restart` solo sale el título (bool estático, se reinicia al pulsar Play).
- `GameIntro.Running` lo consulta `TutorialHints`. Prefijo `[INTRO]`.
- (30/10) **Arranque:** con Quest Link la imagen tarda 1-3 s en salir tras pulsar Play y la voz del tótem se oía con la pantalla en negro. Ahora `waitForHeadset`: la intro espera a que el `XRDisplaySubsystem` esté en marcha y la cabeza localizada (máximo `headsetTimeout`; sin subsistema XR, como en el simulador, empieza al momento) y `startDelay` más; con `muteUntilReady`, `AudioListener.pause` hasta entonces. `StoryTotem` no empieza solo por cercanía mientras `GameIntro.Running` (antes arrancaba en el primer fotograma porque el jugador aparece a 2,5 m).

### 5.5g Barras de vida (`HealthBar`)
- Las añade `Health.Awake` si `showHealthBar` (por defecto sí): no hay que montarlas.
- Canvas World Space con `Image` sin sprite (fondo, rastro blanco que baja despacio, vida verde→roja con anclas). Sale al recibir daño (`showSeconds` 3 s) y se desvanece.
- **Enemigo:** suelta en la escena (no hereda escala), sobre la parte más alta del modelo (bounds en `Start`), mirando al jugador. Se oculta si está muerto o con la `EnemyAI` apagada (desmoronándose, o saliendo del nido). Se destruye con el enemigo.
- **Jugador** (el objeto tiene `XROrigin`): hija de la cámara en `hudOffset` (abajo en la vista). Se queda visible si queda menos de `alwaysShowBelow` (30 %).

### 5.5d Pistas de controles (`TutorialHints`)
- Las crea solo el `GameManager` al empezar (`tutorialHints`). Cada pista sale **una vez**, en contexto (andar, menú Y, palmera, llave, bolsillo, espada, arañas, barca, remar, bajar, escala, cofre, reloj, daño); espera a que el tótem termine (`StoryTotem.IsTalking`) y a que acabe la intro (`GameIntro.Running`). Busca los objetos por tipo. Prefijo `[PISTAS]`.
- (30/10) En un panel del estilo (madera o banda, `panelLook`) con el **dibujo de los botones** (`PirateStyle.MakeButtonIcon`: GRIP, B, X, Y, Stick izq./der.). Cada pista tiene `Side`: `View` = por debajo de la vista con seguimiento suave; `Left`/`Right` = **encima de ese mando** (posición del casco con `XRHandInput.TryGetWorldPosition`), más pequeña (`handScale`), y vibra ese mando.

### 5.5h Estilo de la interfaz (`PirateStyle`, *Isla Pirata > Estilo pirata*)
- ScriptableObject en `Assets/Resources/EstiloPirata.asset` (`PirateStyle.Get()`; si no existe, valores por defecto y un warning `[ESTILO]`). Fuentes por papel (`FontRole`): `titleFont` = **Pirata One** (títulos), `headingFont` = **IM Fell English SC** (nombre del que habla, pestañas del menú, letras de los botones del mando), `bodyFont` = **Libre Baskerville** (texto, la más legible en el casco; con su cursiva en la tabla de pesos; el 30/10 sustituyó a Alegreya). Sprites 9-slice (`plaque`, `band`, `rule`, `darkPanel` = madera lisa del cartel de la intro, `button`, `circle`; `parchment` ya no se usa; los del menú de la muñeca: `chartFrame`, `chartLines`, `pill`, `row`, `medallion`, `segment[4]`/`segmentFill[4]`, colores `chartText` y `chartNavy`), iconos del menú y colores (`ink`, `paperText`, `gold`, `panelSoft`, `panelDone`…).
- (30/10) **Aspecto de los paneles: `panelLook`** = `Madera` (placa de madera oscura, marco dorado y rombos en las esquinas) o `Banda` (franja oscura difuminada a los lados con filetes dorados; `PanelSidePadding` aparta el texto de los bordes). Se elige en el asset o con *Isla Pirata > Estilo pirata - paneles de madera y oro / en banda difuminada*; se nota al darle a Play. Afecta a avisos de objetivo, pistas, subtítulos del tótem, `Panel Objetivos` y `Pantalla Fin`. **Aún sin decidir** entre Madera y Banda (por defecto Madera). El menú de la muñeca no depende de `panelLook`: es una carta náutica (5.5b). Se quitó el pergamino porque quedaba cutre. Muestras en HTML: https://claude.ai/artifact/BJvN5ifdy85JwYH3v31T81
- La herramienta **genera por código** las texturas (ruido Perlin, madera con veta, iconos con 4x4 muestras de antialiasing) en `Assets/UI/*.png` y las fuentes TMP (`PirataOne SDF`, `IMFellEnglishSC SDF`, `LibreBaskerville SDF`, `LibreBaskerville Italic SDF`) con **atlas estático** (no dependen del .ttf en el build). Respaldo para caracteres que falten: títulos y encabezados → Libre Baskerville → LiberationSans de TMP. **Libre Baskerville pasa a ser la fuente por defecto de TMP** (`TMP Settings`, con diálogo) y `GameManager` (si `styleUI`) cambia al empezar a Libre Baskerville todo texto de la escena que no use una fuente del estilo (`PirateStyle.ReplaceDefaultFonts`): no debe salir nunca la fuente por defecto.
- Constructores en tiempo real: `MakePanel` (panel con el aspecto elegido + hijo `Contenido` para los textos), `MakeCanvas`, `MakeText`, `MakeRule` (filete dorado), `MakeButtonIcon` (círculo dorado, letra oscura), `SkinPanel`/`SkinImage` (viste un panel ya hecho), `ApplyFont(t, FontRole)`. Los usan `StoryTotem`, `TutorialHints`, `ObjectiveToast`, `GameIntro`, `ChestLock` y `GameManager` (que viste el `Panel Objetivos` y la `Pantalla Fin` al empezar si `styleUI`).

### 5.5i Objetivos (`GameManager` + `ObjectiveToast`)
- `GameManager.CurrentObjective` = el primero sin hacer. El panel de la muñeca muestra "Objetivo n de N", el actual grande y los demás pequeños (hechos tachados en verde).
- `ObjectiveToast` (lo crea el `GameManager`, `objectiveToasts`): panel grande del estilo (título en Pirata One y filete dorado) delante de la vista, 4,5 s con fundido. Al acabar la intro: "Tu objetivo: ..."; al completar uno: "¡Objetivo cumplido!" + el siguiente (el último no, sale la pantalla de victoria). Prefijo `[OBJETIVO]`.
- `GameManager.onlyWarningsInBuild`: en el build `Debug.unityLogger.filterLogType = Warning` (en el editor salen todos los logs).

### 5.5j Limpieza (*Isla Pirata > Limpieza del proyecto*, `CleanupTool`)
- Assets sin usar = no están en `AssetDatabase.GetDependencies` de: escenas del build, `Resources`, render pipeline de Graphics/Quality y las rutas `"Assets/..."` que aparecen en `Assets/scripts` (para que las herramientas puedan rehacer los prefabs). Protegidas: `scripts`, `Prefabs`, `Resources`, `UI`, `Settings`, `XR`, `XRI`, `TextMesh Pro`, `CompositionLayers`, `Samples`, `Audio`, `Scenes/IslaPirata*`, `Island/Scenes` y cualquier `/Editor/`; sueltos no se borran `.cs`, `.dll`, shaders. Se borra con `AssetDatabase.MoveAssetsToTrash` (papelera) tras un diálogo. Los ficheros sueltos en la raíz de `Assets` no se analizan.
- También: dejar solo la escena del juego en Build Profiles, apagar/encender todas las casillas `debugLogs` (escena + `Assets/Prefabs`), traza de pila de los logs (Player Settings) y limitar a 2048 px / ASTC 6x6 en Android las texturas grandes que usa la escena. Prefijo `[LIMPIEZA]`.

### 5.5a Rescate del jugador (`PlayerRescue`, *Isla Pirata > Rescate del jugador*)
- Caer al **mar** (origen por debajo de `waterLevel - underWater` durante 1,5 s) → vuelves al asiento de la barca (`anclaBarco`) y embarcas. No hay natación en la historia.
- Caer a la **bodega** del barco pirata (dentro de sus bounds y 1,2 m por debajo de la cubierta) → vuelves a `deckPoint`.
- Al empezar activa el CharacterController y los providers de `BoatRowing.disableWhileOnBoard` si se guardaron desactivados (pasó el 28/10: no se podía andar hasta embarcar y desembarcar).

### 5.5b Menú de la muñeca (`WristMenu`, *Isla Pirata > Menu muneca*)
- Canvas World Space hijo del `Left Controller`; **Y** abre/cierra; se pulsa con el rayo derecho.
- Movimiento: `ControllerInputActionManager.smoothMotionEnabled` **solo del mando izquierdo** (el derecho sigue teletransportando: hace falta para la barca). Giro: `smoothTurnEnabled` de los dos. También grados del `SnapTurnProvider`, `moveSpeed` del Move, viñeta (`TunnelingVignetteController`, se activa/desactiva el objeto), hora (`DayNightCycle.ToggleDayNight`, se puede desactivar con `allowTimeChange`), `AudioListener.volume` y reiniciar (`GameManager.Restart`, doble pulsación).
- Opciones guardadas en `PlayerPrefs` (`SGI.Menu2.*`; el "2" hace que se ignoren las de la versión anterior).
- (30/10) **Pestañas** `Movimiento` (movimiento, giro, salto, velocidad), `Confort` (viñeta, volumen) y `Partida` (hora, reiniciar): `tabButtons`/`tabPages`, `ShowTab(i)`. Clic generado (`ProceduralSounds.Click`) y vibración al pulsar y al pasar el rayo (`EventTrigger` PointerEnter, `hoverHaptics`). Hay que volver a pulsar *Menu muneca > Crear / actualizar* cada vez que cambia el diseño del menú: borra el menú anterior y crea uno nuevo (el `Panel Objetivos` no se toca: cuelga de la mano, no del menú).
- (30/10) **Aspecto: carta náutica** (elegido entre 6 diseños; antes madera con botones de papel), como el diseño E de la plantilla (https://claude.ai/artifact/BJvN5ifdy85JwYH3v31T81). *Menu muneca > Crear / actualizar* lo construye así (`WristMenu.chartLayout = true`; si faltan las texturas de *Estilo pirata* da error y no toca el menú): fondo `chartFrame` + hijo `Carta (lineas)`, título Pirata One, cerrar en un medallón, pestañas en píldoras con icono (`pill`), y cada opción es una fila-botón (`row`: invisible, raya y brillo dorados al pasar el rayo) con `Medallon` + `Icono`, `Nombre` (IM Fell) y un selector `Valores` de piezas unidas (`PirateStyle.segment`/`segmentFill`, `Segment(i, n, lleno)`). `WristMenu.SetValues` rellena de oro el valor elegido (letra azul noche) y oculta las piezas que sobran (p. ej. "Cambiando..."); `Option` cae al texto con `<mark>` (`Choices`) en un menú antiguo, que `ApplyStyle` viste a medias. Volumen: piezas `–`, valor y `+` (los dos extremos son los botones).
- `travelButton` es el botón del antiguo *Viaje con la carta*: se oculta al empezar. El menú nuevo ya no lo tiene (campo vacío, no pasa nada).
- `GameManager.objectivesText` apunta al `Texto` del `Panel Objetivos` de la muñeca (desde que se quitó la carta).

### 5.6 Mundo y objetivo
- **`GameManager`** (uno por escena, `GameManager.Instance`):
  - objetivos por id (`llave`, `barco`, `cofre`, opcionalmente `aranas`/`piratas` con `required` > 1), que se completan con `CompleteObjective(id)`;
  - victoria al completarlos todos y derrota si muere el jugador;
  - reiniciar con A/X: **la escena tiene que estar en Build Profiles > Scene List** (lo hace *Crear escena propia*);
  - UI opcional con TMP (usar `[x]`/`[ ]`, no ✔: la fuente por defecto no lo tiene).
- **Diseño actual:** **el cofre está en el barco pirata** (`StylShip_Unity.prefab`, con Mesh Colliders en todas sus piezas). Objetivos: `llave` → `barco` → `cofre`.
  - `ZonaBarcoPirata` (`ObjectiveZone` con id `barco`, 6 m alrededor del barco) se completa al llegar remando.
  - `anclaBarcoPirata` (en la cubierta, Teleporting → Disembark) sirve para subir al barco; para volver, se usa el ancla de la barca de remos.
  - Lo monta *Isla Pirata > Juego > Preparar barco fantasma*. La `ZonaIsla` (objetivo `isla`) se eliminó.
- **`ObjectiveZone`:** completa un objetivo cuando `watch` (la barca), con el jugador a bordo, entra en su Box Collider. Comprueba bounds cada 0,2 s, sin depender de triggers.
- **`DayNightCycle`:** gira la Directional Light (`// DEMO:`), cambia intensidad, color, ambiente y la exposición de una **copia** del skybox. Eventos `onNightStarted`/`onDayStarted`.

### 5.7 Sonidos (`ProceduralSounds`, clase estática)
- El asset `AS_Casual_Island_Game_Sounds_FREE` solo tiene 17 sonidos con nombres genéricos. Los que faltan se **generan por código** y se usan **solo si el campo de sonido del script está vacío**: chapoteo del remo (`BoatHandRowing.splashSounds`), siseo de araña / huesos de esqueleto (`EnemyAI`: al verte, atacar, recibir daño y morir; según la altura del collider), golpe del coco, zumbido y tajo de la espada, crujido + campanas al abrir el cofre y *clonc* si es de día, arena del reloj, campanas de objetivo, victoria y derrota (`GameManager`), siseo al salir del nido, *cling* de la llave (`ShakeablePalm`) y olas de fondo (`GameManager.ambientWaves`).
- `ProceduralSounds.Play(clip, pos)` crea un AudioSource temporal (3D a medias).

### 5.8 Quitado el 29/10: carta y catalejo
- Se borraron `TreasureMap`, `MapMarker`, `GuidedTravel`, `Telescope`, `MapSetupTool`, `TelescopeSetupTool`, `Carta.prefab` y `MapaIsla.png`. El objetivo `otear` ya no existe (`GameSetupTool` aún lo busca al ordenar objetivos; es inofensivo). La carta se quitó porque **estéticamente no quedaba bien** (pergamino plano con una foto cenital).
- El escalado lo cubría la carta; ahora ningún script escala (no hay `allowTwoHandedScaling` en ningún sitio). **Decidido el 30/10: no se sustituye**; la manipulación se cubre con traslación (coger, mover y lanzar la llave, la espada y los cocos) y rotación. Si el profesor lo pidiera, la vía rápida es activar el escalado con dos manos del transformer de un `XRGrabInteractable` (se propuso un barco en una botella).
- Restos en la escena guardada (a borrar a mano en la Hierarchy): `Lugares (carta)` con sus hijos `Lugar Muelle`, `Lugar Palmeral` y `Lugar Barco fantasma` (con componentes *Missing Script*, eran `MapMarker`) y `Boton Viaje carta` dentro de `Menu Muneca`.

### 5.9 Quitado el 29/10: `BoatDoctor`
- Se borró `BoatDoctor.cs`. En la escena guardada queda el objeto `BoatDoctor` (hijo de la barca) con un *Missing Script*: hay que borrarlo a mano.

## 6. Lecciones aprendidas (errores que ya pasaron)

| Síntoma | Causa real | Qué hacer |
|---|---|---|
| El remo no hace nada y no salen logs | El XR Origin **no es hijo de la barca** (`OnBoard` false) | Embarcar con el ancla (Teleporting → Board) o `startOnBoard`; asignar `Xr Origin` en los scripts |
| Se cae de la barca al pulsar Play | `Board()` corría un frame tarde, o el Move/Gravity seguían activos | `startOnBoard` embarca en `Awake` |
| El jugador se mueve dentro de la barca en vez de moverla | Move provider o `CharacterController` activos a bordo | Desactivarlos en `disableWhileOnBoard` |
| La barca no se mueve aunque el script funcione | Terreno/muelle bajo el casco, joint anclado al mundo, Animator/script del asset, malla no hija del Rigidbody, o simplemente va con el jugador dentro y no hay referencia visual | *Isla Pirata > Barca > Diagnóstico* (antes también `BoatDoctor`, ya borrado) |
| Doble fuerza o comportamientos raros | Dos `Rigidbody` o dos `BoatHandRowing` | Un solo Rigidbody en la barca; un solo `BoatHandRowing` (el Diagnóstico lo avisa) |
| Cambios de damping "no hacen nada" | `BoatRowing` reasigna `linearDamping` en `Awake` | Cambiarlo en `BoatRowing`, no en el Rigidbody |
| Teleport anchor no responde | Collider no es hijo/mismo objeto, capas distintas, collider trigger | Ver 5.3 / *Isla Pirata > Barca > Diagnóstico* |
| Las anclas no embarcan; `BoatRowing` sin `Board()` | En `3f20571` se pegó `BoatHandRowing` dentro de `BoatRowing.cs` y se perdió `BoatRowing` | Restaurado: cada clase en su fichero; los valores del remo hubo que volver a ponerlos |
| Se rema con el grip en el aire, sin coger los remos | Los remos del asset no tenían nada agarrable (CLAUDE.md decía que sí) | `Oar` en cada remo + `requireOarGrab` |
| Pared invisible en el muelle / la barca roza al atracar | Colliders sólidos de las anclas en la capa Default | Capa `TeleportAnchor` sin colisiones |
| Al llegar al muelle los pies quedan dentro de las tablas | `anclaMuelle` estaba en el pivote del Dock, por debajo de la superficie | Subir el ancla a ras de suelo (lo hace la herramienta) |
| Enemigos que no se mueven: `NO esta sobre el NavMesh` | NavMesh sin hornear o AI Navigation sin instalar | *Isla Pirata > Aranas*, paso 1, y **Bake** |
| Los piratas se deslizan o se van solos | El asset trae `applyRootMotion` y un componente `Animation` legacy | El prefab `Pirata` los quita |
| Se rema (`[MANO] remada` > 0), la barca avanza según la física, pero la barca que ves no se mueve y un remo no sigue a la mano | El prefab del asset Boats marca barca y remos como **Static** (incluido Batching Static): la malla se queda congelada | *Isla Pirata > Barca > 2* quita el Static; el Diagnóstico lo avisa |
| La palmera no se balancea al sacudirla | `Tree_a`/`Tree_b` del asset Island también vienen **Static** | *Palmeras sacudibles > 3* |
| `CommonUsages is an ambiguous reference` | Dos namespaces con `CommonUsages` | `UnityEngine.XR.CommonUsages` |
| Hay controllers en gris en Play, o `Current Tracking Origin: Unknown` | Modo manos/sin sesión XR | Quest Link como runtime OpenXR activo; revisar `XR Input Modality Manager` |

## 7. Cómo depurar

**Con el casco**
1. Consola de Unity: filtra por el prefijo del sistema:

   | Sistema | Prefijos |
   |---|---|
   | Barca y remo | `[BARCO]`, `[BARCA]`, `[MANO]`, `[REMO]` |
   | Llave, cofre y palmeras | `[LLAVE]`, `[COFRE]`, `[PALMERA]`, `[BOLSILLO]` |
   | Combate | `[VIDA]`, `[JUGADOR]`, `[ENEMIGO]`, `[COCO]`, `[ESPADA]`, `[FUNDA]` |
   | Juego y objetivos | `[JUEGO]`, `[ZONA]`, `[MALDICION]`, `[TOTEM]`, `[PISTAS]`, `[INTRO]`, `[OBJETIVO]`, `[ESTILO]` |
   | Movimiento y mundo | `[NADAR]`, `[ESCALA]`, `[DIANOCHE]`, `[MENU]` |
   | Herramientas del menú | `[ARANAS]`, `[PIRATAS]`, `[COCOS]`, `[PALMERAS]`, `[HERRAMIENTA]`, `[LIMPIEZA]` |

2. **Window > Analysis > Input Debugger**: ver `grip` en vivo del mando.
3. Inspector en modo **Debug** (⋮ > Debug) para ver variables privadas.
4. Vibración del mando = confirmación de que se lee el grip (también al acercar la mano a una empuñadura de remo).
5. Si se prueba un build en el casco (no Link), los logs salen por `adb logcat -s Unity` o Meta Quest Developer Hub.

**Sin abrir Unity (para Claude Code)**
- **Comprobar que compila:**
  - compilador: `"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/DotNetSdk/dotnet.exe" .../DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll`;
  - referencias: **todas** las `Editor/Data/Managed/UnityEngine/*.dll` (incluye los módulos `UnityEditor.*`; no añadas además `Managed/UnityEditor.dll`, que duplica tipos), `Library/ScriptAssemblies/` (InputSystem, XR.Interaction.Toolkit, XR.CoreUtils, TextMeshPro, UnityEngine.UI, AI.Navigation y **XR.Interaction.Toolkit.Samples.StarterAssets**, que usa `WristMenu`; para las herramientas, también los `Unity.*Editor*.dll`, XR.OpenXR y XR.Management) y `NetStandard/ref/2.1.0/netstandard.dll`;
  - opciones: `-langversion:9`.
- **Estado de la escena:** se puede parsear el YAML de `IslaPirata.unity` (lectura). Para encontrar un *Missing Script*, compara los `guid` de `m_Script` con los `.cs.meta` del proyecto. La barca y el XR Origin son instancias de prefab, así que sus cambios están en `m_Modification` y `m_AddedComponents`.

## 8. Hoja de ruta

Lista de tareas concreta para la siguiente sesión, ideas de mejora y lo que falta en código: **`PROXIMOS_PASOS.md`**. Léelo al empezar y actualízalo al terminar.

| # | Paso | Estado |
|---|---|---|
| 1 | **Barca**: XR Origin hijo + remo cogiendo los remos | Montado y probado con el casco (27/10) |
| 2 | Llave en palmera sacudible + cofre | Montado; palmeras y bolsillo probados; falta probar el cofre |
| 3 | Teletransporte estable muelle ↔ barca | Montado y probado |
| 4 | `Health` + cocos + arañas (nidos) | Montado; falta probar |
| 5 | Espada (+ funda) | Espada montada; funda (`SwordHolster`) sin montar; falta probar |
| 6 | Maldición: reloj de arena, cofre de noche, tótems, esqueletos que se desmoronan | Montado; falta probar |
| 6b | Escalado (sustituto de la carta) | Descartado el 30/10: basta con traslación y rotación |
| 6c | Menú de la muñeca (configuración de la navegación y control) | Montado; falta probar |
| 7 | Día/noche y `GameManager` (objetivo y final) | Montado; falta probar |
| 8a | Escena propia copiando la demo (*Crear escena propia*) | Hecho (`IslaPirata.unity`) |
| 8b | **Reconstruir la escena desde cero (fuera de la demo)** | Pendiente, al final |
| 9 | Optimización para Quest y confort VR | Pendiente |

**Paso 8b, reconstruir desde cero** (la 8a ya hace la escena nuestra, pero su contenido sigue siendo el de la demo):
- Crear `Scenes/Main.unity` vacía con URP; reutilizar solo prefabs/materiales/texturas del asset de la isla.
- Convertir en prefabs: XR Origin, barca (con `Oar`, `Empunadura`, `BowRef`, `anclaBarco`), llave, cofre, palmeras, enemigos, `GameManager`.
- Sacar `anclaMuelle` de `Dock.prefab` a un prefab propio y dejar el `Dock.prefab` del asset como estaba.
- Reconstruir terreno/agua/muelle, iluminación, NavMesh, capas (`TeleportAnchor`…) y la matriz de colisiones.
- Volver a asignar los campos del Inspector (`Xr Origin`, `BowRef`, anclas, `Water Surface`, `Sun`) y repasar con *Isla Pirata > Barca > Diagnóstico*.
- Comprobar que nada nuestro referencia objetos de la demo (buscar `Find(`, nombres fijos y comentarios `// DEMO:`).

**Paso 9, optimización y confort:** batching, luces, sombras, tamaño de texturas; viñeta al girar/moverse y snap turn por defecto.

### Pendientes conocidos
- **Restos en la escena:** borrar `BoatDoctor` (hijo de la barca) y `Lugares (carta)` (ver 5.8 y 5.9). `Boton Viaje carta` desaparece al volver a crear el menú de la muñeca.
- **Interfaz (30/10):** elegir `panelLook` (Madera o Banda) y probar con el casco el menú de carta náutica, las fuentes (Libre Baskerville) y el arranque que espera al casco (`GameIntro.waitForHeadset`).
- Montar `PlayerRescue` y `SwordHolster`, que no están en la escena guardada.
- Ramas de git: se trabaja en `barco-fantasma`; `master` se actualiza con `git merge --ff-only barco-fantasma` (el 30/10 estaban iguales).

## 9. Plantilla para pedir tareas a Claude Code

```
Contexto: [qué sistema, qué objeto de la escena]
Objetivo: [qué debe pasar en el juego]
Restricciones: [ficheros que no se pueden tocar, API a usar]
Cómo lo comprobaré: [qué debería ver en la Console / en el casco]
```

Claude Code debe responder con: ficheros modificados, pasos a hacer en el editor (o qué botón del menú *Isla Pirata* pulsar), y la comprobación esperada.
