# Guía de montaje en el editor

Scripts en `Assets/scripts/`. Haz los pasos **en orden**: el paso 0 es obligatorio porque arregla la barca.
Después de cada paso, dale a Play con Quest Link y comprueba lo que dice **"Cómo comprobarlo"**.

---

## Antes de nada: escena propia

1. Abre `Assets/Island/Scenes/Demo.unity`.
2. Menú **Isla Pirata > Crear escena propia**. Crea y abre `Assets/Scenes/IslaPirata.unity`, con su propio terreno (`IslaPirata_Terrain.asset`), y la pone la primera en Build Profiles.
3. **A partir de aquí trabaja siempre en `IslaPirata.unity`.** Donde esta guía diga "abre Demo.unity", abre `IslaPirata.unity`.
4. Si ya habías horneado el NavMesh en la demo, selecciona `NavMesh` y pulsa **Bake** otra vez en la escena nueva.

**Cómo comprobarlo:** en la Console salen las líneas `[ESCENA] Copiada ...`, `[ESCENA] Terreno 'Terrain': TerrainData copiado ...` y `Scene List: ... es la escena 0`. Dale a Play: todo debe verse igual que en la demo.

---

## 0. Arreglar la barca (OBLIGATORIO)

> **Atajo (recomendado):** menú **Isla Pirata > Barca**.
> 1. **Diagnóstico**: escribe en la Console un informe `[BARCA]` con lo que falla.
> 2. **Preparar barca y remos**: rellena BoatRowing, añade BoatHandRowing (con *Require Oar Grab*) y hace agarrables los remos `Paddle` y `Paddle (1)`.
> 3. **Arreglar teletransporte**:
>    - crea la capa `TeleportAnchor`, que no choca con nada, y mete las anclas en ella;
>    - la añade a la Raycast Mask del Teleport Interactor;
>    - conecta Teleporting → Board / Disembark;
>    - pone el ancla de la barca en el asiento del remero y la del muelle a ras de las tablas.
>
> Después vuelve a pulsar **Diagnóstico**: debe acabar en `TODO CORRECTO`. Guarda con Ctrl+S.
>
> **Remar:** acerca la mano a la empuñadura de un remo (vibra un poco), aprieta el **grip** y tira hacia ti. Remar con un solo remo hace girar la barca.
>
> Lo de abajo es el procedimiento manual equivalente.

**Qué pasaba:** `BoatRowing.cs` contenía la clase `BoatHandRowing`. Se había perdido la clase `BoatRowing` (la de `Board()` y `Disembark()`) y las anclas llamaban a métodos que no existían.
**Ahora:** `BoatRowing.cs` = embarcar, desembarcar y quilla. `BoatHandRowing.cs` = remar con las manos (el único script que empuja la barca).

1. Abre `Assets/Island/Scenes/Demo.unity` y selecciona `OldWoodenRowboat_Complete`.
2. El componente que había pasa a ser **Boat Rowing**. Rellena sus campos:
   - `Bow Reference` = `BowRef`
   - `Xr Origin` = `XR Origin (XR Rig)`
   - `Disable While On Board`: arrastra **Move** (Dynamic Move Provider), **Gravity**, **Jump**, **Grab Move** y **Climb** (están en `XR Origin > Locomotion`).
3. **Add Component > Boat Hand Rowing** en el mismo objeto. Antes tenía estos valores; vuelve a ponerlos:
   - `Xr Origin` = XR Origin, `Bow Reference` = BowRef
   - `Left Grip` / `Right Grip` = `XRI Left/Right Interaction/Select Value` (los mismos que tenía)
   - `Push Force` 30, `Reverse Multiplier` 0.6, `Turn Force` 3, `Max Angular Speed` 0.6
4. Revisa los eventos de las anclas:
   - `anclaBarco` → **Teleporting** → `OldWoodenRowboat_Complete` → `BoatRowing.Board`. Ahora mismo está en *Select Exited*; puede funcionar, pero *Teleporting* es lo correcto y es lo que usa `anclaMuelle`.
   - `anclaMuelle` → **Teleporting** → `BoatRowing.Disembark`.
   - Si alguno sale como *Missing*, vuelve a elegir la función en el desplegable.

**Cómo comprobarlo:** teletranspórtate a la barca. En la Console debe salir `[BARCO] A bordo. XR Origin ahora cuelga de 'OldWoodenRowboat_Complete'`. Al remar con el grip deben salir líneas `[MANO] ...` y vibrar el mando. No debe salir `NO ESTAS A BORDO`.

---

## 1. GameManager y vida del jugador

