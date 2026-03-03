# Plunder

A modular Terraria companion mod for the [TerrariaModder](https://github.com/terraria-modder) platform. Plunder provides a suite of cheat features, quality-of-life tools, and item management — all accessible through an in-game draggable panel or configurable keybinds.

> **Platform:** TerrariaModder (Harmony-based mod injection)
> **Framework:** .NET Framework 4.8
> **Author:** Fostot

> **Bundled MonoFont Version:** 1.0.0 — [MonoFont Repository](https://github.com/fostot/Monofont)

---

> [!IMPORTANT]
> **Optional: MonoFont for Better UI Text**
>
> Plunder supports **[MonoFont](https://github.com/fostot/Monofont)** — a crisp 8x16 monospace bitmap font that replaces Terraria's blurry variable-width font in all Plunder panels. Without it, Plunder falls back to Terraria's default font, which can cause misaligned text and UI layout issues.
>
> **To enable MonoFont:**
> 1. Download `Monofont.dll` from the [MonoFont releases](https://github.com/fostot/Monofont/releases)
> 2. Place it in your `Terraria/TerrariaModder/core/` folder (next to `TerrariaModder.Core.dll`)
> 3. Restart Terraria — MonoFont activates automatically
>
> **Plunder works without MonoFont installed** — all text will render using Terraria's built-in font as a fallback.

---

## Features

### Cheats — Survival

| Feature | Description |
|---------|-------------|
| **God Mode** | Full invincibility — HP stays at max, all incoming damage is blocked. |
| **Infinite Mana** | Mana stays at maximum at all times. |
| **Infinite Flight** | Wing and rocket boot flight time never runs out. |
| **Infinite Ammo** | Ammo is never consumed when shooting. |
| **Infinite Breath** | Breath meter stays full — you can never drown. |
| **No Knockback** | Player cannot be knocked back by enemies. |
| **No Fall Damage** | Prevents all fall damage regardless of height. |
| **Damage Multiplier** | Adjustable damage multiplier (0 = one-hit kill, 1 = normal, up to 20x). |
| **Minion Override** | Override max minion slots (0 = unlimited, 1-20 = custom cap). |
| **Run Speed Multiplier** | Boost player run speed (1x normal, up to 10x). |
| **Tool Range Multiplier** | Extend reach of pickaxes, axes, hammers, and block placement (0 = unlimited, up to 50x). |

### Cheats — World

| Feature | Description |
|---------|-------------|
| **Spawn Rate Multiplier** | Control enemy spawn rates (0 = no spawns, 1 = normal, up to 20x). |
| **No Tree Bombs** | Prevents trees from spawning lit bombs in For The Worthy / Zenith seed worlds. |
| **No Gravestones** | Prevents tombstones from spawning on death. |
| **No Death Drop** | Prevents item drops on death. |
| **Kill All Enemies** | Instantly kill all NPCs on the map. |
| **Clear Items / Projectiles** | Remove all dropped items or active projectiles from the world. |

### Cheats — Environment

| Feature | Description |
|---------|-------------|
| **Time Pause** | Freeze the day/night cycle. |
| **Set Time** | Jump to Dawn, Noon, Dusk, or Midnight. Fast-forward to next Dawn. |
| **Toggle Rain** | Turn rain on or off. |
| **Toggle Blood Moon** | Force a Blood Moon event. |
| **Toggle Eclipse** | Force a Solar Eclipse event. |

### Visual

| Feature | Description |
|---------|-------------|
| **Full Bright** | Removes all darkness — every tile is fully illuminated. |
| **Player Glow** | Your character emits light like a torch, illuminating the nearby area. |
| **Full Map Reveal** | Reveals the entire world map instantly, removing all fog of war. |

### Movement

| Feature | Description |
|---------|-------------|
| **Teleport to Cursor** | Press a keybind to instantly teleport to your mouse cursor's position. |
| **Map Click Teleport** | Right-click anywhere on the fullscreen world map to teleport directly to that location. |

### Fishing / Luck

| Feature | Description |
|---------|-------------|
| **Auto Fishing Buffs** | Automatically applies potion buffs when holding a fishing rod — individually toggle Fishing Potion, Sonar Potion, and Crate Potion. |
| **Fishing Power Multiplier** | Multiply your fishing power (1x normal, up to 10x). |
| **Legendary Crates Only** | Forces every fishing catch to be a Golden/Titanium Crate. |
| **Minimum Catch Rarity** | Skip catches below a chosen rarity tier (Blue, Green, Orange, LightRed, Pink). |

### Item Packs

Drop-in JSON item packs that spawn predefined sets of items into your inventory. Create, edit, import/export packs through the in-game Pack Manager. Supports a quantity multiplier for bulk spawning.

### UI

- **Draggable Panel** — Resizable, repositionable panel with tabbed sections (Cheats, Packs, Other, Config).
- **MonoFont Rendering** — Crisp 8x16 monospace bitmap font with automatic fallback to Terraria's default font.
- **Rich Tooltips** — Enhanced tooltip rendering with word-wrapping, rounded corners, and proper text measurement.
- **Split-Panel Layout** — Cheats and Packs tabs use a split-panel design with independent scrolling.
- **Mod Menu Integration** — Quick access button to open the TerrariaModder mod menu (F6).

---

## Default Keybinds

| Key | Action |
|-----|--------|
| `]` | Toggle Plunder panel |
| `Y` | Toggle Full Bright |
| `NumPad4` | Toggle Teleport to Cursor |
| `T` | Teleport to cursor (when enabled) |

Additional keybinds (God Mode, Map Teleport, Player Glow, Fishing Buffs) are unbound by default and can be configured through TerrariaModder's keybind settings.

---

## Installation

1. Install [TerrariaModder](https://github.com/terraria-modder) into your Terraria directory.
2. Copy `Plunder.dll` and `manifest.json` into:
   ```
   Terraria/TerrariaModder/mods/plunder/
   ```
3. *(Optional)* Install [MonoFont](https://github.com/fostot/Monofont) for improved UI text rendering — place `Monofont.dll` in `Terraria/TerrariaModder/core/`.
4. Launch Terraria via `TerrariaInjector.exe`.
5. Press `F6` to open the Mod Menu and enable Plunder, or press `]` in-game to open the panel directly.

## Building from Source

```bash
dotnet build Plunder.csproj -c Release
```

Output: `bin/Plunder.dll`

**Requirements:**
- .NET Framework 4.8 SDK
- `TerrariaModder.Core.dll` and `0Harmony.dll` (provided by TerrariaModder in `core/`)
- `Lux.dll` (shared UI widget library)
- `Monofont.dll` *(optional — build succeeds without it, text falls back to Terraria's font)*

---

## Configuration

All settings are persisted in TerrariaModder's config system and hot-reload when changed. The panel remembers its position and size across sessions. Every toggle can be controlled via the in-game panel, keybinds, or by editing the config directly.

---

## Architecture

Plunder is built as independent modules, each self-contained with its own Harmony patches and reflection caching:

```
Mod.cs              → Entry point, wires modules + config + keybinds
PlunderPanel.cs     → In-game UI (DraggablePanel + StackLayout + widgets)
PlunderConfig.cs    → Config bridge (reads/writes TerrariaModder config)
FullBright.cs       → Lighting override via Lighting.GetColor patch
PlayerGlow.cs       → Player light emission via Player.Update patch
MapReveal.cs        → Fog of war removal via WorldMap.Update
MapTeleport.cs      → Map click → world teleport via PlayerInput raw coords
TeleportToCursor.cs → Cursor teleport via screenPosition + mouse offset
FishingLuck.cs      → Fishing buff injection + power/crate/rarity overrides
Cheats.cs           → God mode, infinite resources, damage/speed/spawn control
EnvironmentControl  → Time, weather, and event control
WorldActions.cs     → Gravestones, death drops, kill/clear commands
ItemPackManager.cs  → JSON item pack loader + spawner
RichTooltip.cs      → Enhanced tooltip rendering with MonoFont support
```

All game interaction is done through reflection (no compile-time Terraria references), making the mod resilient to game updates.

---

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
