# Atajos — Manual de uso

Atajos es un panel que aparece en pantalla y te recuerda qué hace cada uno de tus seis atajos. Los ejecutás desde ahí mismo, con un número.

Sirve para eso y nada más: dejar de olvidarte de los atajos que configuraste.

---

## Lo único que hay que aprender

**Sostené `Ctrl` + `Shift`.** Esperá un segundo. Aparece el panel abajo a la izquierda.

**Sin soltar**, apretá el número de lo que querés hacer.

**Soltá.** El panel desaparece.

Eso es todo.

Atajos arranca solo con Windows y vive en la bandeja del sistema, al lado del reloj. Desde ahí lo cerrás y abrís su carpeta de configuración.

| Mientras sostenés `Ctrl+Shift` | |
|-------------------------------|---|
| `1` a `6` | Ejecuta esa casilla |
| `Espacio` | Cambia al siguiente perfil |

### También podés hacer clic

Si preferís el mouse, hacé clic en la casilla y listo.

Mientras el puntero esté encima del panel, **el panel no se cierra aunque sueltes las teclas**. Así que podés abrirlo, llevar el mouse hasta ahí, soltar `Ctrl+Shift` y hacer clic con calma. Se cierra cuando alejás el puntero.

La casilla se ilumina cuando pasás por encima. Las casillas apagadas no responden al clic, porque no tienen nada asignado.

Hacer clic **no te saca de donde estabas**. Si estabas escribiendo en una ventana, el cursor sigue ahí. Eso importa para acciones como "abrir terminal acá", que necesitan saber qué carpeta tenías abierta.

Y una que conviene saber:

| `Ctrl` + `Alt` + `Q` | Cierra Atajos |
|---|---|

### Por qué hay que sostener un momento

El panel no aparece al toque a propósito. Espera unos milisegundos para asegurarse de que lo querés abrir de verdad.

Gracias a eso, cuando usás `Ctrl+Shift+1` en Excel o `Ctrl+Shift+Espacio` en VS Code, Atajos ni se entera: soltaste mucho antes. No te pisa nada.

---

## Qué hace cada casilla

Depende del perfil que tengas activo. El nombre del perfil se ve abajo del panel.

### Perfil "General"

| | Qué hace |
|---|---|
| **1** | Cambia la salida de audio: si estás en los altavoces pasa a los auriculares, y al revés |
| **2** | Abre VS Code en la carpeta que tenés abierta en el Explorador |
| **3** | Levanta el gateway de OpenClaw |
| **4** | Abre una terminal en la carpeta que tenés abierta en el Explorador |
| **5** | Comprueba que el servicio LocalDrop esté corriendo y te copia la dirección al portapapeles |
| **6** | Arregla el monitor cuando queda en negro después de suspender |

### Perfil "Notas"

| | Qué hace |
|---|---|
| **1** | Abre la nota diaria de hoy en Obsidian |
| **2** | Trae Recordá al frente, o lo manda de vuelta a la bandeja si ya estaba adelante |

Las casillas que se ven **grises y apagadas** están libres. No hacen nada, y no es un error.

---

## Dónde vive todo

La app está instalada en:

```
C:\Users\<tu usuario>\AppData\Local\Atajos
```

El camino corto para llegar: clic derecho en el icono de la bandeja → **Abrir carpeta de configuración**.

## Cambiar qué hace una casilla

Todo se configura en un solo archivo: **`perfiles.json`**, que está junto al programa. Abrilo con cualquier editor de texto.

Cada casilla se ve así:

```json
"G4": { "nombre": "Terminal aquí", "icono": "E756", "accion": "open-terminal-here.ps1" }
```

| Campo | Qué es |
|-------|--------|
| `G4` | Qué casilla es. `G1` es la casilla 1, `G4` la casilla 4, y así. **No lo cambies.** |
| `nombre` | El texto que se ve en el panel. Ponele lo que te sirva a vos. |
| `icono` | El dibujito. Es un código, ver más abajo. |
| `accion` | Qué archivo se ejecuta. Tiene que estar en la carpeta `actions`. |
| `opciones` | Opcional. Datos que necesita esa acción. La mayoría no necesita ninguno. |
| `estado` | Opcional. Hace que la casilla muestre una línea chiquita con lo que está pasando ahora mismo. |

### Ejemplo: intercambiar dos casillas

Si querés que la terminal esté en la casilla 1 y el volumen en la 4, intercambiá los contenidos:

```json
"G1": { "nombre": "Terminal aquí", "icono": "E756", "accion": "open-terminal-here.ps1" },
"G4": { "nombre": "Altavoces",     "icono": "E767", "accion": "open-volume-mixer.ps1" }
```

### Ejemplo: dejar una casilla libre

Vaciá el campo `accion`:

```json
"G3": { "nombre": "", "icono": "E710", "accion": "" }
```

### La casilla de cambiar salida de audio

Esa casilla, además de hacer algo, te cuenta cómo está la cosa. Debajo del
nombre muestra por dónde está sonando el audio ahora y a dónde va a saltar si la apretás:

```
Cambiar salida
HyperX → Realtek
```

Los dos dispositivos salen de `opciones`:

```json
"G1": { "nombre": "Cambiar salida", "icono": "E8AB", "accion": "toggle-audio-output.ps1",
        "opciones": ["Realtek", "HyperX"], "estado": "salida-audio" }
```

Alcanza con un pedacito del nombre y no importan mayúsculas ni minúsculas: `HyperX` encuentra
`Auriculares (HyperX Cloud III)`. Si cambiás de auriculares, cambiás esa palabra ahí y listo —
tanto lo que muestra el panel como lo que hace la tecla salen de esa misma línea, así que no
se pueden desincronizar.

Si escribís el nombre de un dispositivo que no está enchufado, la tecla no hace nada y el
motivo queda anotado en `atajos.log`.

### Que los cambios se apliquen

Cerrá el panel y volvé a abrirlo. **No hace falta reiniciar nada**: el archivo se vuelve a leer cada vez que el panel aparece.

---

## Cambiar un icono

El campo `icono` es un código de la fuente de iconos de Windows. Para ver los códigos disponibles:

1. Abrí el **Mapa de caracteres** de Windows (buscalo en el menú Inicio).
2. Elegí la fuente **Segoe Fluent Icons**.
3. Hacé clic en el icono que te guste y mirá el código abajo (algo como `U+E767`).
4. Poné sólo la parte de después del `+`: `E767`.

También podés buscar en internet "Segoe Fluent Icons list", que hay listas con todos.

Algunos útiles para empezar:

| Código | Icono |
|--------|-------|
| `E710` | Signo de más (para casillas libres) |
| `E713` | Engranaje |
| `E721` | Lupa |
| `E74E` | Disquete / guardar |
| `E787` | Calendario |
| `E7C3` | Documento |
| `E8B7` | Carpeta |

Si ponés un código que no existe, no se rompe nada: aparece un icono genérico.

---

## Agregar una acción nueva

1. Escribí un archivo `.ps1` con lo que quieras hacer y guardalo en la carpeta **`actions`**.
2. En `perfiles.json`, poné el nombre de ese archivo en el campo `accion` de la casilla que quieras.

Ejemplo. Creás `actions\open-downloads.ps1` con esta línea:

```powershell
Start-Process explorer.exe (Join-Path $env:USERPROFILE 'Downloads')
```

Y lo enganchás a la casilla 3:

```json
"G3": { "nombre": "Descargas", "icono": "E896", "accion": "open-downloads.ps1" }
```

> **Dos reglas para los scripts:**
>
> **Escribilos en inglés y sin acentos, ni siquiera en los comentarios.** PowerShell malinterpreta los acentos en ciertos casos y el texto sale corrupto sin avisar.
>
> **No hagas que un script "apriete teclas" por vos.** Cuando la acción se ejecuta, vos tenés `Ctrl+Shift` apretados, así que cualquier combinación que el script mande llega con esos dos encima y no funciona. Si querés un efecto del sistema, buscá el comando que lo hace directamente.

### Cambiar entre qué dispositivos de audio alterna la casilla 1

Abrí `actions\toggle-audio-output.ps1` y mirá las dos primeras líneas de configuración:

```powershell
$deviceA = 'Realtek'
$deviceB = 'HyperX'
```

Poné un pedazo del nombre de cada dispositivo, como aparece en el panel de sonido de Windows. No hace falta el nombre completo ni respetar mayúsculas.

---

## Perfiles

Un perfil es un juego completo de seis casillas. `Espacio` (sosteniendo `Ctrl+Shift`) pasa al siguiente.

Si hay un solo perfil, el atajo no existe y el panel no lo menciona.

Para agregar uno, copiá un bloque entero dentro de `perfiles`:

```json
{
  "id": "trabajo",
  "nombre": "Trabajo",
  "teclas": {
    "G1": { "nombre": "Correo", "icono": "E715", "accion": "" },
    "G2": { "nombre": "", "icono": "E710", "accion": "" },
    "G3": { "nombre": "", "icono": "E710", "accion": "" },
    "G4": { "nombre": "", "icono": "E710", "accion": "" },
    "G5": { "nombre": "", "icono": "E710", "accion": "" },
    "G6": { "nombre": "", "icono": "E710", "accion": "" }
  }
}
```

El `id` tiene que ser distinto al de los otros perfiles. El `nombre` es lo que se ve en el panel.

Acordate de la coma entre bloques: `},` antes del siguiente `{`.

---

## Cuando algo no anda

### El panel no aparece

- Fijate que Atajos esté corriendo. Buscá `Atajos.exe` en el Administrador de tareas.
- Sostené `Ctrl+Shift` un poco más, sin apretar ninguna otra tecla.
- Si estás dentro de un juego en pantalla completa, el panel no se puede mostrar encima. No tiene arreglo. En modo ventana sin bordes anda bien.

### El panel abre, pero los números no hacen nada

Suele pasar cuando la ventana que tenés adelante se abrió **como administrador**. Windows no le entrega atajos a un programa común cuando el foco está en uno con permisos elevados.

Hacé clic en cualquier otra ventana y probá de nuevo. Si te pasa seguido, se puede configurar Atajos para que arranque con permisos de administrador.

### Una casilla puntual no hace nada

Puede ser que esté libre a propósito: si se ve gris y apagada, es eso.

Si no, abrí **`atajos.log`**, que está junto al programa. Ahí queda anotado todo lo que falla, con el motivo. Por ejemplo:

```
2026-08-09T22:14:03  G1 -> 'open-daily-note.ps1' exit=1: No se pudo abrir la nota
```

Si el archivo no existe o está vacío, es que no falló nada.

### El panel muestra un cartel naranja con un error

Significa que `perfiles.json` tiene un problema de formato. El cartel te dice el archivo y en qué línea.

Casi siempre es una coma de más, una coma que falta, o una comilla sin cerrar. Corregilo, cerrá el panel y volvé a abrirlo.

Si te trabaste, pegá el contenido en un validador de JSON online y te va a marcar el error exacto.

### El panel se queda abierto y no se va

Alejá el puntero del panel. Se mantiene abierto a propósito mientras el mouse está encima, para que puedas hacer clic sin apuro.

### Quiero cerrarlo

Clic derecho en el icono de la bandeja y **Salir**. O `Ctrl` + `Alt` + `Q`.

### No encuentro el icono de la bandeja

Windows 11 esconde los iconos nuevos. Hacé clic en la flecha `^` al lado del reloj y ahí va a estar.

Para tenerlo siempre a la vista, arrastralo desde ese panelcito hasta la barra de tareas. Windows se acuerda.

---

## Arranque automático

Ya está configurado: hay un acceso directo en la carpeta de inicio de Windows.

Para que **deje** de arrancar solo: `Win` + `R`, escribí `shell:startup`, aceptá, y borrá el acceso directo `Atajos` que está ahí.

---

## Llevarlo a otra computadora

Copiá la carpeta entera del programa. Nada más.

No hace falta instalar nada, ni permisos de administrador, ni ningún programa del teclado. Funciona en cualquier PC con Windows, con el teclado que sea.

Dos avisos:

- Las acciones que dependen de programas de tu PC (LocalDrop, OpenClaw) no van a funcionar en la otra máquina. No rompen nada: quedan anotadas en `atajos.log`.
- El perfil activo se guarda por computadora, así que cada una arranca en el suyo.

---

## Los archivos, en una tabla

| Archivo | Qué es | ¿Lo puedo tocar? |
|---------|--------|------------------|
| `Atajos.exe` | El programa | No |
| `perfiles.json` | Tu configuración | **Sí, es el que vas a editar** |
| `actions\` | Los scripts que se ejecutan | Sí, para agregar acciones |
| `perfil-activo.txt` | Recuerda en qué perfil estabas | No, se maneja solo |
| `atajos.log` | Anotaciones de lo que falló | Se puede borrar cuando quieras |
| `atajos.log.1` | El registro anterior, cuando el actual creció mucho | Se puede borrar cuando quieras |
| `atajos.ico` | El icono | No |
| `MANUAL.md` | Este archivo | — |