> **Atajo:** menú **Isla Pirata > Juego**. Tiene tres botones:
> 1. **GameManager**: objetivos, vida del jugador, música y jingles.
> 2. **Preparar barco fantasma**: ancla en la cubierta y zona de llegada en barca (objetivo `barco`).
> 3. **Interfaz**: panel de objetivos en la muñeca, pantalla de fin y velo rojo de daño.
>
> Lo de abajo es el montaje a mano equivalente.

1. Hierarchy > Create Empty `GameManager` > Add Component **Game Manager**. Ya trae 3 objetivos: `llave`, `barco` y `cofre`.
2. En `XR Origin (XR Rig)`: Add Component **Health** (`Max Health` 100; **desmarca** `Flash Children If Empty`) y **Player Damage Feedback** (su campo `Health` = el del XR Origin).
3. En el GameManager: `Player Health` = Health del XR Origin.
4. *(Opcional) Velo rojo:* Create > 3D Object > Quad, hijo de `Main Camera`, Position (0, 0, 0.3), quítale el *Mesh Collider*. Material nuevo: URP/Unlit, Surface Type **Transparent**, color rojo con alpha 0. Arrástralo al campo `Overlay` de Player Damage Feedback.
5. *(Opcional) Panel de objetivos:* Create > UI > Canvas, Render Mode **World Space**, Scale 0.001, hijo de `Left Controller`. Dentro, un `Text - TextMeshPro` → campo `Objectives Text`. Haz otro canvas delante de la cámara con un texto grande para `End Panel` y `End Text`.
6. La escena del juego (`IslaPirata.unity`) tiene que estar en **File > Build Profiles > Scene List**; lo hace *Crear escena propia*. Sin eso no se puede reiniciar con A.

**Cómo comprobarlo:** `[JUEGO] Partida empezada con 3 objetivos. playerHealth=XR Origin (XR Rig)`.

---

## 2. Llave y cofre

> **Atajo:** menú **Isla Pirata > Llave y cofre**. Hazlo después del paso 5b, porque necesita palmeras sacudibles.
> 1. **Preparar llave**: arrastra `rust_key` desde la Hierarchy.
> 2. **Esconder en una palmera** (elegida o al azar): al dar a Play, la llave sube sola a la copa. Se ve entre los cocos y cae después de N cocos.
> 3. **Preparar cofre**: arrastra `chest_close` desde la Hierarchy. Crea un objeto `Cofre` con la cerradura encima de la tapa, el modelo abierto y las monedas.
>
> **En el juego:** sacude la palmera hasta que caiga la llave, cógela y suéltala **encima del cofre**. El cofre se abre y aparecen las monedas.

**Llave** (`rust_key`): Rigidbody + **XR Grab Interactable** + **Key Item** (`Key Id` = `cofre`).

**Cofre** (`chest_close`):
1. Hijo vacío `Cerradura` en la cerradura, con **XR Socket Interactor** y un Sphere Collider **Is Trigger** de radio 0.15.
2. En el cofre, Add Component **Chest Lock**: `Socket` = Cerradura, `Required Key Id` = `cofre`.
3. El asset trae el cofre cerrado y el abierto como modelos separados. Arrastra `Assets/ChestFree/chest_open.prefab` como hijo, en la misma posición. Pon `Closed Model` = la malla cerrada y `Open Model` = chest_open. Si separas la tapa, usa `Lid` en su lugar.
4. *(Opcional)* `coins.prefab` dentro del cofre → `Reward`.

**Cómo comprobarlo:** al coger la llave sale `[LLAVE] Has cogido la llave` y `[JUEGO] Objetivo 'llave' 1/1`. Al meterla en la cerradura sale `[COFRE] ... abierto`. Sin casco: clic derecho en Chest Lock > **Abrir ahora**.

---

### 2b. Bolsillo para la llave
*Isla Pirata > Llave y cofre > 4. Crear / actualizar bolsillo* (después de *Preparar llave*). En el juego: con la llave en la mano, acércala a tu cadera derecha (bolita verde) y suelta el grip: se queda guardada (`[BOLSILLO] Llave guardada`). Para sacarla, cógela de la cadera con el grip. Si se cae al agua vuelve sola.

## 3. Barco fantasma y maldición

1. **Isla Pirata > Juego > Preparar barco fantasma**:
   - crea `anclaBarcoPirata` en la cubierta, junto al cofre, para desembarcar;
   - crea `ZonaBarcoPirata`, un `ObjectiveZone` con id `barco` que se completa al llegar remando.

   Después: **Isla Pirata > Barca > 3. Arreglar teletransporte** y **Bake** del NavMesh.
