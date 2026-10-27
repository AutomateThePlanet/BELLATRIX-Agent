---
name: bellatrix-mobile-app-services
description: Reference skill for BELLATRIX mobile App services — Components, AppService, Device, Keyboard, Files, TouchActions, Create, and configuration (Android/iOS).
---

# BELLATRIX Mobile App Services Reference

The test's `App` object is the single entry point to everything a mobile test does besides touching an
element: creating components and page objects, controlling the app under test (background, relaunch,
install, activities, contexts), the device (orientation, lock, notifications), the keyboard, the device
file system, and multi-touch gestures.

| Concern | Explore it live with the CLI | Use it in generated code via |
|---------|------------------------------|------------------------------|
| App lifecycle | `background-app`, `close-app`, `launch-app`, `install-app`, `remove-app`, `is-app-installed` | the app service |
| Native ↔ WebView | `get-context`, `get-contexts`, `set-context` | the app service's context |
| Orientation | `get-orientation`, `set-orientation` | the device service |
| Gestures | `swipe`, `scroll-to-element` | touch actions / component scrolling |
| Screenshot | `take-screenshot --out <file>` | automatic on failure (screenshots plugin) |

Validate any app/device action with the CLI during scenario execution before relying on it in code.

---

{{> lang}}
