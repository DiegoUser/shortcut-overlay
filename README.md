# Atajos

Overlay always-on-top que muestra qué hace cada atajo, para no tener que acordarse. Se abre sosteniendo `Ctrl+Shift`, se ejecuta con un número, y soporta varios perfiles.

> **Estado: instalado y en uso (testing 1).** Corre desde `%LOCALAPPDATA%\Atajos`, arranca con Windows, vive en la bandeja. Falta el empaquetado self-contained para la laptop (testing 2).

> Este documento es la bitácora técnica: qué se decidió, por qué, y qué falta. **Para usar el programa, ver [MANUAL.md](MANUAL.md).**

## El problema

Había seis teclas macro configuradas y creciendo. Cada una hacía algo distinto y no había forma de recordarlas. Sin referencia visual las teclas dejan de usarse, que es exactamente lo que pasó con la mitad de ellas.

## Qué es

Un panel chico, siempre encima, con seis casillas en grid 3x2. Icono y nombre corto por casilla, y el perfil activo abajo. No roba foco, no bloquea clics, no aparece en la barra de tareas ni en alt-tab.

## Interacción

El panel es la interfaz completa. **No hace falta ninguna tecla especial**, y por eso funciona igual en un teclado con teclas G que en el de una laptop.

| Atajo | Qué hace |
|-------|----------|
| **Sostener `Ctrl+Shift`** | Abre el panel abajo a la izquierda. Se cierra al soltar. |
| `1` ... `6` (sosteniendo) | Ejecuta la acción de esa casilla |
| `Espacio` (sosteniendo) | Cicla al siguiente perfil. El atajo ni se registra si hay un solo perfil. |
| **Clic en una casilla** | Ejecuta esa acción. El panel sigue abierto mientras el puntero esté encima, aunque sueltes las teclas. |
| `Ctrl+Alt+Q` | Salir. Provisorio, hasta que exista el icono de bandeja. Deliberadamente sin `Shift`, para que no haga aparecer el panel al salir. |

Las casillas se rotulan `1`-`6`, no `G1`-`G6`: es la única etiqueta que es cierta en las dos máquinas.

### Clics: por qué se pudo, y qué lo sostiene

El diseño original llevaba `WS_EX_TRANSPARENT` para que los clics atravesaran el panel, con el argumento de que un overlay que traga clics es un estorbo. **Ese argumento valía para un panel siempre visible.** Este aparece sólo mientras se usa, así que la restricción se quedó sin motivo y se quitó.

Dos cosas lo sostienen:

- **`WS_EX_NOACTIVATE` pasó de cortesía a requisito.** Sin él, hacer clic en una casilla convertiría al overlay en la ventana en primer plano, y acciones como "abrir terminal en la carpeta actual" resolverían contra el overlay en lugar de contra el Explorador. Verificado: el foco no cambia al hacer clic.
- **El puntero encima mantiene el panel abierto.** Sin eso las casillas serían inclickeables en la práctica: el panel desaparecería en el camino hacia ellas. El enganche se arma recién cuando el puntero sale del panel una vez, para que un panel que abre debajo de un cursor quieto no se quede pegado en pantalla.

### El retardo de 350 ms es la pieza clave

Los atajos `1`-`6` y `Espacio` **existen sólo mientras el panel está abierto**, y el panel no abre hasta que `Ctrl+Shift` lleva 350 ms sostenido.

Eso es lo que hace que el sistema no moleste a nadie. `Ctrl+Shift+1` en Excel (formato número) y `Ctrl+Shift+Espacio` en VS Code (información de parámetros) se sueltan mucho antes de ese umbral, así que nunca nos llegan. Abrir el panel tiene que ser una intención, no un accidente de dedos.

El precio: si sostenés `Ctrl+Shift` más de 350 ms **y después** apretás `1` en Excel, gana el overlay.

### Por qué sondeo y no un hook

`RegisterHotKey` no puede expresar un atajo de sólo modificadores. La alternativa habitual es `WH_KEYBOARD_LL`, que pone a este proceso en el camino de cada tecla de la máquina, algo que los anti-cheat de juegos miran con razón con desconfianza.

