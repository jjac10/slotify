---
name: Modern Booking System
colors:
  surface: '#f9f9ff'
  surface-dim: '#cfdaf2'
  surface-bright: '#f9f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f0f3ff'
  surface-container: '#e7eeff'
  surface-container-high: '#dee8ff'
  surface-container-highest: '#d8e3fb'
  on-surface: '#111c2d'
  on-surface-variant: '#4a4455'
  inverse-surface: '#263143'
  inverse-on-surface: '#ecf1ff'
  outline: '#7b7487'
  outline-variant: '#ccc3d8'
  surface-tint: '#732ee4'
  primary: '#630ed4'
  on-primary: '#ffffff'
  primary-container: '#7c3aed'
  on-primary-container: '#ede0ff'
  inverse-primary: '#d2bbff'
  secondary: '#00687a'
  on-secondary: '#ffffff'
  secondary-container: '#57dffe'
  on-secondary-container: '#006172'
  tertiary: '#4c4f51'
  on-tertiary: '#ffffff'
  tertiary-container: '#646769'
  on-tertiary-container: '#e4e6e8'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#eaddff'
  primary-fixed-dim: '#d2bbff'
  on-primary-fixed: '#25005a'
  on-primary-fixed-variant: '#5a00c6'
  secondary-fixed: '#acedff'
  secondary-fixed-dim: '#4cd7f6'
  on-secondary-fixed: '#001f26'
  on-secondary-fixed-variant: '#004e5c'
  tertiary-fixed: '#e0e3e5'
  tertiary-fixed-dim: '#c4c7c9'
  on-tertiary-fixed: '#191c1e'
  on-tertiary-fixed-variant: '#444749'
  background: '#f9f9ff'
  on-background: '#111c2d'
  surface-variant: '#d8e3fb'
typography:
  headline-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: '1.2'
    letterSpacing: -0.02em
  headline-lg-mobile:
    fontFamily: Plus Jakarta Sans
    fontSize: 24px
    fontWeight: '700'
    lineHeight: '1.2'
  headline-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.3'
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.6'
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: '1.4'
    letterSpacing: 0.01em
  button:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '600'
    lineHeight: '1'
    letterSpacing: 0.02em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  unit: 4px
  container-padding-mobile: 16px
  container-padding-desktop: 32px
  stack-sm: 8px
  stack-md: 16px
  stack-lg: 24px
  stack-xl: 32px
  gutter: 16px
---

## Brand & Style

The design system is anchored in **Precision & Vitality**. It aims to bridge the gap between a high-utility tool and an inviting local marketplace. The brand personality is professional yet energetic, designed to evoke a sense of reliability for service providers and effortless discovery for users.

The aesthetic follows a **Corporate Modern** approach with a strong focus on mobile-first ergonomics. It prioritizes clarity through generous whitespace and a purposeful use of high-chroma accents to guide the user through the booking funnel. The interface should feel "airy" but structured, ensuring that even dense schedules remain legible and easy to navigate.

## Colors

The color strategy uses high-contrast pairings to define interaction hierarchy.

- **Primary (Morado):** Reserved for the most critical actions—"Book Now" buttons, active navigation states, and primary brand touchpoints.
- **Secondary (Cyan):** Used for informational highlights, such as indicating available time slots, verified badges, or secondary filtering options.
- **Neutral (Gris Oscuro/Blanco):** A deep slate (#1E293B) is used for typography to ensure maximum legibility, while the background remains predominantly white (#FFFFFF) to maintain a clean, clinical feel.
- **Surface Tints:** Light grey washes (#F1F5F9) are used to differentiate card sections and background containers without introducing heavy borders.

## Typography

This design system utilizes a dual-font strategy. **Plus Jakarta Sans** is used for headlines to provide a soft, contemporary character that feels approachable for a lifestyle-focused app. **Inter** is used for all functional UI elements, body text, and labels due to its exceptional legibility at small sizes and its neutral, systematic appearance.

Typography is scaled to ensure a clear hierarchy:
- **Headlines** use tight line heights and bold weights to anchor page sections.
- **Body text** utilizes a generous 1.6 line height to prevent fatigue during service browsing.
- **Secondary text** is kept at 12px with slightly increased letter spacing for metadata (e.g., duration, distance).

## Layout & Spacing

The layout philosophy is built on a **Mobile-First Fluid Grid**. On mobile devices, a single-column layout is preferred with a minimum of 16px lateral margins. On tablet and desktop, the system expands to a 12-column grid.

Spacing is governed by an **8px linear scale**, favoring "Generous" padding to create a sense of luxury and ease. 
- Use **32px (stack-xl)** to separate major content blocks.
- Use **16px (stack-md)** for internal card padding and between related elements.
- Use **8px (stack-sm)** for tight groupings, such as an icon next to a text label.

## Elevation & Depth

To maintain the "Clean & Minimal" style, this design system avoids heavy shadows. Depth is communicated through:

1.  **Tonal Layering:** The primary background is White (#FFFFFF). Secondary sections (like a service description or footer) use a Light Slate (#F8FAFC) background to create separation without lines.
2.  **Soft Ambient Shadows:** Only high-level components like floating action buttons (FABs) or active modal sheets use a shadow. Shadows should be highly diffused: `box-shadow: 0 10px 25px -5px rgba(124, 58, 237, 0.1)`. Note the subtle purple tint in the shadow to keep the palette cohesive.
3.  **Low-Contrast Outlines:** Input fields and inactive cards use a 1px border in a very light grey (#E2E8F0).

## Shapes

The shape language is consistently **Rounded (Level 2)**. This 0.5rem (8px) base radius strikes a balance between the geometric rigor of a SaaS product and the softness of a consumer-facing app.

- **Standard Elements:** Buttons, input fields, and small cards use 8px corners.
- **Large Containers:** Service detail cards and modals use 16px (rounded-lg) for a more modern, mobile-app feel.
- **Avatars/Status:** Use full circles (pill-shaped) for user photos and availability indicators to differentiate them from interactive structural elements.

## Components

### Buttons
- **Primary:** Solid Morado (#7C3AED) with white text. High-emphasis for the "Finalize Booking" action.
- **Secondary:** Outline Morado or solid Cyan (#06B6D4) with white text for "Check Availability."
- **Ghost:** No background, Slate text, used for "Cancel" or "Back" actions.

### Cards
Service cards should feature a prominent image with a 16px corner radius. The content area below the image uses 16px padding. Use a subtle 1px border (#E2E8F0) rather than a shadow for the card container.

### Time Slots (Chips)
Selectable time slots are represented as Cyan-tinted chips. 
- **Default:** White background, Cyan border, Cyan text.
- **Selected:** Solid Cyan background, White text.
- **Unavailable:** Light grey background, struck-through text.

### Input Fields
Inputs are minimal: 8px corner radius, 12px horizontal padding, and a 1px border that turns Morado (#7C3AED) on focus. Labels should always be visible above the field in **label-md** typography.

### Progress Indicators
For multi-step booking (Service > Time > Payment), use a thin Morado progress bar at the very top of the viewport to maintain a sense of momentum without cluttering the UI.