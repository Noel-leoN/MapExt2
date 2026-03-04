---
description: Automatically refactor decompiled Unity ECS System code
---

# Refactor Decompiled System

This workflow automates the messy parts of cleaning up decompiled `Unity ECS System` C# code output by ILSpy or dotPeek, following the MapExt2 modding standards.

## Prerequisites
- Target `.cs` file containing a decompiled Unity ECS System (e.g. `GameSystemBase` or `SystemBase`).

## Steps

1. **Ask for Target**
   Confirm with the user which `.cs` file(s) they want to refactor.

2. **Automated Cleanup Script**
   Generate or run a python script to bulk-replace compiler-generated artifacts. The script should:
   - Strip `[CompilerGenerated]` and `[Preserve]` attributes.
   - Strip `private struct TypeHandle { ... }` alongside its `__TypeHandle` field.
   - Strip `__AssignQueries` method, `OnCreateForCompiler`, and the parameterless class constructor.
   - Strip variable `__query...` fields. 
   - Replace complex ILSpy method calls:
     - `InternalCompilerInterface.GetComponentLookup(...)` -> `SystemAPI.GetComponentLookup<T>(isReadOnly: ...)`
     - `InternalCompilerInterface.GetBufferLookup(...)` -> `SystemAPI.GetBufferLookup<T>(isReadOnly: ...)`
     - `InternalCompilerInterface.GetEntityTypeHandle(...)` -> `SystemAPI.GetEntityTypeHandle()`
     - Remove `void IJobChunk.Execute()` implementation if it merely redirects to `Execute()` (i.e. decompiled redundancy).
   
   *Tip:* You can use the regex logic or the python script previously written during the `HouseholdFindPropertySystem` and `RentAdjustSystem` refactoring context.

3. **Structural Formatting & Stylization**
   Use standard C# regions to organize the file contents strictly in this order:
   - `#region Constants` (Include `public override int GetUpdateInterval` defined as an expression-bodied method here).
   - `#region Fields`
   - `#region Lifecycle` (including `OnCreate`, `OnUpdate`, `OnDestroy`)
   - `#region Jobs`
   - `#region Helpers` (for Structs and common utility functions)
   
   Field Styling Rules:
   - Strip empty lines separating variable definitions so they are cleanly clumped.
   - Combine Component tags to the single line defining the variable (e.g. `[ReadOnly] public ComponentLookup<T> ...` instead of splitting across multiple lines).

4. **Semantic Renaming & Logic Review**
   - Look inside `Execute` loops or custom methods.
   - Decompiled code often uses generic names like `num`, `flag`, `num2`, `int5`. 
   - Trace the logic and rename them to semantic names (e.g., `count`, `freeCapacity`, `isWorkplaceNull`, `maxPropertiesCount`).
   - If `GetSingleton<T>` was extracted from an `__query_xxx` that was removed, explicitly replace it with `SystemAPI.GetSingleton<T>()` or manually construct the `m_FeeParameterQuery` in `OnCreate` using `base.GetEntityQuery(ComponentType.ReadOnly<T>());`.

5. **Comments**
   - Ensure the structure is clear.
   - If any core logic is modified, add Chinese comments using `// === [Title] ===` or `// --- [Title] ---` structure.

6. **Verify**
   - Run a dry compilation check or ask the user to verify in Unity/Rider to ensure no NREs or syntax errors exist.
