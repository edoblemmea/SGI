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
| **Catalejo** | El barco fantasma se alejó en la niebla: hay que localizarlo | Coger, mover, rotar; alargar con dos manos = zoom (manipulación) → objetivo `otear` |
| **Mapa / carta** | El diario del capitán con tus notas de dónde escondiste la llave | Objetivos y pistas; se agranda con dos manos (**escalado**); viaje rápido a lugares descubiertos (pendiente) |
| **Palmeras / llave** | La escondiste de noche y no recuerdas en cuál | Sacudir palmeras (selección + feedback háptico) → `llave` |
| **Arañas, cocos** | Peligros de la isla | Combate, coger y lanzar |
| **Espada** | Para defenderte de la tripulación maldita | Combate |
| **Esqueletos** (DungeonCharacters) | Tu tripulación, maldita: protegen el cofre | Enemigos del barco; al morir o al romperse la maldición **se desmoronan** |
| **Tótems** (StaffOfPain) | Marcan el poder de la maldición | Luz roja que late; se apagan al romperla (feedback) |
| **Reloj de arena** | La leyenda: solo bajo la luna | **Control**: darle la vuelta = anochece / amanece (cambia el estado de la app) |
| **Cofre maldito** | De día la llave no gira ("solo bajo la luna…") | De noche brilla la cerradura → `cofre` → maldición rota → victoria |

Recorrido: muelle al amanecer → mirador con el catalejo (`otear`) → palmeras (`llave`) → remar al barco fantasma (`barco`) → esqueletos → la llave no gira → reloj de arena → noche → cofre (`cofre`) → los esqueletos se desmoronan y descansan en paz.

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
  5. **Ante cualquier problema, distingue si es nuestro o de la demo** antes de arreglarlo. Por ejemplo, si la barca no se mueve, comprueba primero con `BoatDoctor` si la bloquea un script, joint o collider de la demo.
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
- `FindObjectsByType<T>(FindObjectsInactive.Exclude/Include)` y `FindAnyObjectByType<T>()`. En 6000.5 la sobrecarga con `FindObjectsSortMode` está **obsoleta** (da *warning*; aún queda en `BoatDoctor.cs`). Nunca `FindObjectsOfType`.
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
  Prefabs/                 ← nuestros prefabs, creados por las herramientas (Arana, Pirata, Coco, PalmeraSacudible…)
  Scenes/IslaPirata.unity  ← ESCENA DEL JUEGO (copia de la demo) + IslaPirata_Terrain.asset (terreno propio)
  Island/                  ← asset de la isla: Scenes/Demo.unity (original, no tocar), Terrain, Prefabs (Tree_a/b, Dock, Coconut…)
  Boats/                   ← barca OldWoodenRowboat_Complete (remos Paddle / Paddle (1))
  ChestFree/               ← cofre: chest_close, chest_open, coins (modelos separados, sin animación)
  Rust Key/                ← llave rust_key
  Spiders - characters with animations/  ← arañas (Generic; clips idle, walk, attack_1, attack_2, dead)
  DungeonCharacters/       ← esqueletos = la tripulación maldita (Generic, huesos Bip001; clips idle, walk, onehand_attack en FBX separados; sin muerte)
  StaffOfPain/             ← palo con calaveras = tótems de la maldición (material Standard, se convierte a URP; trae una Reflection Probe que se quita)
  FreeCharacter/           ← YA NO SE USA (leñadores); se puede borrar
  Maritime Telescope/      ← catalejo (LOD0-2 con first/second/third_segment)
  Stylized_Pirate_Ship/, NekoLegends/, Maritime Telescope/, AS_Casual_Island_Game_Sounds_FREE/
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

### 4.2 Qué es de la demo/asset y qué es nuestro