En su lugar se consulta el estado de Ctrl y Shift con `GetAsyncKeyState` cada 50 ms. No se instala ningún hook, no se intercepta nada, y ninguna tecla pasa por nuestro código. Lo único que el overlay le saca a otras apps son sus seis atajos, y sólo mientras el panel está en pantalla.

### Descartado: la rueda del mouse para ciclar

`Ctrl+rueda` es zoom en casi toda app que exista, y justo vas a estar sosteniendo Ctrl cuando la gires. La colisión sería garantizada, no probable.

### Opcional, no implementado: las teclas G como atajo rápido

Si en algún momento se quiere que las G disparen acciones sin abrir el panel, el camino es mapearlas en iCUE a `F13`-`F18` (teclas que ningún teclado físico tiene, así que no chocan con nada) y registrarlas en la app. Son pocas líneas. Hoy no está, porque el panel alcanza y no depende de tener iCUE.

## Arquitectura

```
sostener Ctrl+Shift  ->  panel  ->  tecla 4
                                       |
                          Atajos.exe (perfiles.json + perfil-activo.txt)
                                       |
                          powershell oculto -> actions\open-terminal-here.ps1
```

Todo vive dentro del proyecto y se copia junto al ejecutable al compilar:

```
Atajos\
  Atajos.csproj
  perfiles.json        configuracion. Se edita a mano, la app NUNCA la reescribe.
  actions\
    _lib\
      Get-ExplorerPath.ps1   resuelve la carpeta del Explorador en foco
      run-action.ps1         envoltorio: convierte un fallo en una linea legible
    open-daily-note.ps1
    open-terminal-here.ps1
    open-vscode-here.ps1
    open-volume-mixer.ps1
    start-localdrop.ps1
    start-openclaw-gateway.ps1
    fix-display.ps1
```

Y en la carpeta de salida, generados en tiempo de ejecución:

```
  perfil-activo.txt    estado. Una linea. Lo escribe solo el overlay.
  atajos.log           solo errores.
```

Las rutas se resuelven desde `AppContext.BaseDirectory` (ver `AppPaths.cs`). No hay ninguna ruta de máquina en el código.

**La app es la única dueña de "atajo -> acción".** No hay iCUE en el camino, ni AutoHotkey, ni proceso intermediario. Un solo `powershell.exe` oculto por acción.

> **`C:\Scripts` ya no participa.** Sigue ahí con los `ScriptG*.{bat,vbs,ps1}` originales para que la configuración vieja de iCUE no se rompa, pero la app no lo lee ni lo escribe.

### Cómo se ejecuta una acción

```
Atajos.exe
  -> powershell -NoProfile -ExecutionPolicy Bypass -File actions\_lib\run-action.ps1 -Script <accion>
       -> & <accion>
       -> si tira error: escribe el mensaje en stderr como UTF-8 y sale con 1
  -> la app lee stderr y lo registra en atajos.log
```

El envoltorio existe para que los scripts no tengan que ocuparse cada uno de registrar sus fallos: alcanza con que tiren el error. Sin él, un `throw` llega al log como un cartel de PowerShell de seis líneas con los acentos rotos.

### Configuración y estado no comparten archivo

`perfiles.json` lo escribís vos; el perfil activo lo escribe la máquina en cada ciclado. Si vivieran juntos, el overlay reescribiría el archivo que estás manteniendo a mano y le destrozaría el formato. Por eso `perfil-activo.txt` está separado, y se escribe vía archivo temporal más `Move`, para que nadie lea un nombre a medio escribir.

### perfiles.json

```json
{
  "perfiles": [
    {
      "id": "general",
      "nombre": "General",
      "teclas": {
        "G1": { "nombre": "Altavoces", "icono": "E767", "accion": "open-volume-mixer.ps1" },
        "G4": { "nombre": "Terminal aqui", "icono": "E756", "accion": "open-terminal-here.ps1" }
      }
    }
  ]
}
```

