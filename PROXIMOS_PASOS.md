# Próximos pasos (actualizado el 30/10/2026, 20:00)

**Resumen:**
- La historia es la del **barco fantasma** (ver `CLAUDE.md` > *Historia*): eres el grumete que no fue maldito y vuelves para liberar a tu tripulación de esqueletos.
- Todo el código está escrito y **la mayoría ya está montado en `IslaPirata.unity`** (revisado leyendo la escena guardada el 30/10). Falta sobre todo **probar con el casco** y **preparar la entrega**.
- Trabaja siempre en **`Assets/Scenes/IslaPirata.unity`** y en la rama **`barco-fantasma`**. Cada paso usa un botón del menú **Isla Pirata**.
- Si algo sale raro, pásale a Claude Code las líneas de la Console o una captura.

**Entrega: 4 de noviembre. Presentación: 5 de noviembre.**

> ⚠️ Guarda a menudo con **Ctrl+S**.

---

## 0. Urgente

### 0.1 Guardar la escena y limpiar restos
- [x] `BoatDoctor`, `Lugares (carta)` y `Boton Viaje carta` ya no están en la escena guardada, y no hay *Missing Script* (revisado el 30/10).
- [ ] **Ctrl+S en `IslaPirata.unity`**: a las 19:50 del 30/10 la escena guardada seguía siendo la de las 18:10 (la recuperada tras el cierre de Unity). El menú de carta náutica solo existía en memoria.
- [ ] Borrar `Assets/_Recovery` desde el Project (era la copia de seguridad de la escena tras el cierre de Unity).
- [ ] Commit en `barco-fantasma` (hay muchos cambios sin guardar en git: scripts nuevos, `Assets/UI`, `Assets/Resources`).

### 0.2 Escalado: descartado
- [x] Decidido el 30/10: **no se hace**. La manipulación se cubre con **traslación** (coger, mover y lanzar la llave, la espada y los cocos) y **rotación**. Si el profesor lo pidiera, la vía rápida es el escalado con dos manos del `XR Grab Interactable` (p. ej. un barco en una botella).

---

## A. Montaje en Unity (sin casco)

### 1. Escena propia
- [x] **Isla Pirata > Crear escena propia** → `Assets/Scenes/IslaPirata.unity`.

### 2. Barca, remos y teletransporte
- [x] XR Origin sobre las tablas del muelle; **Start On Board** desmarcado en Boat Rowing.
- [x] **Isla Pirata > Barca**: *Diagnóstico*, **2. Preparar barca y remos**, **3. Arreglar teletransporte**, y *Diagnóstico* otra vez hasta `TODO CORRECTO`.

### 3. NavMesh y arañas (salen de las rocas)
- [x] NavMesh horneado (está en `Assets/Island/Scenes/Demo/NavMesh-NavMesh.asset`) y prefab `Arana`.
- [x] 3 nidos en rocas (`Nidos de aranas`).
- [ ] Selecciona cada `Nido ...` y comprueba que la **Salida** (al final de la línea morada) queda fuera de la roca, sobre el suelo y no en el agua.
- [ ] Si has movido rocas después de hornear, pulsa **Bake** otra vez en `NavMesh`.
- [ ] Si las rocas salen rosas, convierte sus materiales (`Assets/RockFREE/Materials`) a URP.

### 4. Materiales rosas
- [x] `ChestFree/Materials` y `Rust Key/Materials` convertidos a URP.

### 5. Palmeras, cocos, llave y cofre
- [x] Prefab Coco, palmeras sacudibles, llave escondida y cofre sobre el barco fantasma.
- [x] **Llave y cofre > 4. Bolsillo** (probado el 27/10).