| Elemento | Origen | ¿Se llevará a la escena propia? |
|---|---|---|
| Terreno, agua, vegetación, muelle, skybox/luces | Demo scene del asset Island | Reconstruir con los prefabs/materiales del asset |
| `OldWoodenRowboat_Complete` (modelo, colliders, remos) | Asset Boats | Sí, como prefab propio (copia) |
| XR Origin y locomoción | XRI Starter Assets (configurado por nosotros) | Sí, prefab |
| `BoatRowing`, `BoatHandRowing`, `Oar`, `BoatDoctor` (+ `OverAnchor`, de prueba, desactivado en `anclaBarco`) | Nuestros | Sí |
| `Telescope`, `Curse`, `CurseTotem`, `SkeletonCrumble`, `Hourglass` | Nuestros | Sí, prefabs en `Assets/Prefabs/` (Telescopio, Esqueleto, TotemMaldito, RelojArena) |
| `anclaBarco`, `anclaMuelle`, `BowRef`, `Empunadura` (hijo de cada remo) | Nuestros (colocados en la demo; `anclaMuelle` dentro de `Dock.prefab` del asset) | Sí, recolocar en la nueva escena |
| `XRHandInput` (helper estático), `Health`, `PlayerDamageFeedback`, `GameManager`, `ObjectiveZone` | Nuestros | Sí |
| `KeyItem`, `ChestLock` (sobre `rust_key` y `chest_close`/`chest_open`/`coins` de los assets) | Nuestros | Sí, como prefab |
| `EnemyAI` (arañas y piratas), `Coconut`, `CoconutTree`, `ShakeablePalm`, `Sword` | Nuestros | Sí, prefabs en `Assets/Prefabs/` |
| `SwimmingLocomotion` (opcional, sin montar), `DayNightCycle` | Nuestros (`// DEMO:` agua y Directional Light) | Sí, reasignar agua/sol |
| Herramientas `Assets/scripts/Editor/*` | Nuestras (solo editor) | Sí |

**Assets modificados por las herramientas** (solo ajustes de importación): `tarantula.fbx` (o el modelo de araña elegido) y los FBX de animación de DungeonCharacters, con *Loop Time* en idle/walk. El material `PBS_STAFFOFPAIN.mat` se convierte a URP/Lit.

## 5. Sistemas del juego

Estado: **todo el código está escrito y compila** (comprobado con el Roslyn de Unity). **Nada se ha probado aún con el casco**, y casi nada está montado/guardado en la escena: el montaje se hace con el menú *Isla Pirata* o con `GUIA_MONTAJE.md`.

### 5.1 Llave, palmeras y cofre
- **Palmeras sacudibles** (`ShakeablePalm` + `XRSimpleInteractable` en la raíz):
  - las coloca *Isla Pirata > Palmeras sacudibles* con el modelo `Tree_a`/`Tree_b`;
  - se agarra el tronco con el grip (como mucho a 1,2 m) y se sacude la mano en horizontal: se balancea, vibra y caen cocos de la copa (`Copa`);
  - los cocos vuelven a crecer.
- **Llave**:
  - lleva `KeyItem` (id `cofre`);
  - va escondida en una palmera (`ShakeablePalm.hiddenKey`). Al empezar sube sola a la copa y se ve entre los cocos;
  - cae tras `dropKeyAfterCoconuts` cocos;
  - al cogerla por primera vez completa el objetivo `llave`.
- **Cofre** (`ChestLock` en un objeto `Cofre`):
  - hijos `chest_close` (cerrado), `chest_open` (abierto, oculto), `coins` (recompensa, oculta) y `Cerradura`, con `XRSocketInteractor` y una esfera trigger **encima de la tapa**;
  - al encajar la llave correcta cambia de modelo, muestra las monedas, gasta la llave y completa el objetivo `cofre`;
  - para probarlo sin casco: menú contextual **Abrir ahora**.
- Lo monta *Isla Pirata > Llave y cofre*.

### 5.2 Barca y remo
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
  3. **Remar:** el **grip = pala en el agua**. Grip apretado y tirar hacia el cuerpo = avanzar (la velocidad de la mano hacia atrás, respecto a donde mira el jugador, en espacio de la barca). Sin grip, se recupera sin frenar. Un solo remo = girar.
  - Bolita en la empuñadura: gris = libre, amarilla = mano cerca, verde = cogido, azul = pala en el agua.
  - La posición de las manos sale de los objetos `Left/Right Controller` y el grip de las acciones `XRI Left/Right Interaction/Select Value`. Leerlos solo de `InputDevices` no funcionaba con OpenXR. Lo asigna *Isla Pirata > Barca > 2*.
- **Física de la barca:** el collider del fondo del casco está desactivado en la escena; las paredes empiezan en Y ≈ 3,98. *Isla Pirata > Barca > Diagnóstico* (sección FÍSICA) comprueba el terreno bajo el casco y los colliders que lo tocan, y el botón **4** mueve la barca a agua más profunda si está varada. `BoatDoctor` se pasa solo al objeto del Rigidbody si está en un hijo.
- **No usamos joystick** para remar.
- *Isla Pirata > Barca*: **Diagnóstico** (informe `[BARCA]`), **Preparar barca y remos**, **Arreglar teletransporte**.