- Las claves siguen siendo `G1`-`G6` y corresponden a las casillas `1`-`6` en ese orden.
- `icono` es el código hexadecimal de un glifo de Segoe Fluent Icons. Cambiar un icono es editar cuatro caracteres.
- `accion` es un nombre de archivo dentro de `actions\`. Vacío deja la casilla libre, y el overlay la dibuja atenuada.
- El archivo se relee cada vez que el panel se abre. No hace falta reiniciar nada.

### Comportamiento ante fallas

| Situación | Qué pasa |
|-----------|----------|
| `perfiles.json` roto o ausente | El overlay muestra la ruta y el error de parseo, no un panel vacío. Se recupera solo al volver a abrirlo. |
| `perfil-activo.txt` ausente o con un id inexistente | Se usa el primer perfil. |
| `accion` apunta a un script que no existe | Se registra en `atajos.log`. |
| Casilla sin acción | No pasa nada, y no es un error. Se dibuja atenuada. |
| Una tecla ausente del JSON | Se dibuja libre; no corre la grilla. |

Las acciones corren sin ventana. Un fallo sin registrar sería un fallo invisible, y por eso existe `atajos.log`.

## Inventario de acciones

Viven en `actions\`, nombradas por lo que hacen y **sin ninguna referencia a qué casilla las llama**.

| Acción | Qué hace |
|--------|----------|
| `toggle-audio-output.ps1` | Alterna la salida de audio entre los altavoces Realtek y los auriculares HyperX |
| `open-vscode-here.ps1` | Abre VS Code en la carpeta del Explorador en foco |
| `open-terminal-here.ps1` | Abre `cmd` en la carpeta del Explorador en foco, con el Escritorio como respaldo |
| `start-openclaw-gateway.ps1` | Levanta el gateway de OpenClaw en `~\.openclaw\workspace` |
| `start-localdrop.ps1` | Levanta LocalDrop en `:5000`, detecta la IP de LAN y copia la URL al portapapeles |
| `fix-display.ps1` | Recupera el monitor tras suspensión (`DisplaySwitch /clone` y después `/internal`) |
| `open-daily-note.ps1` | Abre la nota diaria de hoy en el vault `Journal` de Obsidian (vía `obsidian://daily`) |

### Regla: los scripts van en inglés

Todo el contenido de un `.ps1` (comentarios, nombres, mensajes, texto de botones) se escribe en inglés y en ASCII puro.

No es una preferencia estética. Windows PowerShell 5.1 malinterpreta los acentos en cuanto el archivo pierde el BOM, y ese fallo es silencioso: no hay error, sólo texto corrupto que aparece semanas después en un log o en un diálogo. **Escribiendo en inglés esa clase de error deja de existir**, en vez de quedar controlada.

El BOM se mantiene igual como red de seguridad, por si algún día entra un acento.

Verificable en cualquier momento:

```
powershell -NoProfile -Command "Get-ChildItem actions -Recurse -Filter *.ps1 | ForEach-Object { $b=[IO.File]::ReadAllBytes($_.FullName); '{0} no-ascii={1}' -f $_.Name, ($b | Where-Object { $_ -gt 0x7F }).Count }"
```

Tres bytes no-ASCII por archivo es el BOM. Cualquier número mayor es un acento que se coló.

### Qué se arregló al rehacerlas

Los originales de `C:\Scripts` sirvieron de referencia. Estos son los defectos que tenían y que acá no están:

| Defecto | Dónde estaba | Arreglo |
|---------|--------------|---------|
| Agarraba una ventana del Explorador cualquiera, no la que estás mirando | VS Code y terminal | `Get-ExplorerPath` compara contra `GetForegroundWindow` y sólo después cae al respaldo |
| Sin ninguna ventana del Explorador abierta, no hacía nada y no decía nada | VS Code | Respaldo al Escritorio, igual que la terminal |
| Llamaba `code`, que es un `.cmd` y parpadea una consola | VS Code | Lanza `Code.exe` directo |
| Ruta del ejecutable escrita a mano con el nombre de usuario | LocalDrop | Se arma desde `$env:USERPROFILE` |
| `$ErrorActionPreference = 'SilentlyContinue'` global | LocalDrop y monitor | Quitado. En un script que recupera una pantalla negra, esconder el motivo del fallo es el peor default posible. |
| Si el servidor nunca levantaba, seguía igual y mostraba una URL que no apuntaba a nada | LocalDrop | Espera con límite y avisa |
| Copiaba al portapapeles el texto `IP-not-detected` | LocalDrop | Si no hay IP de LAN, lo dice y copia la local |
| Dependía de `C:\ScriptsHome`, fuera del proyecto | OpenClaw | Llama a `openclaw gateway` directamente |

