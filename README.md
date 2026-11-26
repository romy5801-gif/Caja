# Cash Button

Aplicación pequeña para Windows 11 que envía un comando ESC/POS RAW a una impresora instalada en Windows para solicitar la apertura del cajón conectado al puerto CASH/DK.

## Funciones

- Botón flotante, pequeño, sin bordes y siempre visible.
- Se puede arrastrar con el mouse.
- Detecta las colas de impresión instaladas en Windows y muestra el puerto asociado cuando Windows lo proporciona.
- Permite seleccionar la impresora que controlará el cajón.
- Permite introducir manualmente el valor del parámetro de pin `m` (0 a 255).
- Guarda la impresora y el valor del pin en `%AppData%\CashButton\settings.json`.
- Al cerrar la ventana, se oculta en la bandeja del sistema.
- Doble clic en el icono de la bandeja para mostrarla.
- Menú de bandeja para abrir la configuración, activar/desactivar el inicio con Windows y salir.
- Envía únicamente el comando de apertura; no genera un ticket de texto.

## Compilar en Windows 11

1. Instala el SDK de .NET 8.
2. Descarga o clona este repositorio.
3. Abre la carpeta `CashButton`.
4. Ejecuta `build.bat`.
5. El ejecutable se genera en `bin\Release\net8.0-windows\win-x64\publish\CashButton.exe`.

El comando de publicación genera una aplicación autocontenida para Windows x64.

## Configuración y compatibilidad

El comando enviado sigue la estructura ESC/POS `ESC p m t1 t2`. El valor `m` se introduce en la ventana de configuración; los valores habituales son 0 para pin 2 y 1 para pin 5. Otros valores pueden no estar soportados por el firmware de la impresora. Los tiempos `t1` y `t2` permanecen en 25 y 250.

La aplicación envía el comando a la cola de Windows seleccionada en modo RAW. Detectar una cola y su puerto no garantiza que el puerto sea compatible con la apertura del cajón. Tampoco cambia el puerto USB ni la configuración del controlador.

**La apertura física aún debe probarse en la POS-8330 y el cajón concretos.** Antes de probar, verifica el cableado y el pin requerido por el cajón. No envíes comandos repetidamente si no abre.

## Estado del proyecto

Este repositorio contiene el código fuente y el script de compilación. El ejecutable debe generarse localmente con `build.bat`.