Scripts:
- `BoatRowing.cs`: embarque/desembarque, amortiguación (`linearDamping`/`angularDamping` se aplican en `Awake`), quilla (`keelDamping`), eventos `onBoarded`/`onDisembarked`. **No empuja la barca.**
- `BoatHandRowing.cs`: el **único** script que empuja la barca. Lee grip y posición de las manos **directamente de `InputDevices`**, da vibración, escribe `[MANO] ...` y avisa `NO ESTAS A BORDO`. Valores que funcionaban: Push Force 30, Reverse 0.6, Turn Force 3, Max Angular Speed 0.6.
- `Oar.cs`: remo agarrable. Una mano solo puede coger un remo. `Oar.HeldByHand(XRNode)` dice qué remo tiene cada mano.
- `BoatDoctor.cs`: diagnóstico sin XR. Informe de física, joints, animators, colliders y terreno, y prueba de empuje forzado con y sin colisiones. Va en el **mismo objeto que el Rigidbody**. (En la escena guardada está en un hijo `BoatDoctor` de la barca, así que da error "no tiene Rigidbody".)

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
  - `ChestLock.requireNight`: de día la llave entra pero no gira (texto *"solo bajo la luna…"*, vibración); al anochecer, con la llave puesta, se abre sola. `breaksCurse` llama a `Curse.Break()`;
  - `Hourglass` (`RelojArena.prefab`, hecho con primitivas): al darle la vuelta llama a `DayNightCycle.TransitionTo` (9 h ↔ 23 h en 4 s) y la arena se vacía;
  - `DayNightCycle` (objeto `DiaNoche`, `dayLengthMinutes = 0`: el tiempo solo pasa con el reloj o el menú). Menú contextual *Hacer de noche / Hacer de día* para probar sin casco.
- **Cocos:**
  - `Coconut` hace daño según la velocidad del impacto; no hace daño mientras lo tienes en la mano;
  - puede darte en la cabeza al caer;
  - `Coco.prefab` sale del `Coconut` del asset Island;
  - `CoconutTree` deja caer cocos automáticamente en las palmeras pintadas en el Terrain (*Isla Pirata > Cocos*);
  - `ShakeablePalm` los suelta al sacudir.
- **Espada (`Sword`):** solo daña si la tienes en la mano y la punta (`Tip`) va a más de 2 m/s. La hoja necesita un collider trigger. Aún **no hay modelo de espada** elegido.

### 5.4b Telescopio (manipulación con dos manos)
- `Telescope.cs` sobre `Assets/Prefabs/Telescopio.prefab`, creado por *Isla Pirata > Telescopio* a partir de `Maritime Telescope/URP/Prefabs/maritime_telescope.prefab`. El modelo tiene LOD0-2, cada uno con `first/second/third_segment`.
- **Una mano:** coger, mover y rotar (`XRGrabInteractable`, `selectMode = Multiple`, `useDynamicAttach`).
- **Dos manos:** separar o juntar las manos extiende o recoge los tramos (`Segment.openOffset`, calculado desde las mallas) y cambia el zoom de 1.5x a 8x. `XRGeneralGrabTransformer.allowTwoHandedScaling = false`, porque el alargado lo hace `Telescope`.
- **Lente:** un Quad en el `Ocular` con un material Unlit (`LenteTelescopio.mat`) que muestra un RenderTexture de `CamaraTelescopio` (`stereoTargetEye = None`). Esa cámara solo está activa mientras se sujeta.
- **Encontrar el barco:** si `target` (el barco pirata) queda en el centro con zoom ≥ 3, completa `objectiveOnSpot` (`otear`, el primer objetivo).
- Para el **requisito de escalado** de la práctica, en el manual se presenta como "alargar el catalejo con dos manos". Si el profesor lo exige literalmente (escala uniforme), la alternativa es un mapa con `allowTwoHandedScaling = true`.