### 6. Barco fantasma, esqueletos y maldición
- [x] `GameManager` con objetivos `llave` → `barco` → `cofre`, `ZonaBarcoPirata` y `anclaBarcoPirata`.
- [x] Escala de cuerda.
- [x] Esqueletos alrededor del cofre; leñadores antiguos quitados (ya se puede borrar `Assets/FreeCharacter`).
- [x] Maldición: `DiaNoche`, `Maldicion`, reloj de arena y tótems.
- [ ] Comprueba que la cubierta del barco fantasma sale azul en el NavMesh (si no, **Bake** otra vez).
- [ ] Prueba sin casco, en Play:
  - el texto del cofre aparece si intentas abrirlo de día;
  - en `DiaNoche`, clic derecho > **Hacer de noche**: los tótems brillan más y la cerradura se ilumina;
  - en ChestLock, clic derecho > **Abrir ahora**: los tótems se apagan, los esqueletos se desmoronan y salen `[MALDICION]` y `[JUEGO] Objetivo 'cofre'`.

### 6b. Menú de la muñeca
- [x] **Isla Pirata > Menu muneca** creado.
- [ ] Se vuelve a crear en el paso 6d con el aspecto nuevo.

### 6c. Intro y barras de vida (código del 30/10)
- [ ] **Isla Pirata > Intro (historia y titulo)**. En la Console: `[INTRO] Intro lista. Totem: ok, dia/noche: ok, musica: ok`. Si dice que el inicio está LEJOS del tótem, acerca `Inicio jugador`.
- [ ] Título con las imágenes del equipo (`Assets/UI/Titulo/`, las asigna el botón *Intro*): en `Intro` pon *Text Reveal* = **Pisoton**, desmarca *Reveal Glow*, *Stomp Center* = 0,5 / 0,3. Prueba con *Play Story* desmarcado y vuelve a marcarlo.
- [ ] Prueba con el casco: al pulsar Play la pantalla puede estar 1-3 s en negro (es Quest Link), pero **no debe sonar nada hasta que se vea la imagen** (`[INTRO] Casco listo: empiezo tras X s`). Empieza de noche, sin música y sin poder andar; el tótem con luz de fuego; X pasa página; mantener B salta; sale el título mientras amanece; luego música y ya puedes andar.
- [ ] Barras de vida: pega a una araña con la espada (sale la barra encima) y deja que te muerda (sale abajo en tu vista).
- [ ] (Opcional) un prefab de fuego con partículas en *Story Totem > Fire Effect Prefab*.

### 6d. Estilo de la interfaz y menú de carta náutica (código del 30/10, ver `GUIA_MONTAJE.md` > 9 y 11)
Muestras de todos los diseños: https://claude.ai/artifact/BJvN5ifdy85JwYH3v31T81
- [x] **Isla Pirata > Estilo pirata** (fuera de Play). Cuando pregunte por la fuente por defecto de TextMeshPro, **Sí, cambiarla** (pasa a Libre Baskerville). En la Console: `Fuentes: titulos PirataOne SDF, encabezados IMFellEnglishSC SDF, texto LibreBaskerville SDF`, sin ningún `FALTA`.
- [x] Comprueba que en `Assets/UI` están `Carta_Marco`, `Carta_Lineas`, `Pildora`, `Fila`, `Medallon` y los `Segmento_*` (los blancos son normales: se tiñen de oro al jugar).
- [x] **Isla Pirata > Menu muneca** → *Crear / actualizar menú de la muñeca* (sustituye el menú por el de carta náutica).
- [x] **Isla Pirata > Intro** otra vez. **Ctrl+S.**
- [x] Play y **Y**: `[MENU] Menu de carta nautica (creado con la herramienta)`; fondo azul noche con líneas doradas, pestañas en píldoras, el valor elegido relleno de oro, la fila se ilumina al apuntar.
- [x] **Decidir el aspecto de los paneles** (en el estilo guardado está **Banda**) (avisos, pistas, subtítulos, objetivos y pantalla final): *Isla Pirata > Estilo pirata - paneles de madera y oro* o *- paneles en banda difuminada*, probar los dos y quedarse con uno.
- [x] Si te quedas con Libre Baskerville: borrar desde el Project `Assets/UI/Fuentes/Alegreya-*.ttf`, `Alegreya*SDF.asset` y `OFL_Alegreya.txt`.
- [x] Prueba con el casco: subtítulos con la línea en dorado, pista de **Y** encima del mando izquierdo, aviso "Tu objetivo" al acabar la intro, "¡Objetivo cumplido!" al coger la llave; que los textos del menú se lean bien (si alguno queda pequeño, díselo a Claude Code).

