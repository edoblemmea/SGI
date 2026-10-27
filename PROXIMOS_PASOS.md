# Próximos pasos (actualizado el 27/10/2026)

**Resumen:**
- La historia es la del **barco fantasma** (ver `CLAUDE.md` > *Historia*): eres el grumete que no fue maldito y vuelves para liberar a tu tripulación de esqueletos.
- Casi todo el código está escrito y compila, pero **casi nada está montado en la escena ni probado con el casco**.
- Trabaja siempre en **`Assets/Scenes/IslaPirata.unity`**. Cada paso usa un botón del menú **Isla Pirata**.
- Si algo sale raro, pásale a Claude Code las líneas de la Console o una captura.

**Entrega: 4 de noviembre. Presentación: 5 de noviembre.**

> ⚠️ Guarda a menudo con **Ctrl+S**.

---

## A. Montaje en Unity (sin casco)

### 1. Escena propia (si no está hecha)
- [x] **Isla Pirata > Crear escena propia** → `Assets/Scenes/IslaPirata.unity`.

### 2. Barca, remos y teletransporte
- [x] XR Origin sobre las tablas del muelle; **Start On Board** desmarcado en Boat Rowing.
- [x] **Isla Pirata > Barca**: *Diagnóstico*, luego **2. Preparar barca y remos** (también quita el *Static* del prefab de la barca), **3. Arreglar teletransporte**, y *Diagnóstico* otra vez hasta `TODO CORRECTO`.
- [x] `BoatDoctor`: se pasa solo al objeto del Rigidbody. Cuando ya no haga falta, desactívalo (crea postes rojos y empuja la barca al empezar).

### 3. NavMesh y arañas (salen de las rocas)
- [ ] **Isla Pirata > Aranas**: NavMesh (**Bake**) y prefab Arana.
- [ ] Sección **4. Nidos en rocas**, de una de estas dos formas:
  - selecciona rocas que ya hayas puesto (RockFREE) y pulsa **Convertir rocas seleccionadas en nidos**;
  - o centra la vista en la isla y pulsa **Colocar rocas con nido**.
- [ ] **Quitar las arañas sueltas** (el grupo `Aranas` de antes), para que solo salgan de las rocas.
- [ ] Pulsa **Bake** otra vez en `NavMesh`: las rocas nuevas cambian el suelo.
- [ ] Selecciona cada `Nido ...` y comprueba que la **Salida** (al final de la línea morada) queda fuera de la roca, sobre el suelo y no en el agua.
- [ ] Si las rocas salen rosas, convierte sus materiales (`Assets/RockFREE/Materials`) a URP.

### 4. Materiales rosas
- [ ] Selecciona los materiales de `ChestFree/Materials` y `Rust Key/Materials` y usa **Edit > Rendering > Materials > Convert Selected Built-in Materials to URP**. El de StaffOfPain lo convierte solo la herramienta de la maldición.

### 5. Palmeras, cocos, llave y cofre
- [ ] **Isla Pirata > Cocos**: *Crear prefab Coco*. Opcional: cocos en las palmeras del Terrain.
- [ ] **Isla Pirata > Palmeras sacudibles**: crear el prefab y colocar palmeras sobre la isla.
- [ ] **Isla Pirata > Llave y cofre**: preparar la llave (`rust_key`), esconderla en una palmera y preparar el cofre (`chest_close`, que ya está **sobre el barco fantasma**).

### 6. Barco fantasma, esqueletos y maldición
- [ ] **Isla Pirata > Juego**: *Crear / actualizar GameManager* y, en la sección 2, **Preparar barco fantasma**. Esto crea el ancla de la cubierta y la zona de llegada; los objetivos quedan `llave` → `barco` → `cofre`.
- [ ] **Isla Pirata > Barca > 3. Arreglar teletransporte** otra vez (para la nueva ancla de la cubierta) y luego **Diagnóstico**.
- [ ] **Isla Pirata > Escala de cuerda** → *Crear / actualizar escala*: para subir trepando desde la barca. Comprueba que `Abajo` queda a ras del agua y `Punto cubierta` sobre la cubierta.
- [ ] Pulsa **Bake** en `NavMesh`: la cubierta del barco fantasma tiene que salir azul.
- [ ] **Isla Pirata > Esqueletos piratas**:
  1. *Crear / actualizar prefab Esqueleto*. Arrastra uno a la escena, dale a Play y haz clic derecho en SkeletonCrumble > **Desmoronar ahora**: tiene que caer como un muñeco y hundirse.
  2. *Colocar esqueletos* (se ponen alrededor del cofre).
  3. *Quitar los piratas leñadores antiguos*. Después se puede borrar la carpeta `Assets/FreeCharacter`.
- [ ] **Isla Pirata > Maldicion** → **Preparar maldición**. Crea `DiaNoche` (empieza a las 9 h), `Maldicion`, el cofre maldito, el **Reloj de arena** junto al cofre y 4 **tótems** alrededor. Mueve los que queden fuera de la cubierta.
- [ ] Prueba sin casco, en Play:
  - el texto del cofre aparece si intentas abrirlo de día;
  - en `DiaNoche`, clic derecho > **Hacer de noche**: los tótems brillan más y la cerradura se ilumina;
  - en ChestLock, clic derecho > **Abrir ahora**: los tótems se apagan, los esqueletos se desmoronan y salen `[MALDICION]` y `[JUEGO] Objetivo 'cofre'`.