### 5.4c Carta del capitán (`TreasureMap` + `MapMarker`, *Isla Pirata > Carta*)
- **Imagen:** foto cenital ortográfica del Terrain, hecha en el editor con una cámara temporal, con tono sepia → `Assets/Prefabs/MapaIsla.png`. `worldMin/worldMax` (XZ del terreno) convierten posiciones del mundo a posiciones en la carta (arriba = +Z).
- **Objeto:** `Carta` (y `Carta.prefab`) = pergamino (Quad) + 2 **varillas** (Cylinder con CapsuleCollider: lo único que se agarra) + Canvas World Space con `TrackedDeviceGraphicRaycaster`.
  - `Rigidbody` kinematic sin gravedad: al soltarla se queda flotando;
  - `XRGrabInteractable` con `selectMode = Multiple`;
  - `XRGeneralGrabTransformer` con `allowTwoHandedScaling` (escala de 0.6x a 2.5x) → **requisito de escalado**.
- **Wayfinding:** icono "estás aquí" que gira con la cabeza; lugares `MapMarker` (Muelle descubierto desde el principio, Mirador, Palmeral, Barco fantasma). Se **descubren** al acercarse (`discoverRadius`); el barco aparece tras el objetivo `otear` (`hiddenUntilObjective`).
- **Viaje rápido (target-based):** el botón de un lugar descubierto con `allowFastTravel` llama a `TeleportationProvider.QueueTeleportRequest` hacia su `travelPoint`. Si vas en la barca, antes hace `Disembark`. El barco fantasma **no** permite viajar: hay que remar.
- **Objetivos:** `GameManager.objectivesText` apunta al texto de la carta. Se desactiva el `Panel Objetivos` de la muñeca.
- **Botón Menú del mando izquierdo:** trae la carta delante de ti.
- La escena necesita un **EventSystem con `XRUIInputModule`**, que crea la herramienta.

### 5.5 Movimiento avanzado
- **Natación (`SwimmingLocomotion`, en el XR Origin):**
  - se nada a partir de 1,3 m de profundidad (raycast al suelo desde la superficie del agua `// DEMO:`);
  - se bracea con el grip y hay flotación;
  - no actúa a bordo, porque el CC está apagado.
- **Escalada:** sin código, con `Climb Interactable` de XRI.

### 5.6 Mundo y objetivo
- **`GameManager`** (uno por escena, `GameManager.Instance`):
  - objetivos por id (`llave`, `cofre`, `isla`, opcionalmente `aranas`/`piratas` con `required` > 1), que se completan con `CompleteObjective(id)`;
  - victoria al completarlos todos y derrota si muere el jugador;
  - reiniciar con A/X: **la escena tiene que estar en Build Profiles > Scene List** (lo hace *Crear escena propia*);
  - UI opcional con TMP (usar `[x]`/`[ ]`, no ✔: la fuente por defecto no lo tiene).
- **Diseño actual:** **el cofre está en el barco pirata** (`StylShip_Unity.prefab`, con Mesh Colliders en todas sus piezas). Objetivos: `llave` → `barco` → `cofre`.
  - `ZonaBarcoPirata` (`ObjectiveZone` con id `barco`, 6 m alrededor del barco) se completa al llegar remando.
  - `anclaBarcoPirata` (en la cubierta, Teleporting → Disembark) sirve para subir al barco; para volver, se usa el ancla de la barca de remos.
  - Lo monta *Isla Pirata > Juego > Preparar barco fantasma*. La `ZonaIsla` (objetivo `isla`) se eliminó.
- **`ObjectiveZone`:** completa un objetivo cuando `watch` (la barca), con el jugador a bordo, entra en su Box Collider. Comprueba bounds cada 0,2 s, sin depender de triggers.
- **`DayNightCycle`:** gira la Directional Light (`// DEMO:`), cambia intensidad, color, ambiente y la exposición de una **copia** del skybox. Eventos `onNightStarted`/`onDayStarted`.

## 6. Lecciones aprendidas (errores que ya pasaron)

