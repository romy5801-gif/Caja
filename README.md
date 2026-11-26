# Cash Button

Aplicación pequeña para Windows 11 que envía un comando ESC/POS RAW a la cola de impresión **POS-80 (copy of 1)** para solicitar la apertura del cajón conectado al puerto CASH/DK de la impresora.

## Funciones

- Botón flotante, pequeño, sin bordes y siempre visible.
- Se puede arrastrar con el mouse.
- Al cerrar la ventana, se oculta en la bandeja del sistema.
- Doble clic en el icono de la bandeja para mostrarla.
- Menú para activar/desactivar el inicio con Windows y salir.
- Envía únicamente el comando de apertura; no genera un ticket de texto.

## Compilar en Windows 11

1. Instala el SDK de .NET 8.
2. Descarga o clona este repositorio.
3. Abre la carpeta `CashButton`.
4. Ejecuta `build.bat`.
5. El ejecutable se genera en `bin\Release\net8.0-windows\win-x64\publish\CashButton.exe`.

El comando de publicación genera una aplicación autocontenida para Windows x64.

## Configuración y prueba

La cola de Windows está configurada en el código como `POS-80 (copy of 1)`. Si el nombre de la impresora cambia, modifica la constante `PrinterName` en `Program.cs`.

El comando configurado es `ESC p 0 25 250`; selecciona el pin 2 del conector de cajón según la convención ESC/POS. La apertura real depende del cableado, del pin utilizado por el cajón y de la implementación ESC/POS del firmware. **No se ha validado todavía físicamente en la impresora POS-8330 concreta.** Si no abre, no pulses repetidamente: primero hay que verificar pin, cable y compatibilidad.

La aplicación no cambia el puerto USB002 ni la configuración del controlador. Envía el comando a la cola de Windows en modo RAW.

## Estado del proyecto

Este repositorio contiene el código fuente y el script de compilación; no incluye un ejecutable compilado ni firmado.
