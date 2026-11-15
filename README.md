# Aventura Mundo Abierto (Unity)

Prototipo original de aventura móvil para Android. Incluye isla procedural low-poly, jugador en tercera persona, controles táctiles (stick + cámara), recolección de cristales, HUD y ciclo día/noche. El contenido se genera con primitivas de Unity para evitar assets externos.

## Abrir en Unity

Usa Unity **6000.0.32f1** (o una versión 6 compatible), abre esta carpeta como proyecto y abre `Assets/Scenes/Main.unity`. Si la escena no estuviera cargada, usa `Tools > Aventura > Crear escena de juego` para generarla.

## Android

En Unity Hub instala Android Build Support, SDK/NDK y OpenJDK para la versión del editor. En `File > Build Profiles` elige Android, activa ARM64 y crea el APK. El proyecto no incluye una licencia ni binarios de Unity.

## Build automatizado

El workflow `Android APK` se ejecuta manualmente (Actions > Android APK > Run workflow). Configura en Settings > Secrets and variables > Actions estos secretos: `UNITY_LICENSE` (archivo de licencia serializado de Unity), `UNITY_EMAIL` y `UNITY_PASSWORD` (cuenta con derecho a usar Unity en CI). El build publica `AventuraMundoAbierto-Android` como artifact al finalizar. La disponibilidad de Unity Personal en CI está sujeta a los términos/licencia de Unity.

## Controles

Móvil: stick izquierdo para caminar, arrastrar el lado derecho para girar la cámara, botón para saltar y botón de acción al acercarse a un cristal. Editor: WASD, mouse y espacio.