| Síntoma | Causa real | Qué hacer |
|---|---|---|
| El remo no hace nada y no salen logs | El XR Origin **no es hijo de la barca** (`OnBoard` false) | Embarcar con el ancla (Teleporting → Board) o `startOnBoard`; asignar `Xr Origin` en los scripts |
| Se cae de la barca al pulsar Play | `Board()` corría un frame tarde, o el Move/Gravity seguían activos | `startOnBoard` embarca en `Awake` |
| El jugador se mueve dentro de la barca en vez de moverla | Move provider o `CharacterController` activos a bordo | Desactivarlos en `disableWhileOnBoard` |
| La barca no se mueve aunque el script funcione | Terreno/muelle bajo el casco, joint anclado al mundo, Animator/script del asset, malla no hija del Rigidbody, o simplemente va con el jugador dentro y no hay referencia visual | Ejecutar `BoatDoctor` y leer el veredicto |
| Doble fuerza o comportamientos raros | Dos `Rigidbody` o dos `BoatHandRowing` | Un solo Rigidbody en la barca; un solo `BoatHandRowing` (el Diagnóstico lo avisa) |
| Cambios de damping "no hacen nada" | `BoatRowing` reasigna `linearDamping` en `Awake` | Cambiarlo en `BoatRowing`, no en el Rigidbody |
| Teleport anchor no responde | Collider no es hijo/mismo objeto, capas distintas, collider trigger | Ver 5.3 / *Isla Pirata > Barca > Diagnóstico* |
| Las anclas no embarcan; `BoatRowing` sin `Board()` | En `3f20571` se pegó `BoatHandRowing` dentro de `BoatRowing.cs` y se perdió `BoatRowing` | Restaurado: cada clase en su fichero; los valores del remo hubo que volver a ponerlos |
| Se rema con el grip en el aire, sin coger los remos | Los remos del asset no tenían nada agarrable (CLAUDE.md decía que sí) | `Oar` en cada remo + `requireOarGrab` |
| Pared invisible en el muelle / la barca roza al atracar | Colliders sólidos de las anclas en la capa Default | Capa `TeleportAnchor` sin colisiones |
| Al llegar al muelle los pies quedan dentro de las tablas | `anclaMuelle` estaba en el pivote del Dock, por debajo de la superficie | Subir el ancla a ras de suelo (lo hace la herramienta) |
| Enemigos que no se mueven: `NO esta sobre el NavMesh` | NavMesh sin hornear o AI Navigation sin instalar | *Isla Pirata > Aranas*, paso 1, y **Bake** |
| Los piratas se deslizan o se van solos | El asset trae `applyRootMotion` y un componente `Animation` legacy | El prefab `Pirata` los quita |
| Se rema (`[MANO] remada` > 0), `BoatDoctor` dice que avanza, pero la barca que ves no se mueve y un remo no sigue a la mano | El prefab del asset Boats marca barca y remos como **Static** (incluido Batching Static): la malla se queda congelada | *Isla Pirata > Barca > 2* quita el Static; el Diagnóstico y `BoatDoctor` lo avisan |
| `CommonUsages is an ambiguous reference` | Dos namespaces con `CommonUsages` | `UnityEngine.XR.CommonUsages` |
| Hay controllers en gris en Play, o `Current Tracking Origin: Unknown` | Modo manos/sin sesión XR | Quest Link como runtime OpenXR activo; revisar `XR Input Modality Manager` |

## 7. Cómo depurar

**Con el casco**
1. Consola de Unity: filtra por el prefijo del sistema:

   | Sistema | Prefijos |
   |---|---|
   | Barca y remo | `[BARCO]`, `[BARCA]`, `[MANO]`, `[REMO]`, `[DOCTOR]` |
   | Llave, cofre y palmeras | `[LLAVE]`, `[COFRE]`, `[PALMERA]` |
   | Combate | `[VIDA]`, `[JUGADOR]`, `[ENEMIGO]`, `[COCO]`, `[ESPADA]` |
   | Juego y objetivos | `[JUEGO]`, `[ZONA]` |
   | Movimiento y mundo | `[NADAR]`, `[LIANA]`, `[DIANOCHE]` |
   | Herramientas del menú | `[ARANAS]`, `[PIRATAS]`, `[COCOS]`, `[PALMERAS]`, `[HERRAMIENTA]` |

2. **Window > Analysis > Input Debugger**: ver `grip` en vivo del mando.
3. Inspector en modo **Debug** (⋮ > Debug) para ver variables privadas.
4. Vibración del mando = confirmación de que se lee el grip (también al acercar la mano a una empuñadura de remo).
5. Postes rojos fijos en el agua (`BoatDoctor`) para percibir el movimiento de la barca.
6. Si se prueba un build en el casco (no Link), los logs salen por `adb logcat -s Unity` o Meta Quest Developer Hub.

