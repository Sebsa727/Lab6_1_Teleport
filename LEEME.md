# Lab 6.1 – Meta XR SDK Teleport (Subestación Andahuasi)

Proyecto armado a partir de Hito1-Quemaduras (Unity 2022.3.62f3, Meta XR SDK 201), sin sus assets propios,
con el paquete `Modelo_Planta_Andahuasi.unitypackage` ya integrado en `Assets/`.

## Cómo usarlo
1. Unity Hub → **Add project from disk** → selecciona la carpeta `Lab6_1_Teleport`.
   Ábrelo con **2022.3.62f3**. Recomendado: en el Hub, *Add command line arguments* → `-buildTarget Android`
   para que importe directo para Quest (así no reimporta 7 GB de texturas después).
2. La primera apertura tarda bastante (importa un OBJ de 1.2 GB y texturas grandes) y descarga el Meta XR SDK.
3. Al terminar aparece un diálogo **"Lab 6.1 - Teleport"** → *Sí, configurar*.
   (O en cualquier momento: menú **Lab 6.1 → 1. Configurar escenas**.)
4. Si no abriste con Android: **Lab 6.1 → 2. Cambiar plataforma a Android**.
5. Conecta el Quest → **File → Build And Run**.

## Qué hace el script (`Assets/Lab61/Editor/Lab61Setup.cs`)
| Paso de la guía | Resultado |
|---|---|
| Prevenir caída al vacío | GameObject `Floor` (Plane + Mesh Collider) bajo todo el modelo |
| Agregar colisionadores | Mesh Collider en los pisos transitables (losas planas a nivel de suelo: Solid1, Solid1:4, Solid1:231, …) |
| OVRPlayerController (opcional) | Escena `Escena_Andahuasi_Joystick` (en Build Settings, desactivada) |
| OVRCameraRigInteraction | Escena `Escena_Andahuasi`, spawn sobre Solid1:231 |
| TeleportHotspot | Hasta 12 hotspots en `Lab61_TeleportHotspots` |
| Teleport Interactable + Collider Surface + Reticle Data Teleport | En Solid1:231 y los demás pisos |
| Tracking Origin Type | Floor Level |

La lista de pisos detectados sale en la Consola (`[Lab 6.1] Pisos transitables ...`).
Si quieres colisión con todo (paredes, equipos): **Lab 6.1 → 3. Agregar Mesh Collider a TODO el modelo** (pesado).
El script se puede volver a ejecutar sin duplicar nada.

## Si lo clonaste desde Git
El repo NO trae los archivos pesados del modelo (límite de 100 MB de GitHub):
`Assets/Modelo 3D/Modelos/Planta-Andahuasi-Electrico.obj` y las imágenes de `Assets/Modelo 3D/Texturas-adicionales/`
(sus `.meta` sí están, para no perder las referencias). Tráelos de una de estas dos formas:

- **A (recomendada):** antes de abrir Unity, copia esos archivos (o toda la carpeta `Modelo 3D`) desde la PC original / USB
  a `Assets/Modelo 3D/`, reemplazando.
- **B:** abre el proyecto y luego **Assets → Import Package → Custom Package…** → `Modelo_Planta_Andahuasi.unitypackage`
  → Import (todo). Después ejecuta **Lab 6.1 → 1. Configurar escenas**.
