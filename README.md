# Plunder

A modular Terraria companion mod for the [TerrariaModder](https://github.com/terraria-modder) platform. Plunder provides a suite of cheat features, quality-of-life tools, and item management — all accessible through an in-game draggable panel or configurable keybinds.

> **Platform:** TerrariaModder (Harmony-based mod injection)
> **Framework:** .NET Framework 4.8
> **Author:** Fostot

---

## Features

### Visual

| Feature | Description |
|---------|-------------|
| **Full Bright** | Removes all darkness — every tile is fully illuminated. Great for exploring caves without torches. |
| **Player Glow** | Your character emits light like a torch, illuminating the nearby area as you move. |
| **Full Map Reveal** | Reveals the entire world map instantly, removing all fog of war. |

### Movement

| Feature | Description |
|---------|-------------|
| **Teleport to Cursor** | Press a keybind to instantly teleport to your mouse cursor's position in the world. |
| **Map Click Teleport** | Right-click anywhere on the fullscreen world map to teleport directly to that location. Works at any zoom level, any resolution, and with ultrawide/borderless setups. |

### OP Cheats

| Feature | Description |
|---------|-------------|
| **God Mode** | Full invincibility — HP stays at max, all incoming damage is blocked. |
| **Infinite Mana** | Mana stays at maximum at all times. |
| **Infinite Flight** | Wing and rocket boot flight time never runs out. |
| **Infinite Ammo** | Ammo is never consumed when shooting. |
| **Infinite Breath** | Breath meter stays full — you can never drown. |
| **No Knockback** | Player cannot be knocked back by enemies. |
| **No Fall Damage** | Prevents all fall damage regardless of height. |
| **No Tree Bombs** | Prevents trees from spawning lit bombs in For The Worthy / Zenith seed worlds. |
| **Damage Multiplier** | Adjustable damage multiplier (0 = one-hit kill, 1 = normal, up to 20x). |
| **Spawn Rate Multiplier** | Control enemy spawn rates (0 = no spawns, 1 = normal, up to 20x). |
| **Run Speed Multiplier** | Boost player run speed (1x normal, up to 10x). |
| **Minion Override** | Override max minion slots (0 = unlimited, 1-20 = custom cap). |

### Fishing

| Feature | Description |
|---------|-------------|
| **Auto Fishing Buffs** | Automatically applies potion buffs when holding a fishing rod — individually toggle Fishing Potion, Sonar Potion, and Crate Potion. |
| **Fishing Power Multiplier** | Multiply your fishing power (1x normal, up to 10x). |
| **Legendary Crates Only** | Forces every fishing catch to be a Golden/Titanium Crate. |
| **Minimum Catch Rarity** | Skip catches below a chosen rarity tier (Blue, Green, Orange, LightRed, Pink). |

### Item Packs

Drop-in JSON item packs that spawn predefined sets of items into your inventory. Place `.json` pack files in the mod's `packs/` directory. Supports a quantity multiplier for bulk spawning.

### UI

- **Draggable Panel** — Resizable, repositionable panel with tabbed sections for each feature category.
- **Rich Tooltips** — Enhanced tooltip rendering with word-wrapping, rounded corners, and proper text measurement.
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
3. Launch Terraria via `TerrariaInjector.exe`.
4. Press `F6` to open the Mod Menu and enable Plunder, or press `]` in-game to open the panel directly.

## Building from Source

```bash
dotnet build Plunder.csproj -c Release
```

Output: `bin/Plunder.dll`

**Requirements:**
- .NET Framework 4.8 SDK
- `TerrariaModder.Core.dll` and `0Harmony.dll` (provided by TerrariaModder in `core/`)

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
OpCheats.cs         → God mode, infinite resources, damage/speed/spawn control
ItemPackManager.cs  → JSON item pack loader + spawner
RichTooltip.cs      → Enhanced tooltip rendering
```

All game interaction is done through reflection (no compile-time Terraria references), making the mod resilient to game updates.