**Sin abrir Unity (para Claude Code)**
- **Comprobar que compila:**
  - compilador: `"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/DotNetSdk/dotnet.exe" .../DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll`;
  - referencias: `Editor/Data/Managed/UnityEngine/UnityEngine*.dll` y `UnityEditor*.dll`, `Library/ScriptAssemblies/` (InputSystem, XR.Interaction.Toolkit, XR.CoreUtils, TextMeshPro, UnityEngine.UI) y `NetStandard/ref/2.1.0/netstandard.dll`;
  - opciones: `-langversion:9`.
- **Estado de la escena:** se puede parsear el YAML de `Demo.unity` (lectura). La barca y el XR Origin son instancias de prefab, así que sus cambios están en `m_Modification` y `m_AddedComponents`.

## 8. Hoja de ruta

Lista de tareas concreta para la siguiente sesión, ideas de mejora y lo que falta en código: **`PROXIMOS_PASOS.md`**. Léelo al empezar y actualízalo al terminar.

| # | Paso | Estado |
|---|---|---|
| 1 | **Barca**: XR Origin hijo + `BoatDoctor` en verde + remo cogiendo los remos | Código hecho; falta montar con *Isla Pirata > Barca* y probar con el casco |
| 2 | Llave en palmera sacudible + cofre | Código y herramientas hechos; falta montar y probar |
| 3 | Teletransporte estable muelle ↔ barca | Arreglo hecho (herramienta); falta probar |
| 4 | `Health` + cocos + arañas | Código y herramientas hechos; falta montar y probar |
| 5 | Espada + piratas | Piratas hechos; **falta modelo de espada** y montarla |
| 6 | Maldición: reloj de arena, cofre de noche, tótems, esqueletos que se desmoronan | Código y herramientas hechos; falta montar y probar |
| 6b | Carta (escalado, wayfinding, objetivos, viaje rápido) | Código y herramienta hechos; falta montar y probar |
| 6c | Menú de la muñeca (configuración de la navegación y control) | Pendiente: requisito de la práctica |
| 7 | Día/noche y `GameManager` (objetivo y final) | Código hecho; falta montar la UI |
| 8a | Escena propia copiando la demo (*Crear escena propia*) | Herramienta hecha; falta pulsarla |
| 8b | **Reconstruir la escena desde cero (fuera de la demo)** | Pendiente, al final |
| 9 | Optimización para Quest y confort VR | Pendiente |

**Paso 8b, reconstruir desde cero** (la 8a ya hace la escena nuestra, pero su contenido sigue siendo el de la demo):
- Crear `Scenes/Main.unity` vacía con URP; reutilizar solo prefabs/materiales/texturas del asset de la isla.
- Convertir en prefabs: XR Origin, barca (con `Oar`, `Empunadura`, `BowRef`, `anclaBarco`), llave, cofre, palmeras, enemigos, `GameManager`.
- Sacar `anclaMuelle` de `Dock.prefab` a un prefab propio y dejar el `Dock.prefab` del asset como estaba.
- Reconstruir terreno/agua/muelle, iluminación, NavMesh, capas (`TeleportAnchor`…) y la matriz de colisiones.
- Volver a asignar los campos del Inspector (`Xr Origin`, `BowRef`, anclas, `Water Surface`, `Sun`) y repasar con `BoatDoctor` y *Isla Pirata > Barca > Diagnóstico*.
- Comprobar que nada nuestro referencia objetos de la demo (buscar `Find(`, nombres fijos y comentarios `// DEMO:`).

**Paso 9, optimización y confort:** batching, luces, sombras, tamaño de texturas; viñeta al girar/moverse y snap turn por defecto.

### Pendientes conocidos
- **Git:** hay un conflicto de merge sin resolver en `Assets/XR/Settings/OpenXRPackageSettings.asset` y un duplicado `OpenXR Package Settings.asset`. Hay que resolverlo antes de hacer commit.
- `BoatDoctor` está en un hijo de la barca y no en el objeto del Rigidbody; hay que moverlo.
- No hay modelo de espada ni sonidos asignados (está el asset `AS_Casual_Island_Game_Sounds_FREE`).

## 9. Plantilla para pedir tareas a Claude Code

```
Contexto: [qué sistema, qué objeto de la escena]
Objetivo: [qué debe pasar en el juego]
Restricciones: [ficheros que no se pueden tocar, API a usar]
Cómo lo comprobaré: [qué debería ver en la Console / en el casco]
```

Claude Code debe responder con: ficheros modificados, pasos a hacer en el editor (o qué botón del menú *Isla Pirata* pulsar), y la comprobación esperada.