2. **Isla Pirata > Esqueletos piratas**: crear el prefab Esqueleto, colocarlos alrededor del cofre y quitar los leñadores antiguos.
3. **Isla Pirata > Maldicion > Preparar maldición**: ciclo día/noche (`DiaNoche`), `Maldicion`, cofre que solo se abre de noche, reloj de arena y tótems.

**Cómo comprobarlo:**
- al llegar remando sale `[ZONA] ... -> objetivo 'barco'`;
- con la llave de día sale `[COFRE] Es de dia: la llave no gira`;
- al darle la vuelta al reloj de arena: `[RELOJ] ... cae la noche` y luego `[COFRE] Ha salido la luna: la llave gira`;
- al final: `[MALDICION] ¡La maldicion se ha roto!`, `[ESQUELETO] ... se desmorona` y `[JUEGO] ===== VICTORIA =====`.

---

## 4. NavMesh y arañas

> **Atajo:** el menú **Isla Pirata > Aranas** hace casi todo esto automáticamente: instala AI Navigation, crea el NavMesh, crea el prefab `Assets/Prefabs/Arana.prefab` con su Animator y coloca las arañas conectadas al jugador. Los pasos de abajo son para hacerlo a mano o para entender qué hace la herramienta.

1. **Window > Package Manager > Unity Registry > AI Navigation > Install.** Ahora no está instalado y sin él no se puede hornear el NavMesh.
2. Create Empty `NavMesh` > Add Component **NavMesh Surface** > **Bake**. Comprueba en la Scene view que la isla sale en azul.
3. Arrastra una araña de `Spiders - characters with animations/.../Models/Prefabs/` (p. ej. `tarantula`) encima del NavMesh y conviértela en prefab propio en `Assets/Prefabs/`. Añádele:
   - **Nav Mesh Agent**
   - **Capsule Collider**
   - **Rigidbody** con Is Kinematic marcado
   - **Health** (`Max Health` 30, `Destroy On Death`)
   - **Enemy AI**: `Target` = Main Camera, `Target Health` = Health del XR Origin. Valores de araña: `Attack Range` 1.2, `Attack Damage` 10, `Attack Windup` 0, `Chase Speed` 4.
4. **Animaciones:** el modelo trae un Animator sin controller. Crea `Assets/Prefabs/SpiderAnimator.controller` con:
   - los estados Idle, Walk, Attack y Die (arrastra los clips del FBX);
   - un parámetro float `Speed` (Idle→Walk si es mayor que 0.1);
   - dos triggers, `Attack` y `Die` (desde Any State).

   Asígnalo al Animator.
5. *(Opcional)* Objetivo de matar arañas: en el GameManager añade `id` = `aranas` con `Required` igual al número de arañas, y pon `Objective On Death` = `aranas` en cada una.

**Cómo comprobarlo:** `[ENEMIGO] 'tarantula' Patrol -> Chase` al acercarte y `[VIDA] 'XR Origin (XR Rig)' recibe 10 de dano`. Si sale `NO esta sobre el NavMesh`, vuelve a hornear.

---

## 5. Cocos

> **Atajo:** menú **Isla Pirata > Cocos**. Crea `Assets/Prefabs/Coco.prefab` a partir del `Coconut` del asset Island. Después pone un `CoconutTree` en la copa de las palmeras pintadas en el Terrain más cercanas a la vista, y deja cocos en el suelo para cogerlos y lanzarlos.

1. Prefab `Coco`: esfera de 0.2 m (o el modelo que tengáis) con:
   - **Rigidbody** de masa 1
   - **Sphere Collider**
   - **XR Grab Interactable** (Movement Type *Velocity Tracking*, Throw On Detach ✔)
   - **Coconut**
2. En la copa de cada palmera: Create Empty `Cocos` > **Coconut Tree**: `Coconut Prefab` = Coco, `Player Head` = Main Camera, `Player Health` = Health del XR Origin.

**Cómo comprobarlo:** debajo de la palmera sale `[COCO] ... suelta un coco`. Si te da, sale `[COCO] Impacto ...`. Lánzaselo a una araña: también le hace daño.

---

### 5b. Palmeras sacudibles

Menú **Isla Pirata > Palmeras sacudibles**. Hace falta haber creado antes el prefab Coco.
1. **Crear prefab**: crea `Assets/Prefabs/PalmeraSacudible.prefab` con el modelo `Tree_a` (o `Tree_b`) del asset Island y cocos en la copa.
2. **Colocar palmeras**: las pone sobre el terreno, alrededor de la vista.
3. **Quitar 'Static' de las palmeras de la escena**: `Tree_a`/`Tree_b` vienen marcados *Static* y, si no, el tronco no se balancea. Las palmeras creadas antes del 27/10 lo necesitan; las nuevas ya salen bien.

