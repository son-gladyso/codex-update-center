# Codex Update Center

[简体中文](../../README.md) · [English](../README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português (Brasil)](README.pt-BR.md)

### Menos complicaciones al actualizar Codex en Windows.

**Encuentra tu instalación, prepara una actualización oficial verificada y comprueba qué se instaló realmente, desde una sola ventana.** Para quienes no quieren buscar repetidamente carpetas de instalación, volver a descargar paquetes grandes o investigar por qué una actualización no funciona.

[![Windows build](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml)

[Descargar la versión preliminar para Windows](https://github.com/son-gladyso/codex-update-center/releases) · [Informar de un problema](https://github.com/son-gladyso/codex-update-center/issues/new/choose)

Windows x64 · ZIP autónomo con .NET incluido · MIT · Proyecto comunitario independiente, sin afiliación con OpenAI

![Selección de instalación, verificación del candidato, coste de red y confirmación de instalación](../images/update-center.png)

*Aplicación real con datos de ejemplo sintéticos. La interfaz está actualmente en chino; esta imagen no demuestra una actualización de una instalación real de un usuario.*

## Una ventana para las complicaciones de actualizar

| Problema | Cómo lo aborda la herramienta |
| --- | --- |
| No sé si debo actualizar la aplicación de escritorio o la CLI | Detecta instalaciones oficiales MSIX del usuario actual y prefijos npm estándar de la CLI. Muestra cada instalación por separado y actualiza únicamente el destino seleccionado. |
| Una interrupción me obliga a empezar la descarga de nuevo | Conserva puntos de reanudación. Los componentes nativos grandes de la CLI se descargan en rangos reanudables de 1 MiB; los paquetes oficiales en caché se reutilizan tras verificarlos otra vez. |
| El paquete está listo, pero sigo trabajando | Separa preparación e instalación. Antes de instalar pide confirmación y comprueba los procesos activos; nunca fuerza el cierre de Codex. |
| Indica éxito, pero la CLI no funciona | Vuelve a detectar la versión instalada y ejecuta el comando de versión de la CLI; solo informa de éxito cuando los resultados coinciden. |
| No quiero agotar los datos de mi conexión móvil | Bloquea por defecto las descargas grandes en redes de pago por uso o coste desconocido. Puedes autorizar explícitamente la descarga actual; vuelve a comprobar el coste antes de instalar con npm. |
| Queda poco espacio en C | Comprueba el presupuesto de espacio y el margen de seguridad. Prefiere C solo si hay reservas suficientes; de lo contrario considera discos internos fijos aptos y muestra la ubicación de la caché. |

**Verifica las firmas y los hashes del contenido de los paquetes oficiales antes de instalar.** No requiere API key, no lee tokens de cuenta ni bases de datos de chats, no recopila telemetría y no reemplaza la configuración de tu lanzador.

## Empieza en cuatro pasos

1. Descarga `CodexUpdateCenter-*-win-x64.zip` desde [Releases](https://github.com/son-gladyso/codex-update-center/releases) y comprueba `SHA256SUMS.txt`.
2. Extrae el archivo en una carpeta con permisos de escritura y ejecuta `CodexUpdateCenter.exe`. El entorno de ejecución .NET está incluido.
3. Selecciona una instalación y prepara su actualización. Si solo hay una instalación compatible, se selecciona y prepara automáticamente por defecto; puedes desactivar esta opción al inicio.
4. Espera a que se verifique el candidato, guarda tu trabajo y cierra normalmente esa instalación de Codex. Confirma la instalación y revisa la versión detectada después.

Puedes añadir directorios npm personalizados si no se detectan. Si todavía no tienes Codex, la ventana ofrece instrucciones oficiales de instalación.

> Esta es una **versión preliminar comunitaria**, sin firma Authenticode comercial. No desactives la seguridad de Windows para ejecutarla. Aún no se han verificado completamente la matriz de despliegue en Windows limpio, las pruebas físicas de accesibilidad y algunos casos de red y proxy. Consulta el [registro de validación](../VALIDATION.md).

## Instalaciones compatibles

| Instalación | Compatibilidad |
| --- | --- |
| Aplicación de escritorio `OpenAI.Codex` x64 del usuario actual, con firma oficial de Store | Detecta y verifica el MSIX oficial y actualiza mediante los servicios de despliegue de Windows. El despliegue real en Windows todavía requiere una aceptación completa. |
| Instalación global estable de `@openai/codex` mediante Node.js/npm estándar | Detecta y verifica el paquete principal y los componentes nativos de Windows; actualiza tras confirmación. Se ha probado una actualización real en un directorio aislado. |
| Varios directorios npm globales o escritorio y CLI coexistentes | Los muestra por separado y actualiza uno cada vez; permite añadir prefijos npm manualmente. |
| Copias portátiles, lanzadores personalizados de múltiples instancias, WSL, ARM64 y estructuras enlazadas de gestores de versiones de Node | No toma su control automáticamente; utiliza la herramienta de instalación original. |

La aplicación de escritorio y la CLI se mantienen por separado. No introduce una CLI independiente dentro de la aplicación. Mover la caché de descarga a otro disco no modifica la ubicación de instalación de escritorio gestionada por Windows.

## Resultados claros cuando algo falla

- Conserva los puntos de descarga al pausar o cerrar la ventana. No reanuda automáticamente una pausa explícita del usuario.
- Si la instalación falla, se interrumpe o no se puede confirmar su versión, pide revisar el estado. **Que el instalador haya terminado no basta para declarar éxito.**
- La instalación npm no es atómica: un fallo puede requerir reparación con npm. No se garantiza una reversión automática.
- No fuerza el cierre de Codex, no instala un candidato más antiguo, no migra cuentas ni chats y no inicia un servicio persistente en segundo plano.
- Utiliza el proxy efectivo del sistema y mantiene la validación TLS. No controla nodos VPN ni promete velocidad máxima en todas las redes. Las solicitudes del índice npm aún pueden fallar con algunos proxies; conserva la caché y los registros del fallo.

Consulta [soporte y resolución de problemas](../SUPPORT.md). El estado se guarda en `%LOCALAPPDATA%\CodexUpdateCenter\Public`; las ubicaciones de caché aparecen en la vista de tareas. Eliminar este actualizador no desinstala Codex. Revisa los archivos de estado y caché antes de limpiarlos.

## Qué se ha validado

- **81 pruebas del programa**: cambios de destino, firmas no válidas, reanudación de descargas, bloqueos de concurrencia, pausa y controles del coste de red al instalar.
- **10 comprobaciones de herramientas de publicación**: manipulación de paquetes, omisión de archivos ocultos, rutas fuera de los límites y fronteras de privacidad del código fuente.
- Actualización real de **CLI 0.161.0 → 0.162.1** en un directorio aislado nuevo, ejecutando la nueva CLI para verificar su versión. Los manifiestos de instalaciones existentes permanecieron iguales.
- El paquete oficial de escritorio superó la validación de firma de Windows y de todos los hashes de bloques. También se verificaron la compilación independiente del paquete fuente y la extracción e inicio del paquete ejecutable.

La actualización real reutilizó paquetes oficiales reverificados y metadatos npm en caché. **No equivale a una prueba de red con caché vacía.** Verificar firma y hashes no demuestra un despliegue real de Windows. Consulta las últimas ejecuciones en [GitHub Actions](https://github.com/son-gladyso/codex-update-center/actions) y los alcances y pendientes en [VALIDATION](../VALIDATION.md).

## Empieza inspeccionando, sin instalar

Para decidir si encaja en tu entorno, extrae el programa y revisa las instalaciones detectadas y sus versiones actuales. No necesitas confirmar una instalación inmediatamente. La preparación puede usar la red y descargar archivos; puedes desactivar la preparación automática de una única instalación compatible.

- Para ejecutar: Windows x64 y una carpeta de extracción con escritura. .NET incluido; no hace falta API key.
- Para comprobar y preparar: acceso a fuentes oficiales, sujeto al proxy, controles de coste de red y reservas de disco.
- Para instalar: guarda el trabajo, sal normalmente del Codex seleccionado, vuelve a comprobar el destino y confirma solo cuando estés preparado.

**El primer éxito consiste en:** identificar el tipo, ubicación y versión de la instalación seleccionada y comprender su estado de verificación o bloqueo. Una actualización solo es exitosa tras las comprobaciones posteriores necesarias.

## Preguntas frecuentes

**¿La aplicación de escritorio y la CLI son una sola instalación?** No. Se detectan y actualizan por separado; una versión de CLI no prueba el despliegue del escritorio.

**¿Por qué no aparece mi copia portátil o configuración personalizada de varias instancias?** Está fuera de la gestión automática. Usa su herramienta original en vez de hacerla pasar por una instalación compatible.

**¿Por qué se bloquea la instalación después de descargar?** Todavía necesita verificar el candidato, confirmar el destino y satisfacer los controles aplicables de procesos activos y coste de red. Terminar la descarga no significa terminar la instalación.

**¿Por qué se bloquea la descarga si se desconoce el coste?** Es la protección de coste predeterminada. Revisa la red y autoriza explícitamente esa descarga si corresponde; no debilites TLS ni la verificación de firmas.

**¿Un fallo restaura automáticamente la versión anterior?** No se garantiza reversión. npm no instala de forma atómica. Examina el resultado y la [guía de soporte](../SUPPORT.md) en lugar de confirmar repetidamente.

**¿Qué hago si Windows avisa sobre esta herramienta?** La versión preliminar no tiene firma Authenticode comercial. Verifica el origen y las sumas de comprobación, no desactives la seguridad y no ejecutes el programa si no puedes establecer su fiabilidad.

### Localiza las responsabilidades antes de modificar

| Área | Entrada del código | Explica en el PR |
| --- | --- | --- |
| Detección y selección del destino | `src/UpdateCenter.Core/Discovery.cs` | Cómo se evitan destinos incorrectos y se tratan las estructuras no compatibles. |
| Red oficial y verificación | `OfficialNetwork.cs`, `Verification.cs` | Casos observables de fallo, reanudación y rechazo, sin debilitar la confianza. |
| Flujo de actualización y persistencia | `UpdateService.cs`, `Storage.cs` | Pausa, interrupción, estado tras reinicio y verificación posterior a la instalación. |
| Interfaz y mensajes | `src/UpdateCenter.App/MainWindow.xaml` y su código asociado | Cobertura de regresión de acciones repetidas, transiciones de estado y errores. |
| Compilación y publicación | `scripts/`, `docs/RELEASING.md` | Resultados reales de auditoría, pruebas y comprobaciones de manifiestos. |

Los nombres cortos de archivos Core están bajo `src/UpdateCenter.Core/`. Consulta la [arquitectura](../ARCHITECTURE.md); una respuesta de éxito simulada no demuestra un despliegue nativo de Windows.

## Desarrollo y colaboración

Compila en Windows x64 con PowerShell 7 y el SDK de `global.json`:

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

Los controles de publicación auditan el código, restauran dependencias bloqueadas, ejecutan pruebas, generan paquetes autónomos y comprueban manifiestos por archivo. El directorio de salida debe ser nuevo; las pruebas normales no instalan ni modifican Codex. La CI de Windows y la aceptación nativa aislada mantienen separados los distintos alcances de evidencia.

[Arquitectura](../ARCHITECTURE.md) · [Contribuciones](../../CONTRIBUTING.md) · [Publicación](../RELEASING.md) · [Historial de cambios](../../CHANGELOG.md) · [Seguridad](../../SECURITY.md)

La información más útil incluye el tipo de instalación, la versión de Windows, el paso que te bloqueó y el resultado esperado. Usa las [plantillas de problemas o sugerencias](https://github.com/son-gladyso/codex-update-center/issues/new/choose). Nunca adjuntes credenciales, chats ni registros de cuenta sin depurar. Son bienvenidas las correcciones pequeñas y reproducibles y los mensajes de fallo más claros.

### Por dónde empezar a colaborar

- **Documentación y mensajes:** convierte un fallo observado en pasos reproducibles o aclara tipos de instalación, ubicaciones de caché y mensajes de error.
- **Pruebas de regresión:** cubre cambios de destino, pausa y reanudación o fallos de verificación de paquetes, explicando el resultado observable.
- **Validación en Windows:** registra sistema, instalación, versiones y alcance exactos; separa pruebas sintéticas, operaciones nativas y verificación en línea.

Lee la [guía de contribución](../../CONTRIBUTING.md) y el [registro de validación](../VALIDATION.md), y elige un problema concreto. Usa un entorno aislado para pruebas con instalaciones reales; no tomes tu instalación habitual de trabajo como destino predeterminado. Antes de proponer un nuevo tipo como compatible, debate las reglas de detección, el mecanismo de actualización y el tratamiento de fallos.

La página muestra una captura de la aplicación real con datos de ejemplo, no una grabación continua desde la preparación hasta la instalación terminada. Se agradecen mejoras reproducibles de la guía, pero los estados de demostración no deben presentarse como actualizaciones reales.

MIT cubre el código de esta herramienta. Los paquetes publicados no incluyen Codex; se descarga en tiempo de ejecución desde fuentes oficiales y conserva sus propios términos y licencia. Consulta [NOTICE](../../NOTICE.md).