`open-daily-note.ps1` quedó como estaba. No tenía nada que arreglar.

### Regla: una acción no puede sintetizar teclas

**Nunca.** Una acción siempre se ejecuta con `Ctrl+Shift` físicamente apretados, porque eso es lo que mantiene el panel abierto. Cualquier combinación que la acción inyecte llega con esos dos modificadores encima.

Así se descubrió: la primera versión de la acción de audio mandaba `Win+Ctrl+V` para abrir el panel de sonido. Ejecutada desde una consola funcionaba perfecto. Desde el panel no hacía nada, porque Windows recibía `Win+Ctrl+Shift+V`, que no es ningún atajo. Verificado en los dos escenarios.

Si una acción necesita un efecto del sistema, hay que llamar a la API que lo produce, no imitar al usuario apretando teclas.

### La acción de audio, en detalle

Alterna el dispositivo de salida por defecto entre dos, buscados por texto parcial del nombre (`Realtek` y `HyperX`, configurables arriba del script).

- Enumera con `IMMDeviceEnumerator`, que es API documentada.
- Cambia el predeterminado con `IPolicyConfig`, que **no** está documentada, pero es estable desde Windows 7 y es lo que usa cualquier herramienta del rubro. No hay que instalar nada: sin módulos de PowerShell Gallery, sin NirCmd.
- Lo aplica a los tres roles (consola, multimedia y comunicaciones). Dejar uno afuera hace que algunas apps sigan usando el dispositivo anterior.
- Si el dispositivo por defecto no es ninguno de los dos, va al primero. Así un tercer dispositivo que se cuele no deja la tecla sin efecto.
- Si falta alguno de los dos, tira error listando las salidas activas, y eso aparece en `atajos.log`.

### Por qué la estructura vieja se rompió

El estado anterior era un triplete `ScriptG<N>.{bat,vbs,ps1}` por tecla, y terminó desalineado: `ScriptG3.bat` estaba asignado a la tecla **G4**, y `ScriptG1TercerPerfil` sólo tenía sentido si sabías que existía un tercer perfil de iCUE.

La causa no fue descuido. El nombre `ScriptG3` intenta decir dos cosas a la vez, qué tecla lo llama y qué hace, y cuando una de las dos cambia, el nombre miente sobre la otra. Ahora la acción no sabe quién la llama, así que no tiene nada sobre lo que mentir.

## Perfiles

Replican lo que ya existía en iCUE, en vez de inventar categorías nuevas.

| iCUE | Acá | Contenido |
|------|-----|-----------|
| Perfil 1 | no se replica | No hacía nada |
| Perfil 2 | **General** | Las seis acciones de uso diario |
| Perfil 3 | **Notas** | Sólo la nota diaria en la casilla 1; el resto libres |

Si "Notas" con una sola casilla resulta que no se usa, se borran unos renglones del JSON y listo.

## Stack

**WPF sobre .NET 10, C#.** Razones:

- Es el stack de casa. Este proyecto no es para aprender algo nuevo, es para resolver un problema.
- WPF hace ventanas sin borde, transparencia real y topmost sin pelear.
- Los hotkeys globales salen con `RegisterHotKey` de `user32`, sin dependencias.

Descartados: Electron (100 MB para dibujar seis cuadrados), WinForms (peor transparencia y DPI), y AutoHotkey.

**AutoHotkey no era una opción, aunque lo pareciera.** Las teclas G no emiten scancode estándar: Windows sólo ve un "Corsair composite virtual input device" creado por el driver de iCUE. AHK escucha después de que Windows recibe la tecla, así que quedaría **abajo** de iCUE, nunca en su lugar. Como el diseño final no depende de las teclas G, la pregunta dejó de importar.

## Trampas técnicas conocidas