### 6b. Menú de la muñeca
- [ ] **Isla Pirata > Menu muneca** → *Crear / actualizar menú de la muñeca*. Hazlo **después** de la maldición (usa `DiaNoche`). En la Console, `[MENU] Menu creado...` sin ningún FALTA. Ctrl+S.
- [ ] Prueba con el casco: **Y** abre el menú; con el rayo derecho cambia el movimiento a continuo y anda con el joystick izquierdo; cambia el giro; quita la viñeta; cambia la hora; baja el volumen.

### 7. Telescopio
- [ ] **Isla Pirata > Telescopio**: *Crear prefab*. En el prefab, mueve el slider **Extension** de 0 a 1: los tramos tienen que salir hacia el ocular.
- [ ] *Colocar telescopio* en un sitio alto de la isla, con *Objetivo 'otear'* marcado, que será el primer objetivo.

### 8. Interfaz y carta
- [ ] **Isla Pirata > Juego** > sección 3: pantalla de fin y velo de daño. El panel de la muñeca lo sustituye la carta.
- [ ] **Isla Pirata > Carta**. Hazlo **después** del telescopio, las palmeras y el barco fantasma, porque los usa para crear los lugares:
  1. **Hacer foto de la isla**: genera `Assets/Prefabs/MapaIsla.png`. Ábrela y comprueba que se ve la isla desde arriba, en tono pergamino.
  2. **Crear / actualizar carta**:
     - crea la carta flotando delante del XR Origin;
     - crea los lugares en `Lugares (carta)`: Muelle, Mirador, Palmeral y Barco fantasma;
     - crea el **EventSystem**;
     - hace que los objetivos salgan en la carta.
  3. Revisa los `Punto de llegada` de cada lugar (dónde apareces al viajar): tienen que estar sobre el suelo, no dentro de rocas.
- [ ] Prueba con el casco:
  1. Coge la carta por las varillas y sepárala con las dos manos: se agranda.
  2. Al acercarte al mirador o al palmeral sale "Descubierto".
  3. Apunta con el rayo a un lugar descubierto y pulsa el gatillo: viajas allí.
  4. Pulsa **Menú** en el mando izquierdo: la carta vuelve delante de ti.
  5. El barco fantasma aparece en la carta tras verlo con el catalejo, y no permite viajar.

### 9. OpenXR (para el PC del casco)
- [ ] *Project Settings > XR Plug-in Management > OpenXR* (pestaña Windows):
  - añadir *Oculus Touch Controller Profile* y *Meta Quest Touch Plus Controller Profile*;
  - en *Play Mode OpenXR Runtime*, elegir **System Default**.

### 10. Git
- [x] Resolver el conflicto de `Assets/XR/Settings/OpenXRPackageSettings.asset`: quedarse con el nuevo `OpenXR Package Settings.asset`, que es el que usa Unity, y borrar el antiguo (roto). Claude Code puede hacerlo.
- [ ] Commit y push. Tu compañera hace *pull* y abre el proyecto con **Unity 6000.5.0f1**.

---

## B. Prueba con el casco (en el PC de tu compañera)
- [ ] Lo básico: mandos visibles, andar, girar, teletransporte.
- [x] Barca: teletransporte del muelle a la barca, coger los remos con el grip, remar y volver (probado el 27/10).
- [ ] Palmeras: sacudir, que caigan cocos y la llave.
- [ ] Catalejo: alargarlo con dos manos, mirar por el ocular y ver el barco → `¡Barco a la vista!`.
- [ ] Barco fantasma: llegar remando (`barco`), teletransportarse a la cubierta, luchar contra los esqueletos.
- [ ] Final: llave en el cofre de día ("solo bajo la luna…") → darle la vuelta al reloj de arena → noche → se abre → maldición rota → victoria.

---

## C. Código pendiente (pedírselo a Claude Code), por prioridad
1. ~~Mapa / carta~~: **hecho** (`TreasureMap`, `MapMarker`, *Isla Pirata > Carta*), falta probarlo.
2. ~~**Menú de la muñeca**~~: **hecho** (`WristMenu`, *Isla Pirata > Menu muneca*), falta montarlo y probarlo. Era:
   - tipo de movimiento: teletransporte / continuo;
   - giro por saltos / suave;
   - viñeta de confort;
   - hora del día;
   - volumen;
   - reiniciar.

   El EventSystem con XRUIInputModule ya lo crea la herramienta de la carta. El botón **Menú** del mando izquierdo ya lo usa la carta, así que el menú de opciones irá con otro botón (p. ej. **Y**).
3. **Texto de introducción** al empezar, en el muelle al amanecer.
4. **Espada** sencilla junto a la barca.
5. Sonidos de golpes, remo y cofre (asset `AS_Casual_Island_Game_Sounds_FREE`).
6. *(Si sobra tiempo)* trepar a una palmera (Climb Interactable de XRI), resaltado al apuntar objetos, cinturón para guardar la espada y el catalejo.

## D. Entrega
- [ ] **Documento**: objetivo de la aplicación, **manual de uso** de las interacciones 3D y **disclosure notice**. Hay que declarar el uso de Claude Code (LLM) en el código y las herramientas.
- [ ] **Vídeo** del recorrido completo: grabación del Quest o OBS sobre la ventana Game.
- [ ] Carpetas a entregar: `Assets`, `Packages`, `ProjectSettings`, `UserSettings`.

Documentación: `CLAUDE.md` (cómo está hecho todo y por qué) y `GUIA_MONTAJE.md` (montaje paso a paso, también a mano).
