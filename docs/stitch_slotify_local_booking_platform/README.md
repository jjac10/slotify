# Prototipos de diseño — Google Stitch

Esta carpeta contiene los **prototipos de interfaz y el sistema de diseño** de Slotify,
generados con **[Google Stitch](https://stitch.withgoogle.com/)** (herramienta de diseño de UI
asistida por IA). Sirvieron de **referencia visual** para implementar el frontend real
(React + Tailwind); no forman parte de la aplicación desplegada.

## Contenido

| Carpeta | Qué es |
|---------|--------|
| `modern_booking_system/DESIGN.md` | **Sistema de diseño** completo: paleta, tipografía, espaciado, componentes |
| `slotify_landing_page/` | Mockup de la landing pública |
| `slotify_booking_flow/` | Mockup del flujo de reserva (servicio → hueco) |
| `slotify_confirmation/` | Mockup de la pantalla de confirmación |
| `slotify_owner_dashboard/` | Mockup del panel del dueño |
| `slotify_owner_settings/` | Mockup de la configuración del dueño |
| `slotify_logo/` | Exploración del logo |
| `animated_svg/` | Animación SVG de apoyo |

Cada pantalla incluye `screen.png` (imagen del mockup) y, cuando aplica, `code.html`
(HTML+CSS generado por Stitch).

## Cómo se usó

Los tokens del sistema de diseño (morado `#7C3AED`, cyan `#06B6D4`, tipografías
**Plus Jakarta Sans** + **Inter**, esquinas redondeadas, chips de huecos, etc.) se
trasladaron a la configuración de **Tailwind** del frontend, de modo que la app final
respeta la guía de diseño de estos prototipos.