| Tema | Qué hacer |
|------|-----------|
| Robo de foco | La ventana necesita `WS_EX_NOACTIVATE`. Sin eso, mostrar el overlay te saca el cursor de donde estabas escribiendo. |
| Clics | **Sin** `WS_EX_TRANSPARENT`: las casillas son clickeables. Ver más abajo. |
| Juegos | En **fullscreen exclusivo** ningún overlay se ve, y no tiene arreglo desde la app. En borderless windowed funciona bien. Asumir esa limitación. |
| Anti-cheat y pruebas automatizadas | Con el cliente de League of Legends en primer plano, **la entrada sintética deja de disparar atajos globales**. Verificado con un atajo propio, ajeno a Atajos: `keybd_event` no produce `WM_HOTKEY`. El sondeo de `GetAsyncKeyState` sigue funcionando, así que el panel abre pero las teclas no responden a input inyectado. Consecuencia práctica: **las pruebas automatizadas de las casillas no valen nada mientras el juego esté abierto**; hay que probarlas a mano. |
| Posición del panel | Con `SetWindowPos` en píxeles físicos, no con `Window.Left/Top`. Las unidades lógicas de WPF están escaladas por el DPI de un monitor, así que en un setup con escalas distintas dejan la ventana en el lugar equivocado. Se posiciona dos veces: mover a un monitor con otra escala provoca un re-layout y el tamaño medido antes queda viejo. |
| Hotkeys y UAC | `RegisterHotKey` no recibe la tecla si la ventana en foco corre elevada y el overlay no. Si pasa seguido, elevar el overlay vía Programador de tareas. |
| DPI | WPF sobre .NET Core ya es PerMonitorV2 por defecto; no hizo falta `app.manifest`. |
| Iconos | Fuente **Segoe Fluent Icons** en vez de archivos de imagen. Cambiar un icono es editar un string. |
| **Codificación de los `.ps1`** | **Guardar siempre en UTF-8 con BOM.** Windows PowerShell 5.1 lee un `.ps1` sin BOM usando la página de códigos ANSI, así que cualquier acento dentro del script queda corrupto antes de ejecutarse. No da error: simplemente el texto sale mal. Verificado: sin BOM `niña` llega como `niÃ±a`; con BOM, perfecto. |
| Escribir archivos con PowerShell | Nunca `Get-Content` + `Set-Content` sobre un archivo UTF-8: 5.1 lo lee como ANSI y lo reescribe corrupto. Usar `[IO.File]::ReadAllText($p, [Text.Encoding]::UTF8)` y `WriteAllText`. |

## Historial de decisiones

1. **Qué es el overlay.** Cerrada: fuente de verdad, no sólo visual. Un cheatsheet que puede mentir se deja de mirar.
2. **Visibilidad.** Cerrada: sostener para ver. Se probó primero con panel fijo y toggle, y sostener ganó al usarlo.
3. **Quién dispara las acciones.** Cerrada: la app. Se pasó por una cadena `iCUE -> .bat -> .vbs -> dispatcher.ps1` que funcionaba, pero ataba el sistema a iCUE (imposible en la laptop) y producía un parpadeo de consola en cada pulsación.
4. **Cuántos perfiles.** Cerrada: dos, copiando el uso real de iCUE.
5. **RGB por perfil.** Descartado. Era un sustituto de no saber qué hacen las teclas, y el overlay lo resuelve mejor. Mantenerlo exigía el SDK de iCUE (que no libera de iCUE) u OpenRGB (que pelea con iCUE).
6. **Casillas clickeables.** Cerrada: sí. `WS_EX_TRANSPARENT` era herencia del diseño de panel permanente y ya no aplicaba.
7. **Posición del panel.** Abierta. Hoy es fija abajo a la izquierda. Falta decidir si se vuelve configurable.

## Pendiente

### Testing 1 (uso diario en la PC principal)

1. **Probar físicamente `Ctrl+Shift` + un número.** Es lo único de esta tanda que no se pudo verificar: con el juego abierto la entrada sintética no dispara atajos globales (ver trampas técnicas).
2. Sacar el icono de la bandeja del área desbordada: Windows 11 esconde los iconos nuevos detrás del chevron.

### Testing 2 (laptop)

3. `.\deploy.ps1 -SelfContained` y copiar `%LOCALAPPDATA%\Atajos` a la laptop.
4. Dos acciones no van a funcionar allá y van a quedar registradas en `atajos.log`: `start-localdrop.ps1` (necesita `PublicarLocalDrop` en el Escritorio) y `start-openclaw-gateway.ps1` (necesita `openclaw` instalado). Falta decidir si se resuelven con un perfil propio de esa máquina o si alcanza con que fallen y lo digan.

