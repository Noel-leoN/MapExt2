---
trigger: always_on
---

# Project Development Rules

## 0. Project Structure

- **MapExtPDX** (`d:/CS2.WorkSpace/CS2Mod/A.Mod/MapExt2/MapExtPDX`):
  - **Main Mod**: 核心 Mod 项目，包含所有系统实现和逻辑。
- **EconomyExt** (`d:/CS2.WorkSpace/CS2Mod/A.Mod/MapExt2/EconomyExt`):
  - **Sub-Mod**: 由 Main Mod 派生出的独立经济扩展子项目，僅用於原版地圖大小。主要邏輯代码与主项目重合，后续将采用链接形式整合。除非特别指定，不需要引入上下文。
- **_KnowledgeBase** (`d:/CS2.WorkSpace/CS2Mod/A.Mod/MapExt2/_KnowledgeBase`):
  - **Reference**: 游戏原程序集 (`Game.dll`) 的反编译代码精简整合，作为主要参考资料。
- **_ReferenceSolution** (`d:/CS2.WorkSpace/CS2Mod/A.Mod/MapExt2/_ReferenceSolution`):
  - **Playground**: 临时方案验证区，除非特别指定，不需要引入上下文。

## 1. Environment & Constraints

- **Base**: Cities: Skylines 2 Mod (Official ToolChain).
- **Tech**: Unity 2022.3.62f+ (DOTS/ECS), Burst (AOT), C# 9.0, Harmony 2.2.2.
- **Forbidden**: No BepInEx or HarmonyX.
- **Reference**: Use `_KnowledgeBase` (decompiled Game.dll) & [Modding Wiki](https://cities-skylines-2-modding.wiki/).
- **Compilation**: The main project file is `d:/CS2.WorkSpace/CS2Mod/A.Mod/MapExt2/MapExtPDX/MapExt2.csproj`. Always use this path for compilation.

## 2. Modding Strategies

- **System Replacement (Preferred)**: Disable vanilla system (`Enabled = false`) and register modded system in same Group. Use when coupling is low, logic needs full rewrite, or specifically for `CellMapSystem<T>` derivatives.
- **Harmony Patching**: Use Harmony 2.2.2 (Prefix/Postfix/Transpiler). Prefer Transpiler for hot paths, high coupling, or partial logic tweaks (e.g., Job struct replacement).
- **CellMapSystem<T> & Large Maps**:
  - **Mandatory Replacement**: Systems inheriting `CellMapSystem<T>` MUST use System Replacement (not Harmony Transpiler) to handle complex dependency chains and `OnCreate` issues.
  - **Memory Safety**: Replace `Allocator.Temp` with `Allocator.TempJob` for scratch buffers in `OnUpdate` to prevent overflow on large maps (>57km). Ensure `Dispose(Dependency)` is called.

## 3. Coding & Comments

- **Language**: Prefer Chinese for comments.
- **Format**:
  - `/// <summary>`: Classes, Structs, Methods.
  - `// === [Title] ===`: Primary functional blocks.
  - `// --- [Title] ---`: Secondary logic sections.
  - `//`: Critical inline explanations.
- **Structure**: Use `#region` for: Config, Constants & Fields, System Loop, Jobs, Helpers.

## 4. Interaction Guidelines

- **Builder Mode**: Builder模式下先给出方案，然后再执行。

## 4.1 Git Safety (CRITICAL)

- **NEVER** run `git checkout`, `git restore`, `git reset --hard`, or `git clean -fd` on any tracked or modified file without **explicit user confirmation**.
- **NEVER** run `git stash drop` or `git stash clear`.
- Before any destructive Git operation, **always** run `git status` and `git stash list` first, and present the result to the user.
- When the user asks to "revert" or "undo", clarify the exact scope (single file vs. entire repo) before executing.
- Prefer `git diff` to inspect changes before discarding anything.
- If unsure whether a file has uncommitted changes, **do not touch it** — ask the user.

## 5. Decompiled Code Refactoring (Entities 1.4+)

- **Scope**: ONLY apply when refactoring decompiled Unity ECS code files.
- **Cleanup**: Remove `InternalCompilerInterface`, `TypeHandle`, `[CompilerGenerated]`, and `[Preserve]`.
- **API Usage**: Use `SystemAPI` (e.g., `GetComponentLookup`) and `QueryBuilder`.
- **Naming**:
  - **Local Variables**: Rename `num`/`flag` in Job `Execute` methods to semantic names.
  - **Fields**: DO NOT rename class/struct fields (must match game data structure).
- **Structure**: `Constants` -> `Fields` -> `Lifecycle` -> `Jobs` (use `#region`).
- **Logic**: Preserve inheritance, business logic, and Job dependency chains. Remove redundant inline code.
- **Documentation**: Follow Section 3 rules (Chinese comments, standard XML tags).

## 6. Documentation Saving Convention

- **Location**: Save generated analysis/research documents to `docs/{TopicSubdir}/` under the workspace root (`d:/CS2.WorkSpace/CS2Mod/A.Mod/MapExt2/docs/`).
- **Subdirectory**: Create a topic-based subdirectory for each research area (e.g., `HouseholdLifecycle`, `EconomySystem`).
- **Naming**: Use descriptive filenames in English (e.g., `Household_Lifecycle_Analysis.md`).
- **Format**: Markdown with Mermaid diagrams, tables, and file links where appropriate.
- **Updates**: When extending an existing topic, update the existing document or add new files to the same subdirectory.
