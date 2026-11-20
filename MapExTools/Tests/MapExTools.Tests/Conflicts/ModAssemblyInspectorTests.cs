using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Tests.Conflicts;

public sealed class ModAssemblyInspectorTests
{
    [Fact]
    public void EnabledTracking_DetectsChained_Field_LocalBranch_AndVariableBool_IncludingNestedHost()
    {
        byte[] bytes = SyntheticModBuilder.BuildModAssembly("EnabledPositiveMod", new Version(1, 0, 0), (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType("Sample.PositiveSystem", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
            FieldBuilder instField = tb.DefineField("_rentSys", env.RentAdjustSystemType, FieldAttributes.Private);
            FieldBuilder staticField = tb.DefineField("s_navActions", env.CarNavActionsType, FieldAttributes.Private | FieldAttributes.Static);

            // 1. 链式直调：world.GetOrCreateSystemManaged<LandValueSystem>().Enabled = false
            MethodBuilder mChain = tb.DefineMethod("DisableChained", MethodAttributes.Public, typeof(void), [env.WorldType]);
            ILGenerator il1 = mChain.GetILGenerator();
            il1.Emit(OpCodes.Ldarg_1);
            il1.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.LandValueSystemType));
            il1.Emit(OpCodes.Ldc_I4_0);
            il1.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
            il1.Emit(OpCodes.Ret);

            // 2. 实例字段 ldfld + 静态字段 ldsfld + 非常量布尔形参 enable (.Enabled = enable)
            MethodBuilder mField = tb.DefineMethod("ToggleViaFields", MethodAttributes.Public, typeof(void), [typeof(bool)]);
            ILGenerator il2 = mField.GetILGenerator();
            il2.Emit(OpCodes.Ldarg_0);
            il2.Emit(OpCodes.Ldfld, instField);
            il2.Emit(OpCodes.Ldarg_1); // 非常量布尔实参！
            il2.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);

            il2.Emit(OpCodes.Ldsfld, staticField);
            il2.Emit(OpCodes.Ldarg_1);
            il2.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
            il2.Emit(OpCodes.Ret);

            // 3. LocalVarSig 局部变量判空中转：var s = world.GetExistingSystemManaged<HouseholdFindPropertySystem>(); if (s != null) s.Enabled = flag;
            MethodBuilder mLocal = tb.DefineMethod("ToggleViaLocalAfterNullCheck", MethodAttributes.Public, typeof(void), [env.WorldType, typeof(bool)]);
            ILGenerator il3 = mLocal.GetILGenerator();
            LocalBuilder locSys = il3.DeclareLocal(env.HouseholdFindPropertySystemType);
            Label endLabel = il3.DefineLabel();
            il3.Emit(OpCodes.Ldarg_1);
            il3.Emit(OpCodes.Callvirt, env.GetExistingGeneric.MakeGenericMethod(env.HouseholdFindPropertySystemType));
            il3.Emit(OpCodes.Stloc, locSys);
            il3.Emit(OpCodes.Ldloc, locSys);
            il3.Emit(OpCodes.Brfalse_S, endLabel);
            il3.Emit(OpCodes.Ldloc, locSys);
            il3.Emit(OpCodes.Ldarg_2);
            il3.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
            il3.MarkLabel(endLabel);
            il3.Emit(OpCodes.Ret);

            tb.CreateType();
        });

        AssemblyInspectionResult? res = ModAssemblyInspector.InspectBytes(bytes);
        Assert.NotNull(res);

        Assert.Contains(res.EnabledWrites, w => w.TargetSystemTypeName == "Game.Simulation.LandValueSystem");
        Assert.Contains(res.EnabledWrites, w => w.TargetSystemTypeName == "Game.Simulation.RentAdjustSystem");
        Assert.Contains(res.EnabledWrites, w => w.TargetSystemTypeName == "Game.Simulation.CarNavigationSystem+Actions");
        Assert.Contains(res.EnabledWrites, w => w.TargetSystemTypeName == "Game.Simulation.HouseholdFindPropertySystem");
    }

    [Fact]
    public void EnabledTracking_IgnoresDisablingSelf_ReadOnlyGetData_AndAuxToolHosts()
    {
        // 复刻 CustomWaterToolSystem.OnCreate 形态：同方法内获取受保护系统（甚至 LandValueSystem）与 WaterSystem/TerrainSystem 引用，
        // 只读调用 GetData(true)，最后调用 this.Enabled = false 停用自身，以及停用常态禁用的工具宿主 AreaToolSystem -> 必须 0 误报！
        byte[] bytes = SyntheticModBuilder.BuildModAssembly("BenignToolMod", new Version(1, 0, 0), (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType("Benign.CustomToolSystem", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
            FieldBuilder lvField = tb.DefineField("_landValue", env.LandValueSystemType, FieldAttributes.Private);
            FieldBuilder waterField = tb.DefineField("_water", env.WaterSystemType, FieldAttributes.Private);
            FieldBuilder terrainField = tb.DefineField("_terrain", env.TerrainSystemType, FieldAttributes.Private);

            MethodBuilder onCreate = tb.DefineMethod("OnCreate", MethodAttributes.Family | MethodAttributes.Virtual, typeof(void), [env.WorldType]);
            ILGenerator il = onCreate.GetILGenerator();

            // 缓存受保护系统与水/地形系统引用
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.LandValueSystemType));
            il.Emit(OpCodes.Stfld, lvField);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.WaterSystemType));
            il.Emit(OpCodes.Stfld, waterField);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.TerrainSystemType));
            il.Emit(OpCodes.Stfld, terrainField);

            // 只读观测 LandValueSystem.GetData(true)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, lvField);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Callvirt, env.LandValueGetDataMethod);
            il.Emit(OpCodes.Pop);

            // this.Enabled = false（接收者为 ldarg.0 Self！）
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);

            // 对工具宿主 AreaToolSystem 和补丁目标 WaterSystem 设 Enabled（均不在 IsProtectedEnabledTarget 名单内）
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.AreaToolSystemType));
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, waterField);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);

            il.Emit(OpCodes.Ret);
            tb.CreateType();
        });

        AssemblyInspectionResult? res = ModAssemblyInspector.InspectBytes(bytes);
        Assert.NotNull(res);
        Assert.Empty(res.EnabledWrites);
        Assert.Empty(res.HarmonyOverlaps);
        Assert.Empty(res.UnpatchAllNullCalls);
        Assert.Empty(res.ReflectionFieldWrites);
    }

    [Fact]
    public void HarmonyDetection_MergesClassAndMethodAttributes_ConventionNames_Getter_AndVanillaRecipe()
    {
        byte[] bytes = SyntheticModBuilder.BuildModAssembly("HarmonyOverlapMod", new Version(1, 0, 0), (mod, env) =>
        {
            // 类级 [HarmonyPatch(typeof(WaterSystem))]
            TypeBuilder tb = mod.DefineType("Sample.WaterPatches", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
            tb.SetCustomAttribute(new CustomAttributeBuilder(env.HarmonyPatchTypeOnlyCtor, [env.WaterSystemType]));

            // 方法 1：方法级 [HarmonyPatch("OnSimulateGPU")] + 约定方法名 Prefix（返回 bool，无 [HarmonyPrefix] 特性）
            // OnSimulateGPU 属于 VanillaRecipe，None 模式下同样应告警！
            MethodBuilder mSim = tb.DefineMethod("Prefix", MethodAttributes.Public | MethodAttributes.Static, typeof(bool), Type.EmptyTypes);
            mSim.SetCustomAttribute(new CustomAttributeBuilder(env.HarmonyPatchMethodOnlyCtor, ["OnSimulateGPU"]));
            ILGenerator il1 = mSim.GetILGenerator();
            il1.Emit(OpCodes.Ldc_I4_1);
            il1.Emit(OpCodes.Ret);

            // 方法 2：[HarmonyPatch(typeof(WaterSystem), "MapSize", MethodType.Getter)] + [HarmonyTranspiler]（仅扩展模式生效）
            MethodBuilder mMapSize = tb.DefineMethod("MapSizeTranspiler", MethodAttributes.Public | MethodAttributes.Static, typeof(object), [typeof(object)]);
            object getterEnumVal = Enum.ToObject(env.HarmonyMethodTypeEnum, 1);
            mMapSize.SetCustomAttribute(new CustomAttributeBuilder(env.HarmonyPatchMethodTypeCtor, [env.WaterSystemType, "MapSize", getterEnumVal]));
            mMapSize.SetCustomAttribute(new CustomAttributeBuilder(env.HarmonyTranspilerAttrType.GetConstructor(Type.EmptyTypes)!, []));
            ILGenerator il2 = mMapSize.GetILGenerator();
            il2.Emit(OpCodes.Ldarg_0);
            il2.Emit(OpCodes.Ret);

            tb.CreateType();
        });

        AssemblyInspectionResult? res = ModAssemblyInspector.InspectBytes(bytes);
        Assert.NotNull(res);
        Assert.Equal(2, res.HarmonyOverlaps.Count);

        HarmonyOverlapEvidence simOverlap = Assert.Single(res.HarmonyOverlaps, o => o.TargetMethodName == "OnSimulateGPU");
        Assert.Equal(DetectedPatchKind.PrefixBlockingCapable, simOverlap.Kind);

        HarmonyOverlapEvidence getterOverlap = Assert.Single(res.HarmonyOverlaps, o => o.TargetMethodName == "get_MapSize");
        Assert.Equal(DetectedPatchKind.Transpiler, getterOverlap.Kind);

        // 验证 None 模式下：OnSimulateGPU 属于 VanillaRecipe 仍为 Warning，而 get_MapSize 为 None
        var vanillaSettings = new ConflictEvalSettings("None", true, true, true, true, false, false, false);
        PatchedMethodTarget simTarget = ConflictTargets.FindPatchedMethod("Game.Simulation.WaterSystem", "OnSimulateGPU")!;
        PatchedMethodTarget mapSizeTarget = ConflictTargets.FindPatchedMethod("Game.Simulation.WaterSystem", "get_MapSize")!;
        Assert.Equal(ConflictSeverity.Warning, ConflictTargets.EvaluatePatchOverlap(simTarget, vanillaSettings, true).Severity);
        Assert.Equal(ConflictSeverity.None, ConflictTargets.EvaluatePatchOverlap(mapSizeTarget, vanillaSettings, true).Severity);
    }

    [Fact]
    public void UnpatchAll_ClassifiesOnLoadAsHard_DisposeAndPrivateHelperAsDisposeOnly_AndWithIdAsIgnored()
    {
        Type updateSysType = SyntheticModBuilder.Env.GameAsm.GetType("Game.UpdateSystem")!;

        // 样本 A：OnDispose 调用私有辅助方法 Cleanup() -> UnpatchAll(null)，且 OnLoad 调用带参 UnpatchAll("my.id") -> 仅 DisposeOnly 备注，0 拦截！
        byte[] benignDisposeMod = SyntheticModBuilder.BuildModAssembly("AnarchyLikeMod", new Version(1, 0, 0), (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType("Benign.ModEntry", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);

            MethodBuilder cleanupHelper = tb.DefineMethod("CleanupPatches", MethodAttributes.Private, typeof(void), Type.EmptyTypes);
            ILGenerator cil = cleanupHelper.GetILGenerator();
            cil.Emit(OpCodes.Ldstr, "benign.id");
            cil.Emit(OpCodes.Newobj, env.HarmonyCtor);
            cil.Emit(OpCodes.Ldnull); // 无参 UnpatchAll(null)
            cil.Emit(OpCodes.Callvirt, env.UnpatchAllMethod);
            cil.Emit(OpCodes.Ret);

            MethodBuilder onLoad = tb.DefineMethod("OnLoad", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [updateSysType]);
            ILGenerator lil = onLoad.GetILGenerator();
            lil.Emit(OpCodes.Ldstr, "benign.id");
            lil.Emit(OpCodes.Newobj, env.HarmonyCtor);
            lil.Emit(OpCodes.Ldstr, "benign.id"); // 带参 UnpatchAll("benign.id") -> 0 误报
            lil.Emit(OpCodes.Callvirt, env.UnpatchAllMethod);
            lil.Emit(OpCodes.Ret);

            MethodBuilder onDispose = tb.DefineMethod("OnDispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
            ILGenerator dil = onDispose.GetILGenerator();
            dil.Emit(OpCodes.Ldarg_0);
            dil.Emit(OpCodes.Call, cleanupHelper);
            dil.Emit(OpCodes.Ret);

            tb.CreateType();
        });

        AssemblyInspectionResult? benignRes = ModAssemblyInspector.InspectBytes(benignDisposeMod);
        Assert.NotNull(benignRes);
        UnpatchAllNullEvidence dispOnly = Assert.Single(benignRes.UnpatchAllNullCalls);
        Assert.Equal(UnpatchAllContext.DisposeOnly, dispOnly.Context);

        // 样本 B：OnLoad 内调用无参 UnpatchAll(null) -> InitializationOrLifecycle (Hard)！
        byte[] badOnLoadMod = SyntheticModBuilder.BuildModAssembly("WipeAllMod", new Version(1, 0, 0), (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType("Bad.ModEntry", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);

            MethodBuilder onLoad = tb.DefineMethod("OnLoad", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [updateSysType]);
            ILGenerator lil = onLoad.GetILGenerator();
            lil.Emit(OpCodes.Ldstr, "bad.id");
            lil.Emit(OpCodes.Newobj, env.HarmonyCtor);
            lil.Emit(OpCodes.Ldnull);
            lil.Emit(OpCodes.Callvirt, env.UnpatchAllMethod);
            lil.Emit(OpCodes.Ret);

            tb.DefineMethod("OnDispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes)
              .GetILGenerator().Emit(OpCodes.Ret);

            tb.CreateType();
        });

        AssemblyInspectionResult? badRes = ModAssemblyInspector.InspectBytes(badOnLoadMod);
        Assert.NotNull(badRes);
        UnpatchAllNullEvidence initHit = Assert.Single(badRes.UnpatchAllNullCalls);
        Assert.Equal(UnpatchAllContext.InitializationOrLifecycle, initHit.Context);
    }

    [Fact]
    public void StaticFieldReflection_RequiresSameMethodCoOccurrence()
    {
        MethodInfo fieldSetValue = typeof(FieldInfo).GetMethod("SetValue", [typeof(object), typeof(object)])!;

        // 1. 跨方法分处（方法 A 有 "kMapSize" 字符串，方法 B 调 FieldInfo.SetValue，如 MapExt2.dll 自身） -> 0 误报
        byte[] splitMethodsBytes = SyntheticModBuilder.BuildModAssembly("SplitMapSizeMod", new Version(1, 0, 0), (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType("Split.Helper", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);

            MethodBuilder mStr = tb.DefineMethod("GetFieldName", MethodAttributes.Public | MethodAttributes.Static, typeof(string), Type.EmptyTypes);
            ILGenerator ilA = mStr.GetILGenerator();
            ilA.Emit(OpCodes.Ldstr, "kMapSize");
            ilA.Emit(OpCodes.Ret);

            MethodBuilder mSet = tb.DefineMethod("WriteOtherField", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [typeof(FieldInfo), typeof(object)]);
            ILGenerator ilB = mSet.GetILGenerator();
            ilB.Emit(OpCodes.Ldarg_0);
            ilB.Emit(OpCodes.Ldnull);
            ilB.Emit(OpCodes.Ldarg_1);
            ilB.Emit(OpCodes.Callvirt, fieldSetValue);
            ilB.Emit(OpCodes.Ret);

            tb.CreateType();
        });

        AssemblyInspectionResult? splitRes = ModAssemblyInspector.InspectBytes(splitMethodsBytes);
        Assert.NotNull(splitRes);
        Assert.Empty(splitRes.ReflectionFieldWrites);

        // 2. 同一方法体内同时包含 "kMapSize" 与 FieldInfo.SetValue -> 命中！
        byte[] sameMethodBytes = SyntheticModBuilder.BuildModAssembly("OverwriteMapSizeMod", new Version(1, 0, 0), (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType("Overwrite.Mutator", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);

            MethodBuilder mMutate = tb.DefineMethod("MutateStaticSize", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [typeof(Type), typeof(FieldInfo)]);
            ILGenerator il = mMutate.GetILGenerator();
            il.Emit(OpCodes.Ldstr, "kMapSize");
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldc_I4, 57344);
            il.Emit(OpCodes.Box, typeof(int));
            il.Emit(OpCodes.Callvirt, fieldSetValue);
            il.Emit(OpCodes.Ret);

            tb.CreateType();
        });

        AssemblyInspectionResult? sameRes = ModAssemblyInspector.InspectBytes(sameMethodBytes);
        Assert.NotNull(sameRes);
        StaticFieldReflectionWriteEvidence hit = Assert.Single(sameRes.ReflectionFieldWrites);
        Assert.Equal("kMapSize", hit.FieldNameLiteral);
    }

    [Fact]
    public void WaterSimSpeedWrites_ListEachWritingMethodOnce_AndIgnoreReadsAndOtherTypes()
    {
        byte[] bytes = SyntheticModBuilder.BuildModAssembly("WaterSpeedMod", new Version(1, 0, 0), (mod, env) =>
        {
            // 自有类型上的同名 setter：声明类型不是原版 WaterSystem，不计入
            TypeBuilder fakeTb = mod.DefineType("WaterSpeed.FakeWater", TypeAttributes.Public | TypeAttributes.Class);
            fakeTb.DefineDefaultConstructor(MethodAttributes.Public);
            MethodBuilder fakeSetter = fakeTb.DefineMethod("set_WaterSimSpeed", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), [typeof(int)]);
            fakeSetter.GetILGenerator().Emit(OpCodes.Ret);
            Type fakeType = fakeTb.CreateType();

            TypeBuilder tb = mod.DefineType("WaterSpeed.ToolSystem", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
            FieldBuilder waterField = tb.DefineField("_water", env.WaterSystemType, FieldAttributes.Private);

            // 1. 同一方法内先冻结再恢复：写两次只记一次
            MethodBuilder onUpdate = tb.DefineMethod("OnUpdate", MethodAttributes.Family | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
            ILGenerator il1 = onUpdate.GetILGenerator();
            il1.Emit(OpCodes.Ldarg_0);
            il1.Emit(OpCodes.Ldfld, waterField);
            il1.Emit(OpCodes.Ldc_I4_0);
            il1.Emit(OpCodes.Callvirt, env.WaterSimSpeedSetter);
            il1.Emit(OpCodes.Ldarg_0);
            il1.Emit(OpCodes.Ldfld, waterField);
            il1.Emit(OpCodes.Ldc_I4_1);
            il1.Emit(OpCodes.Callvirt, env.WaterSimSpeedSetter);
            il1.Emit(OpCodes.Ret);

            // 2. 只读 getter：不计入
            MethodBuilder read = tb.DefineMethod("ReadSpeed", MethodAttributes.Public, typeof(int), Type.EmptyTypes);
            ILGenerator il2 = read.GetILGenerator();
            il2.Emit(OpCodes.Ldarg_0);
            il2.Emit(OpCodes.Ldfld, waterField);
            il2.Emit(OpCodes.Callvirt, env.WaterSimSpeedGetter);
            il2.Emit(OpCodes.Ret);

            // 3. 写自有类型的同名属性：不计入
            MethodBuilder writeFake = tb.DefineMethod("WriteFake", MethodAttributes.Public, typeof(void), [fakeType]);
            ILGenerator il3 = writeFake.GetILGenerator();
            il3.Emit(OpCodes.Ldarg_1);
            il3.Emit(OpCodes.Ldc_I4_1);
            il3.Emit(OpCodes.Callvirt, fakeSetter);
            il3.Emit(OpCodes.Ret);
            tb.CreateType();

            // 4. 静态辅助方法链式写：world.GetOrCreateSystemManaged<WaterSystem>().WaterSimSpeed = speed
            TypeBuilder helperTb = mod.DefineType("WaterSpeed.Helper", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);
            MethodBuilder force = helperTb.DefineMethod("Force", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [env.WorldType, typeof(int)]);
            ILGenerator il4 = force.GetILGenerator();
            il4.Emit(OpCodes.Ldarg_0);
            il4.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.WaterSystemType));
            il4.Emit(OpCodes.Ldarg_1);
            il4.Emit(OpCodes.Callvirt, env.WaterSimSpeedSetter);
            il4.Emit(OpCodes.Ret);
            helperTb.CreateType();
        });

        AssemblyInspectionResult? res = ModAssemblyInspector.InspectBytes(bytes);
        Assert.NotNull(res);
        Assert.NotNull(res.WaterSimSpeedWrites);

        string[] expected = ["WaterSpeed.Helper.Force", "WaterSpeed.ToolSystem.OnUpdate"];
        Assert.Equal(expected, res.WaterSimSpeedWrites.Select(w => w.CallerTypeAndMethod).Order(StringComparer.Ordinal));
        Assert.Empty(res.EnabledWrites);
    }

    [Fact]
    public void TwoDimensionalEvaluation_CellOnlyBlocksOneClickInModeA_AllowsInVanilla_AndNeverDowngradesHardRule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MapExTools_EvalTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var paths = new UserDataPaths(tempDir);

            // 1. 构造一个只改写 RentAdjustSystem.Enabled 的模拟 Mod（检验 C1/D1 CellOnly 双重归属）
            string rentModDir = Path.Combine(paths.ModsDir, "CustomRentMod");
            Directory.CreateDirectory(rentModDir);
            byte[] rentBytes = SyntheticModBuilder.BuildModAssembly("CustomRentMod", new Version(1, 0, 0), (mod, env) =>
            {
                TypeBuilder tb = mod.DefineType("CustomRent.ModSys", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
                MethodBuilder m = tb.DefineMethod("DisableRent", MethodAttributes.Public, typeof(void), [env.WorldType]);
                ILGenerator il = m.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.RentAdjustSystemType));
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
                il.Emit(OpCodes.Ret);
                tb.CreateType();
            });
            File.WriteAllBytes(Path.Combine(rentModDir, "CustomRentMod.dll"), rentBytes);

            // 2. 构造按 RealisticPathFinding 0.7.6 形态停用 4 个原版系统、并把 UpdateGroupSystem 跨阶段注册到 GameSimulation 的 v0.8.0，
            //    检验硬冲突由字节码给出（已无按名规则）；修复了跨阶段注册的版本放到另一独立工作区测试
            string rpfDir = Path.Combine(paths.ModsDir, "RealisticPathFinding");
            Directory.CreateDirectory(rpfDir);
            File.WriteAllBytes(Path.Combine(rpfDir, "RealisticPathFinding.dll"), BuildRealisticPathFindingLike(new Version(0, 8, 0), phaseCode: 12));

            // 3. 构造对模拟核心宿主 CarNavigationSystem+Actions 分别赋 Enabled = false 与 Enabled = true 的两个 Mod（验证 D1 方向乙与 P7-1）
            string hostDisableDir = Path.Combine(paths.ModsDir, "HostDisableMod");
            Directory.CreateDirectory(hostDisableDir);
            byte[] hostDisableBytes = SyntheticModBuilder.BuildModAssembly("HostDisableMod", new Version(1, 0, 0), (mod, env) =>
            {
                TypeBuilder tb = mod.DefineType("HostTest.Disabler", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
                MethodBuilder m = tb.DefineMethod("DisableHost", MethodAttributes.Public, typeof(void), [env.WorldType]);
                ILGenerator il = m.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.CarNavActionsType));
                il.Emit(OpCodes.Ldc_I4_0); // Enabled = false -> Warning (D1 方向乙)
                il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
                il.Emit(OpCodes.Ret);
                tb.CreateType();
            });
            File.WriteAllBytes(Path.Combine(hostDisableDir, "HostDisableMod.dll"), hostDisableBytes);

            string hostEnableDir = Path.Combine(paths.ModsDir, "HostEnableOnlyMod");
            Directory.CreateDirectory(hostEnableDir);
            byte[] hostEnableBytes = SyntheticModBuilder.BuildModAssembly("HostEnableOnlyMod", new Version(1, 0, 0), (mod, env) =>
            {
                TypeBuilder tb = mod.DefineType("HostTest.Enabler", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
                MethodBuilder m = tb.DefineMethod("EnableHost", MethodAttributes.Public, typeof(void), [env.WorldType]);
                ILGenerator il = m.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.CarNavActionsType));
                il.Emit(OpCodes.Ldc_I4_1); // 常量 Enabled = true -> 对宿主无害，0 误报 (P7-1)
                il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
                il.Emit(OpCodes.Ret);
                tb.CreateType();
            });
            File.WriteAllBytes(Path.Combine(hostEnableDir, "HostEnableOnlyMod.dll"), hostEnableBytes);

            // 4. 构造向 NaturalResourceSystem 调用 AddWriter 的 Mod：
            //    因 CellMapReplacementSystem.Redirect 已透明将原版 GetData/AddWriter 重定向至扩展缓冲区（如 SimpleBrush 与 FertilityControl），必须 0 误报！
            string bufWriteDir = Path.Combine(paths.ModsDir, "BufferWriterMod");
            Directory.CreateDirectory(bufWriteDir);
            byte[] bufWriteBytes = SyntheticModBuilder.BuildModAssembly("BufferWriterMod", new Version(1, 0, 0), (mod, env) =>
            {
                TypeBuilder tb = mod.DefineType("BufTest.WriterSys", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
                MethodBuilder m = tb.DefineMethod("WriteNatRes", MethodAttributes.Public, typeof(void), [env.WorldType]);
                ILGenerator il = m.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(env.NaturalResourceSystemType));
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, env.NaturalResourceAddWriterMethod);
                il.Emit(OpCodes.Ret);
                tb.CreateType();
            });
            File.WriteAllBytes(Path.Combine(bufWriteDir, "BufferWriterMod.dll"), bufWriteBytes);

            // 5. 构造跨类字符串字面量动态 TargetMethod() 补丁（验证 ServiceCoverageFix 形态字节码实时检出）
            string dynPatchDir = Path.Combine(paths.ModsDir, "DynamicStringPatchMod");
            Directory.CreateDirectory(dynPatchDir);
            byte[] dynPatchBytes = SyntheticModBuilder.BuildModAssembly("DynamicStringPatchMod", new Version(1, 0, 0), (mod, env) =>
            {
                TypeBuilder gateTb = mod.DefineType("DynTest.Gate", TypeAttributes.Public | TypeAttributes.Class);
                MethodBuilder gateM = gateTb.DefineMethod("GetTargetName", MethodAttributes.Public | MethodAttributes.Static, typeof(string), Type.EmptyTypes);
                ILGenerator gateIl = gateM.GetILGenerator();
                gateIl.Emit(OpCodes.Ldstr, "Game.Simulation.ServiceCoverageSystem");
                gateIl.Emit(OpCodes.Ret);
                gateTb.CreateType();

                TypeBuilder patchTb = mod.DefineType("DynTest.Patch", TypeAttributes.Public | TypeAttributes.Class);
                patchTb.SetCustomAttribute(new CustomAttributeBuilder(env.HarmonyPatchAttrType.GetConstructor(Type.EmptyTypes)!, []));
                MethodBuilder tm = patchTb.DefineMethod("TargetMethod", MethodAttributes.Private | MethodAttributes.Static, typeof(MethodBase), Type.EmptyTypes);
                ILGenerator tmIl = tm.GetILGenerator();
                tmIl.Emit(OpCodes.Ldstr, "OnUpdate");
                tmIl.Emit(OpCodes.Pop);
                tmIl.Emit(OpCodes.Ldnull);
                tmIl.Emit(OpCodes.Ret);

                MethodBuilder transpiler = patchTb.DefineMethod("Transpiler", MethodAttributes.Private | MethodAttributes.Static, typeof(object), [typeof(object)]);
                ILGenerator trIl = transpiler.GetILGenerator();
                trIl.Emit(OpCodes.Ldarg_0);
                trIl.Emit(OpCodes.Ret);
                patchTb.CreateType();
            });
            File.WriteAllBytes(Path.Combine(dynPatchDir, "DynamicStringPatchMod.dll"), dynPatchBytes);

            ScannedModWorkspace workspace = ConflictAnalyzer.ScanWorkspace(paths, gameDir: null);

            // A. ModeA 扩展模式（ResourceBuyer 开启）：
            var modeASettings = new ConflictEvalSettings("ModeA", true, true, true, true, resourceBuyer: true, false, false);
            ConflictReport modeAReport = ConflictAnalyzer.Evaluate(workspace, modeASettings);

            // CustomRentMod（CellOnlyDualRole）：保留 Hard 且禁止给一键关闭按钮
            ModConflictItem rentModeA = Assert.Single(modeAReport.Items, i => i.DisplayName == "CustomRentMod");
            Assert.Equal(ConflictSeverity.Hard, rentModeA.Severity);
            Assert.False(rentModeA.CanOneClickMitigate);
            Assert.Empty(rentModeA.OneClickDisableKeys);

            // 扩展地图模式下 CellOnly 与分组无关：标为扩展地图模式，并带上角色供文案区分“替换”与“修改”
            ConflictFinding rentModeAFinding = Assert.Single(rentModeA.Findings, f => f.Kind == ConflictFindingKind.SystemEnabledWrite);
            Assert.Equal(ConflictScope.ExpandedMapModes, rentModeAFinding.Scope);
            Assert.Null(rentModeAFinding.ScopeGroupKeys);
            Assert.Equal(ProtectedSystemRole.CellOnlyDualRole, rentModeAFinding.SystemRole);

            // 换原版尺寸、沿用同一组分组设定：CustomRentMod 降为条件冲突，扩展专属的宿主停用项消失，故给出跨模式摘要
            var vanillaSameGroups = new ConflictEvalSettings("None", true, true, true, true, resourceBuyer: true, false, false);
            ConflictReport vanillaSameGroupsReport = ConflictAnalyzer.Evaluate(workspace, vanillaSameGroups);
            Assert.Equal(ConflictSeverity.Warning, Assert.Single(vanillaSameGroupsReport.Items, i => i.DisplayName == "CustomRentMod").Severity);
            Assert.DoesNotContain(vanillaSameGroupsReport.Items, i => i.DisplayName == "HostDisableMod");
            Assert.True(vanillaSameGroupsReport.HardConflictCount > 0); // 跨阶段注册的 RealisticPathFinding 在两类模式下都是硬冲突
            const string zh = UiLanguage.SimplifiedChinese;
            string expectedCounts = string.Format(
                CultureInfo.InvariantCulture,
                Strings.Get(zh, "Conflicts.CrossMode.Hard"),
                vanillaSameGroupsReport.HardConflictCount,
                vanillaSameGroupsReport.WarningCount);
            Assert.Equal(
                string.Format(CultureInfo.InvariantCulture, Strings.Get(zh, "Conflicts.CrossMode.Vanilla"), "None 14km", expectedCounts),
                ConflictText.CrossModeSummary(modeAReport, zh));

            // RealisticPathFinding：跨阶段注册由字节码定为 Hard；4 处停用原版系统各记一条，没有按名规则与核对基准版本
            ModConflictItem rpfItem = Assert.Single(modeAReport.Items, i => i.DisplayName == "RealisticPathFinding");
            Assert.Equal(ConflictSeverity.Hard, rpfItem.Severity);
            Assert.Contains(rpfItem.Findings, f => f.Kind == ConflictFindingKind.CrossPhaseSystemSchedule);
            Assert.Equal(4, rpfItem.Findings.Count(f => f.Kind == ConflictFindingKind.SystemEnabledWrite));
            Assert.Equal(ConflictScope.AllModes, Assert.Single(rpfItem.Findings, f => f.Kind == ConflictFindingKind.CrossPhaseSystemSchedule).Scope);
            // 停用的 4 个原版系统都是经济分组替换：按各自分组标注（居民 AI 与消费采购）
            Assert.All(
                rpfItem.Findings.Where(f => f.Kind == ConflictFindingKind.SystemEnabledWrite),
                f =>
                {
                    Assert.Equal(ConflictScope.EconomyGroups, f.Scope);
                    Assert.Equal(ProtectedSystemRole.SharedEconomyReplaced, f.SystemRole);
                    Assert.Contains(Assert.Single(f.ScopeGroupKeys!), new[] { ConflictTargets.KeyResidentAI, ConflictTargets.KeyResourceBuyer });
                });
            Assert.DoesNotContain(rpfItem.Findings, f => f.Kind == ConflictFindingKind.KnownAuditRule);
            Assert.Null(rpfItem.AuditedVersion);
            Assert.False(rpfItem.CanOneClickMitigate);

            // D1 方向乙：模拟核心宿主被设为 Enabled = false 降为 Warning（无一键按钮），常量 Enabled = true 则 0 误报
            ModConflictItem hostDisItem = Assert.Single(modeAReport.Items, i => i.DisplayName == "HostDisableMod");
            Assert.Equal(ConflictSeverity.Warning, hostDisItem.Severity);
            Assert.False(hostDisItem.CanOneClickMitigate);
            Assert.DoesNotContain(modeAReport.Items, i => i.DisplayName == "HostEnableOnlyMod");

            // NaturalResourceSystem.AddWriter：经 CellMapReplacementSystem.Redirect 透明转发，0 误报
            Assert.DoesNotContain(modeAReport.Items, i => i.DisplayName == "BufferWriterMod");

            // 跨类字符串动态补丁命中已替换的 ServiceCoverageSystem.OnUpdate：ResourceBuyer 开启时为 Warning 且支持一键关组
            ModConflictItem dynPatchActive = Assert.Single(modeAReport.Items, i => i.DisplayName == "DynamicStringPatchMod");
            Assert.Equal(ConflictSeverity.Warning, dynPatchActive.Severity);
            Assert.True(dynPatchActive.CanOneClickMitigate);
            Assert.Equal([ConflictTargets.KeyResourceBuyer], dynPatchActive.OneClickDisableKeys);

            // B. None (14km) 模式且 HouseholdProperty 开启、ResourceBuyer 关闭：
            var vanillaOpen = new ConflictEvalSettings("None", true, true, true, true, resourceBuyer: false, false, false);
            ConflictReport vanillaOpenReport = ConflictAnalyzer.Evaluate(workspace, vanillaOpen);
            ModConflictItem rentVanillaOpen = Assert.Single(vanillaOpenReport.Items, i => i.DisplayName == "CustomRentMod");
            Assert.Equal(ConflictSeverity.Warning, rentVanillaOpen.Severity);
            Assert.True(rentVanillaOpen.CanOneClickMitigate);
            Assert.Equal([ConflictTargets.KeyHouseholdProperty], rentVanillaOpen.OneClickDisableKeys);

            // 原版尺寸模式下 CellOnly 只随找房与租金系统组出现：标为该经济分组
            ConflictFinding rentVanillaFinding = Assert.Single(rentVanillaOpen.Findings, f => f.Kind == ConflictFindingKind.SystemEnabledWrite);
            Assert.Equal(ConflictScope.EconomyGroups, rentVanillaFinding.Scope);
            Assert.Equal([ConflictTargets.KeyHouseholdProperty], rentVanillaFinding.ScopeGroupKeys!);
            Assert.DoesNotContain(vanillaOpenReport.Items, i => i.DisplayName == "BufferWriterMod");

            ModConflictItem dynPatchMitigated = Assert.Single(vanillaOpenReport.Items, i => i.DisplayName == "DynamicStringPatchMod");
            Assert.Equal(ConflictSeverity.Mitigated, dynPatchMitigated.Severity);

            // C. None 模式且关闭 EnableHouseholdPropertyEcoSystem 后：变为 Mitigated
            var vanillaClosed = new ConflictEvalSettings("None", true, true, true, false, false, false, false);
            ConflictReport vanillaClosedReport = ConflictAnalyzer.Evaluate(workspace, vanillaClosed);
            ModConflictItem rentVanillaClosed = Assert.Single(vanillaClosedReport.Items, i => i.DisplayName == "CustomRentMod");
            Assert.Equal(ConflictSeverity.Mitigated, rentVanillaClosed.Severity);
            Assert.False(rentVanillaClosed.CanOneClickMitigate);
        }
        finally
        {
            foreach (string f in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
                File.Delete(f);
            foreach (string d in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                Directory.Delete(d, recursive: false);
            Directory.Delete(tempDir, recursive: false);
        }
    }

    [Fact]
    public void RealisticPathFinding_WithoutCrossPhaseSchedule_IsMitigatableWarningFromBytecodeAlone()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MapExTools_RpfFixedTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var paths = new UserDataPaths(tempDir);
            string rpfDir = Path.Combine(paths.ModsDir, "RealisticPathFinding");
            Directory.CreateDirectory(rpfDir);

            // 模拟未来修复了跨阶段注册的 v0.9.0：UpdateGroupSystem 只注册在合法的 Modification5 = 8，仍停用那 4 个原版系统
            File.WriteAllBytes(Path.Combine(rpfDir, "RealisticPathFinding.dll"), BuildRealisticPathFindingLike(new Version(0, 9, 0), phaseCode: 8));

            ScannedModWorkspace workspace = ConflictAnalyzer.ScanWorkspace(paths, gameDir: null);

            // 开启 ResidentAI 组时：没有跨阶段注册，只剩停用原版系统的交集，可通过关闭 ResidentAI 规避
            var activeSettings = new ConflictEvalSettings("ModeA", true, true, true, true, resourceBuyer: false, residentAI: true, false);
            ConflictReport activeReport = ConflictAnalyzer.Evaluate(workspace, activeSettings);
            ModConflictItem activeItem = Assert.Single(activeReport.Items);
            Assert.Equal(ConflictSeverity.Warning, activeItem.Severity);
            Assert.True(activeItem.CanOneClickMitigate);
            Assert.Equal([ConflictTargets.KeyResidentAI], activeItem.OneClickDisableKeys);
            Assert.DoesNotContain(activeItem.Findings, f => f.Kind is ConflictFindingKind.KnownAuditRule or ConflictFindingKind.CrossPhaseSystemSchedule);

            // 只剩经济分组的交集：换原版尺寸模式结果相同，不给跨模式摘要
            Assert.Null(ConflictText.CrossModeSummary(activeReport, UiLanguage.SimplifiedChinese));

            // ResidentAI 与 ResourceBuyer 均关闭（默认设定）时：变为 Mitigated
            var defaultSettings = new ConflictEvalSettings("ModeA", true, true, true, true, resourceBuyer: false, residentAI: false, false);
            ConflictReport defaultReport = ConflictAnalyzer.Evaluate(workspace, defaultSettings);
            ModConflictItem mitigatedItem = Assert.Single(defaultReport.Items);
            Assert.Equal(ConflictSeverity.Mitigated, mitigatedItem.Severity);
        }
        finally
        {
            foreach (string f in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
                File.Delete(f);
            foreach (string d in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                Directory.Delete(d, recursive: false);
            Directory.Delete(tempDir, recursive: false);
        }
    }

    [Fact]
    public void WaterSimSpeedWriter_IsNoteInEveryMode_WithoutKnownRuleOrAuditedVersion()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MapExTools_WaterSpeedTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var paths = new UserDataPaths(tempDir);

            static void WriteSpeedInOnUpdate(ModuleBuilder mod, SyntheticModBuilder.StubEnvironment env, string typeName, OpCode speedConst)
            {
                TypeBuilder tb = mod.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
                FieldBuilder waterField = tb.DefineField("m_WaterSystem", env.WaterSystemType, FieldAttributes.Private);
                MethodBuilder onUpdate = tb.DefineMethod("OnUpdate", MethodAttributes.Family | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
                ILGenerator il = onUpdate.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, waterField);
                il.Emit(speedConst);
                il.Emit(OpCodes.Callvirt, env.WaterSimSpeedSetter);
                il.Emit(OpCodes.Ret);
                tb.CreateType();
            }

            // 程序集名沿用已删除的按名规则 Water_Features：只由字节码给出备注，不再命中按名规则
            string writerDir = Path.Combine(paths.ModsDir, "Water_Features");
            Directory.CreateDirectory(writerDir);
            byte[] writerBytes = SyntheticModBuilder.BuildModAssembly("Water_Features", new Version(1, 4, 5), (mod, env) =>
            {
                WriteSpeedInOnUpdate(mod, env, "WaterTest.Tools.CustomWaterToolSystem", OpCodes.Ldc_I4_0);
                WriteSpeedInOnUpdate(mod, env, "WaterTest.Systems.TidesAndWavesSystem", OpCodes.Ldc_I4_1);
            });
            File.WriteAllBytes(Path.Combine(writerDir, "Water_Features.dll"), writerBytes);

            // 只读 WaterSimSpeed 的模组：不出卡片
            string readerDir = Path.Combine(paths.ModsDir, "WaterSpeedReader");
            Directory.CreateDirectory(readerDir);
            byte[] readerBytes = SyntheticModBuilder.BuildModAssembly("WaterSpeedReader", new Version(1, 0, 0), (mod, env) =>
            {
                TypeBuilder tb = mod.DefineType("WaterTest.ReaderSystem", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
                FieldBuilder waterField = tb.DefineField("m_WaterSystem", env.WaterSystemType, FieldAttributes.Private);
                MethodBuilder read = tb.DefineMethod("ReadSpeed", MethodAttributes.Public, typeof(int), Type.EmptyTypes);
                ILGenerator il = read.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, waterField);
                il.Emit(OpCodes.Callvirt, env.WaterSimSpeedGetter);
                il.Emit(OpCodes.Ret);
                tb.CreateType();
            });
            File.WriteAllBytes(Path.Combine(readerDir, "WaterSpeedReader.dll"), readerBytes);

            ScannedModWorkspace workspace = ConflictAnalyzer.ScanWorkspace(paths, gameDir: null);

            // MapExt 在各模式、各水模拟档位都会写 WaterSimSpeed：定级与模式、经济分组都无关
            foreach (string mode in new[] { "ModeA", "None" })
            {
                ConflictReport report = ConflictAnalyzer.Evaluate(workspace, new ConflictEvalSettings(mode, true, true, true, true, true, true, true));

                ModConflictItem item = Assert.Single(report.Items);
                Assert.Equal("Water_Features", item.DisplayName);
                Assert.Equal(ConflictSeverity.Info, item.Severity);
                Assert.Null(item.AuditedVersion);
                Assert.False(item.CanOneClickMitigate);

                ConflictFinding finding = Assert.Single(item.Findings);
                Assert.Equal(ConflictFindingKind.WaterSimSpeedWrite, finding.Kind);
                Assert.Equal("CustomWaterToolSystem.OnUpdate, TidesAndWavesSystem.OnUpdate", finding.Arg0);

                Assert.Equal(1, report.InfoCount);
                Assert.False(report.HasHardConflicts);
                Assert.False(report.HasWarnings);
            }
        }
        finally
        {
            foreach (string f in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
                File.Delete(f);
            foreach (string d in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                Directory.Delete(d, recursive: false);
            Directory.Delete(tempDir, recursive: false);
        }
    }

    /// <summary>
    /// 按 RealisticPathFinding 0.7.6 的 <c>Mod.OnLoad</c> 形态构造程序集：停用居民 AI 与消费采购的 4 个原版系统，
    /// 并把 <c>UpdateGroupSystem</c> 注册在 <paramref name="phaseCode"/> 阶段（12 = GameSimulation 为跨阶段，8 = Modification5 为合法）。
    /// </summary>
    private static byte[] BuildRealisticPathFindingLike(Version version, sbyte phaseCode) =>
        SyntheticModBuilder.BuildModAssembly("RealisticPathFinding", version, (mod, env) =>
        {
            TypeBuilder dummySys = mod.DefineType("RPF.CustomSys", TypeAttributes.Public | TypeAttributes.Class, env.GameSystemBaseType);
            Type createdDummy = dummySys.CreateType();

            TypeBuilder tb = mod.DefineType("RPF.Entry", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);
            Type updateSysType = env.GameAsm.GetType("Game.UpdateSystem")!;
            MethodBuilder onLoad = tb.DefineMethod("OnLoad", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [updateSysType]);
            ILGenerator il = onLoad.GetILGenerator();

            // new World().GetOrCreateSystemManaged<T>().Enabled = false（真实代码经 World.DefaultGameObjectInjectionWorld 取得世界）
            foreach (Type vanilla in new[] { env.ResidentAISystemType, env.ResidentAIActionsType, env.TripNeededSystemType, env.ResourceBuyerSystemType })
            {
                il.Emit(OpCodes.Newobj, env.WorldType.GetConstructor(Type.EmptyTypes)!);
                il.Emit(OpCodes.Callvirt, env.GetOrCreateGeneric.MakeGenericMethod(vanilla));
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, env.EnabledProperty.SetMethod!);
            }

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_S, phaseCode);
            il.Emit(OpCodes.Callvirt, env.UpdateAfterGeneric2Method.MakeGenericMethod(env.UpdateGroupSystemType, createdDummy));
            il.Emit(OpCodes.Ret);

            tb.DefineMethod("OnDispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
            tb.CreateType();
        });
}
