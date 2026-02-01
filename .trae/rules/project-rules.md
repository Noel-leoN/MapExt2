# Project Development Rules

## 1. Environment & Constraints
- **Base**: Cities: Skylines 2 Mod (Official ToolChain).
- **Tech**: Unity 2022.3.62f+ (DOTS/ECS), Burst (AOT), C# 9.0, Harmony 2.2.2.
- **Forbidden**: No BepInEx or HarmonyX.
- **Reference**: Use `_KnowledgeBase` (decompiled Game.dll) & [Modding Wiki](https://cities-skylines-2-modding.wiki/).

## 2. Modding Strategies
- **System Replacement (Preferred)**: Disable vanilla system (`Enabled = false`) and register modded system in same Group. Use when coupling is low.
- **Harmony Patching**: Use Harmony 2.2.2 (Prefix/Postfix/Transpiler). Prefer Transpiler for hot paths or high coupling.

## 3. Coding & Comments
- **Language**: Prefer Chinese for comments.
- **Format**:
  - `/// <summary>`: Classes, Structs, Methods.
  - `// === [Title] ===`: Primary functional blocks.
  - `// --- [Title] ---`: Secondary logic sections.
  - `//`: Critical inline explanations.
- **Structure**: Use `#region` for: Config, Constants & Fields, System Loop, Jobs, Helpers.
