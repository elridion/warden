---
paths:
  - "src/**/*.cs"
---

# C# code style

Match the existing code; it follows Valheim's own conventions so it reads naturally next to the
game classes it extends.

## Naming

- Instance fields: `m_camelCase` (`m_radius`, `m_hull`). Public `m_` fields on components mirror
  vanilla components and are set when the prefab is built in `WardPieces` (e.g. `m_enabledEffect`,
  `m_tier`, `m_connectionPrefab`). The plugin's `_harmony` is the one exception; do not spread that style.
- Static and static readonly fields: `s_camelCase` (`s_instances`, `s_hazeMaterial`, `s_wardRuneGlow`,
  `s_candles`). The exception is the public static `ConfigEntry` fields on `WardenPlugin`, which are
  PascalCase (`BaseRadius`, `ShowHull`).
- Constants: `PascalCase` (`BaseAlpha`, `StackableTier`, `CandlePrefab`).
- Localization keys: `piece_spawnward*` for piece names/descriptions, `spawnward_*` for other UI strings.
- Types are `internal` by default. By convention, the components that implement vanilla interfaces
  (`SpawnWard`, `WardExtension`) are `public class`.

## Structure

- One namespace, `Warden`, as a block (`namespace Warden { ... }`), not the file-scoped
  `namespace Warden;` form.
- `using` directives at the top, `System*` first, then the rest alphabetically (`BepInEx`,
  `HarmonyLib`, `Jotunn.*`, `UnityEngine`).
- Four-space indentation, Allman braces, one statement per line, single-line guard clauses without
  braces (`if (!m_hull) return;`).
- Expression-bodied members for trivial getters and interface stubs (`public float GetHoverOffset() => m_hoverOffset;`).
- Prefer new tuning values as named constants near the top of the class with a short comment
  grouping them (as in `WardHull`). Existing code still has some literals inside methods; don't churn
  them unless you are working on that code.
- Prefer `UnityEngine.Object` truthiness (`if (!ward)`) over `== null` for Unity objects: destroyed
  objects compare as false. `?.` and `??` bypass that check, so do not use them on a Unity object
  that may have been destroyed; the existing uses are on fresh `GetComponent` results (null when the
  component is absent) and on plain C# objects, where they are fine.

## Comments

- Give new classes a `/// <summary>` saying what the class is for and the one non-obvious thing about it.
- Inline comments explain why: the game quirk being worked around, the reason for a number, what
  breaks if the line is removed. Do not narrate what the next line does.
- Keep comments in plain sentences with normal punctuation, as in the existing files.

## Logging and failure handling

- Log through `Jotunn.Logger` (`LogWarning` for skipped content or degraded features, `LogError` for a
  failed piece build). No `Debug.Log`.
- Do not leave diagnostic logging in committed code; remove it once a problem is understood.
- A missing vanilla prefab or resource should log a warning and skip that feature, not throw.
  Upgrade pieces are guarded this way (`RequirePrefab` plus the try/catch in `AddExtension`). The
  warden stone itself (`AddWard`) is not: if `guard_stone` were missing, `Register` would throw and the
  upgrades after it would not register. Guard any new piece like the upgrades.