### 7. Lo que falta montar
- [ ] **Isla Pirata > Rescate del jugador** (`PlayerRescue`): no está en la escena guardada. Sin él, si caes al mar no vuelves a la barca.
- [x] **Isla Pirata > Espada > 3. Funda** (`SwordHolster`): ya está en la escena.
- [ ] **Isla Pirata > Juego** > sección 3: pantalla de fin y velo de daño (comprobar).

### 8. OpenXR (para el PC del casco)
- [x] Perfiles de mando de Oculus Touch y Meta Quest Touch Plus; *Play Mode OpenXR Runtime* = **System Default**.

### 9. Git
- [x] Conflicto de `OpenXRPackageSettings.asset` resuelto.
- [x] `master` igualada a `barco-fantasma` (30/10).
- [ ] Commit y push de la limpieza del paso 0.1. Para pasar los cambios a `master` otra vez: `git checkout master`, `git pull`, `git merge --ff-only barco-fantasma`, `git push`, `git checkout barco-fantasma`.

---

## B. Prueba con el casco (en el PC de tu compañera)
- [x] Lo básico: mandos visibles, andar, girar, teletransporte.
- [x] Barca: teletransporte del muelle a la barca, coger los remos con el grip, remar y volver (probado el 27/10).
- [x] Palmeras: sacudir, que caigan cocos y la llave.
- [ ] Tótem narrador: al acercarte cuenta la historia con la voz (`Historia_completa.mp3`).
- [ ] Arañas: salen de las rocas al acercarte y te atacan; la espada las mata.
- [ ] Barco fantasma: llegar remando (`barco`), subir por la escala o teletransportarse a la cubierta, luchar contra los esqueletos.
- [ ] Final: llave en el cofre de día ("solo bajo la luna…") → darle la vuelta al reloj de arena → noche → se abre → maldición rota → victoria.
- [ ] Menú de la muñeca (**Y**, carta náutica): movimiento continuo, giro, viñeta, hora, volumen; que se lea bien y que la fila se ilumine al apuntar.

---

## C. Código pendiente (pedírselo a Claude Code), por prioridad
1. *(Si sobra tiempo)* trepar a una palmera (Climb Interactable de XRI), resaltado al apuntar objetos.

Hecho y ya no pendiente: menú de la muñeca, texto/voz de introducción (tótem narrador), espada y funda, sonidos (asset + `ProceduralSounds`), pistas de controles (`TutorialHints`).

## D. Entrega
- [ ] **Limpieza** (en una rama): *Isla Pirata > Limpieza del proyecto* (`GUIA_MONTAJE.md` > 12). Antes del build final, apagar los `debugLogs`.
- [ ] **Documento**: objetivo de la aplicación, **manual de uso** de las interacciones 3D y **disclosure notice**. Hay que declarar el uso de Claude Code (LLM) en el código y las herramientas. Citar las fuentes *Pirata One*, *IM Fell English SC* y *Libre Baskerville* (Google Fonts, SIL OFL 1.1).
- [ ] **Vídeo** del recorrido completo: grabación del Quest o OBS sobre la ventana Game.
- [ ] Carpetas a entregar: `Assets`, `Packages`, `ProjectSettings`, `UserSettings`.

Documentación: `CLAUDE.md` (cómo está hecho todo y por qué) y `GUIA_MONTAJE.md` (montaje paso a paso, también a mano).