### Cuando moleste

5. Que la casilla del audio muestre hacia dónde va a cambiar, en vez de un nombre fijo. Requiere que la app consulte el dispositivo actual al abrir el panel.
6. El diálogo de LocalDrop roba el foco. Va contra el objetivo de invisibilidad.
7. `open-terminal-here.ps1` abre `cmd`. Windows Terminal está instalado (`wt.exe`) y sería una mejora, pero es una preferencia, no un defecto. Cambiarlo es una línea.
8. Posición del panel configurable en vez de fija abajo a la izquierda.

### Limpieza de C:\Scripts

Sólo quedan los `ScriptG*.{bat,vbs,ps1}`, y **los usa iCUE hoy**. Se borran cuando desasignes las teclas G. La app no lee nada de esa carpeta.

## Portabilidad

La forma buscada, ya casi lista:

```
Atajos\
  Atajos.exe         self-contained, sin .NET instalado en la maquina destino
  perfiles.json
  perfil-activo.txt  se crea solo
  actions\
```

Sin instalador, sin permisos de administrador, sin iCUE, sin AutoHotkey. Un acceso directo en `shell:startup` y listo. Nada de esto depende de tener teclas G, que es justamente lo que lo hace portable.

## Instalación y despliegue

```
.\deploy.ps1
```

Publica, cierra la instancia en ejecución, instala en `%LOCALAPPDATA%\Atajos`, crea el acceso directo de arranque automático y la vuelve a levantar.

| Opción | Para qué |
|--------|----------|
| `-SelfContained` | Empaqueta el runtime de .NET. Necesario para una máquina sin él. |
| `-NoStart` | Instala sin levantar la app |
| `-NoAutostart` | No crea el acceso directo en `shell:startup` |

**La app instalada y la carpeta de compilación tienen que estar separadas.** Mientras la app corre su `.exe` está bloqueado, así que cada `dotnet build` falla, y un `dotnet clean` te borraría la app en uso. Nos pasó cuatro veces antes de separarlas.

El despliegue **nunca pisa** `perfiles.json`, `perfil-activo.txt` ni los logs. Perder un `perfiles.json` editado por una instalación sería el bug más molesto posible en este proyecto.

Para desarrollar sin tocar la instalada:

```
dotnet build
bin\Debug\net10.0-windows\Atajos.exe
```

Ojo: las dos instancias no pueden convivir, hay guarda de instancia única.

## Robustez

| Tema | Cómo está resuelto |
|------|--------------------|
| Instancia única | Mutex `Local\Atajos.SingleInstance`. La segunda instancia sale en silencio, que es lo correcto cuando el acceso directo de arranque dispara con la app ya abierta. |
| Excepción en el hilo de UI | Se registra y se traga. Un utilitario de fondo que desaparece sin decir nada es peor que uno que se pierde una tecla: los atajos dejarían de andar sin explicación. Avisa con un globo desde la bandeja. |
| Excepción fatal en otro hilo | Se registra. No hay nada que recuperar, pero es la diferencia entre una pista y un misterio. |
| Registro de atajos fallido | Se anota en `atajos.log` con el código de error. Sin eso, un atajo que otra app ya tomó produce un panel cuyas teclas no hacen nada, indistinguible de una acción rota. |
| Tamaño del log | Rota a `atajos.log.1` pasados 256 KB. |
| Icono de bandeja | Es la única prueba visible de que la app está viva. Permite salir y abrir la carpeta de configuración y el registro. |

## Contexto relacionado

- `C:\Scripts` - sólo legado. Los `ScriptG*` que todavía usa iCUE, más restos de dos intentos anteriores. La app no lo lee.
- `C:\ScriptsHome` - otra carpeta de automatizaciones previa (`openclaw-gateway.cmd`, `shutdown-after.ps1`, `monitor-config-startup.ps1`). El proyecto ya no depende de ella.
- `C:\Users\garci\Desktop\Journal` - el vault de Obsidian de la nota diaria.
- `C:\Users\garci\Desktop\PublicarLocalDrop` - el ejecutable que levanta `start-localdrop.ps1`.