**Prueba sin casco:** en Play, selecciona una palmera > clic derecho en *Shakeable Palm* > **Sacudir ahora (prueba sin casco)**: se balancea y cae un coco (o la llave, si la esconde).

Después vuelve a hornear el NavMesh, para que arañas y piratas esquiven los troncos nuevos.

**En el juego:** acércate, agarra el tronco con el **grip** y mueve la mano a los lados con fuerza. La palmera se balancea, el mando vibra y caen cocos (`[PALMERA] ¡Cae un coco!`). Cuidado, pueden darte en la cabeza. Los cocos vuelven a crecer a los 45 s.

## 6. Espada

**Espada:**
- raíz con Rigidbody + **XR Grab Interactable** + **Sword**;
- hijo `Blade` con un Box Collider que cubra la hoja (**Is Trigger**);
- hijo vacío `Tip` en la punta → campo `Tip`.

Los enemigos del barco son los **esqueletos** (apartado 3).

**Cómo comprobarlo:** al dar un tajo rápido sale `[ESPADA] Tajo a X m/s ...`. Si mueves la espada despacio no hace nada (`Min Swing Speed` 2).

---

## 7. Natación y escalada (opcionales, no están en la historia principal)

**Nadar:** en el XR Origin, Add Component **Swimming Locomotion**:
- `Water Surface` = el plano de agua de la demo (`// DEMO:`, en la escena propia el nuevo).
- `Ground Mask` = solo la capa del terreno (no la del agua).
- `Disable While Swimming` = Gravity, Jump y Climb.

En aguas poco profundas se camina; a partir de 1.3 m se nada. Se bracea con el grip y tirando hacia atrás. Sale `[NADAR] Al agua ...`.

**Escala de cuerda al barco pirata:** menú **Isla Pirata > Escala de cuerda** → *Crear / actualizar escala*. La cuelga en el costado del barco pirata que mira hacia la barca de remos (objeto `Escala de cuerda`, con `Abajo`, `Arriba` y `Punto cubierta`; muévelos si hace falta). En el juego: rema hasta la escala, acerca una mano, aprieta el **grip** y tira hacia **abajo**, alternando manos. Al asomar la cabeza por la borda apareces en la cubierta (`[ESCALA] ¡Arriba!`). Para volver, teletranspórtate al ancla de la barca.

**Escalar otras cosas:** no hace falta código. A la roca o pared ponle un collider y un **Climb Interactable** (XRI). Comprueba que el `Climb Provider` de `Locomotion` está activo.

---

## 8. Día / noche

Lo crea *Isla Pirata > Maldicion*: el objeto `DiaNoche` (**Day Night Cycle**) con `Sun` = la Directional Light (`// DEMO:`) y una `Luna`. `Day Length Minutes` = 0: el tiempo solo pasa con el reloj de arena (o el menú de la muñeca, cuando exista).
Para que salgan más arañas de noche: en `On Night Started` activa un grupo de arañas (`GameObject.SetActive(true)`) y en `On Day Started` desactívalo.

**Cómo comprobarlo:** `[DIANOCHE] Empieza a las 9.0 h (dia)`. Sin casco: clic derecho en el componente > *Hacer de noche*.

---

## 9. Confort VR y menú de la muñeca

Menú **Isla Pirata > Menu muneca** → *Crear / actualizar menú de la muñeca*:
- añade la **viñeta** (`TunnelingVignette` de los Starter Assets) bajo `Main Camera`, conectada a Move, giro suave y teletransporte;
- crea `Menu Muneca` (con `WristMenu`) encima del `Left Controller`;
- crea el EventSystem con XR UI Input Module si no existe.

**En el juego:** botón **Y** = abrir/cerrar. Se pulsa con el rayo del mando derecho y el gatillo. Opciones: movimiento (teletransporte / continuo en el joystick izquierdo; el derecho siempre teletransporta para la barca), giro (saltos / suave), grados del salto, velocidad, viñeta, hora (día/noche), volumen y reiniciar (pide confirmación). Se guardan en el dispositivo.

**Cómo comprobarlo:** `[MENU] Listo...` al empezar (sin ningún FALTA) y `[MENU] Movimiento continuo`, etc., al pulsar. Sin casco: clic derecho en *Wrist Menu* > **Abrir / cerrar**.

---

## Prefabs

Cuando cada cosa funcione, arrástrala a `Assets/Prefabs/`: XR Origin, barca, llave, cofre, araña, esqueleto, coco, espada, catalejo, reloj de arena y GameManager. Es el paso previo para migrar a la escena propia (CLAUDE.md, hoja de ruta, paso 8).
