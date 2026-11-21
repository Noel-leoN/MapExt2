using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace MapExTools.Tests.Conflicts;

/// <summary>
/// 基于 .NET 11 内置 <see cref="PersistedAssemblyBuilder"/> 在内存中生成模拟引擎桩库（<c>Unity.Entities</c>、<c>Game</c>、<c>0Harmony</c>）
/// 与最小第三方测试 Mod PE 字节流，零外部二进制样本或第三方 NuGet 依赖。
/// </summary>
internal static class SyntheticModBuilder
{
    private static readonly Lazy<StubEnvironment> s_env = new(CreateStubEnvironment);

    public static StubEnvironment Env => s_env.Value;

    public sealed record StubEnvironment(
        AssemblyLoadContext Alc,
        Assembly UnityEntitiesAsm,
        Assembly GameAsm,
        Assembly HarmonyAsm,
        byte[] UnityEntitiesBytes,
        byte[] GameBytes,
        byte[] HarmonyBytes,
        Type ComponentSystemBaseType,
        PropertyInfo EnabledProperty,
        Type WorldType,
        MethodInfo GetOrCreateGeneric,
        MethodInfo GetExistingGeneric,
        Type IModType,
        Type GameSystemBaseType,
        Type LandValueSystemType,
        Type NaturalResourceSystemType,
        Type UpdateGroupSystemType,
        Type SystemUpdatePhaseType,
        MethodInfo UpdateAfterGeneric2Method,
        MethodInfo NaturalResourceAddWriterMethod,
        Type RentAdjustSystemType,
        Type HouseholdFindPropertySystemType,
        Type ServiceCoverageSystemType,
        Type WaterSystemType,
        Type TerrainSystemType,
        Type AreaToolSystemType,
        Type CarNavActionsType,
        MethodInfo LandValueGetDataMethod,
        Type HarmonyType,
        ConstructorInfo HarmonyCtor,
        MethodInfo UnpatchAllMethod,
        MethodInfo HarmonyPatchImperativeMethod,
        Type HarmonyPatchAttrType,
        ConstructorInfo HarmonyPatchTypeAndMethodCtor,
        ConstructorInfo HarmonyPatchTypeOnlyCtor,
        ConstructorInfo HarmonyPatchMethodOnlyCtor,
        ConstructorInfo HarmonyPatchMethodTypeCtor,
        Type HarmonyPrefixAttrType,
        Type HarmonyPostfixAttrType,
        Type HarmonyTranspilerAttrType,
        Type HarmonyTargetMethodAttrType,
        Type HarmonyMethodTypeEnum,
        MethodInfo WaterSimSpeedGetter,
        MethodInfo WaterSimSpeedSetter,
        Type ResidentAISystemType,
        Type ResidentAIActionsType,
        Type TripNeededSystemType,
        Type ResourceBuyerSystemType,
        Type GameManagerType,
        ConstructorInfo HarmonyPatchStringAndMethodCtor,
        MethodInfo AccessToolsMethodMethod);

    /// <summary>将一个 <see cref="PersistedAssemblyBuilder"/> 序列化为完整的托管 PE <c>.dll</c> 字节数组。</summary>
    public static byte[] EmitDllBytes(PersistedAssemblyBuilder pab)
    {
        MetadataBuilder mdBuilder = pab.GenerateMetadata(out BlobBuilder ilStream, out BlobBuilder fieldData);
        var peBuilder = new ManagedPEBuilder(
            PEHeaderBuilder.CreateLibraryHeader(),
            new MetadataRootBuilder(mdBuilder),
            ilStream,
            fieldData);

        var peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);
        return peBlob.ToArray();
    }

    public static byte[] BuildModAssembly(
        string assemblyName,
        Version? version,
        Action<ModuleBuilder, StubEnvironment> configure)
    {
        var asmName = new AssemblyName(assemblyName) { Version = version ?? new Version(1, 0, 0, 0) };
        var pab = new PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
        ModuleBuilder mod = pab.DefineDynamicModule(assemblyName + ".dll");
        configure(mod, Env);
        return EmitDllBytes(pab);
    }

    /// <summary>
    /// 构造测试程序集并在序列化为 PE 前将 <c>ilStream</c> 中匹配 <paramref name="markerPattern"/> 的等长字节序列覆写为
    /// <paramref name="replacement"/>，用于注入未定义操作码或畸形 switch 跳转表等边界 IL。
    /// </summary>
    public static byte[] BuildModAssemblyWithIlPatch(
        string assemblyName,
        Version? version,
        Action<ModuleBuilder, StubEnvironment> configure,
        byte[] markerPattern,
        byte[] replacement)
    {
        ArgumentNullException.ThrowIfNull(markerPattern);
        ArgumentNullException.ThrowIfNull(replacement);
        if (markerPattern.Length == 0 || markerPattern.Length != replacement.Length)
            throw new ArgumentException("markerPattern 与 replacement 必须非空且等长。");

        var asmName = new AssemblyName(assemblyName) { Version = version ?? new Version(1, 0, 0, 0) };
        var pab = new PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
        ModuleBuilder mod = pab.DefineDynamicModule(assemblyName + ".dll");
        configure(mod, Env);

        MetadataBuilder mdBuilder = pab.GenerateMetadata(out BlobBuilder ilStream, out BlobBuilder fieldData);
        byte[] ilBytes = ilStream.ToArray();
        int idx = ilBytes.AsSpan().IndexOf(markerPattern);
        if (idx < 0)
            throw new InvalidOperationException("未在生成的 IL 流中定位到目标标记字节序列。");

        replacement.CopyTo(ilBytes, idx);
        var patchedIl = new BlobBuilder();
        patchedIl.WriteBytes(ilBytes);

        var peBuilder = new ManagedPEBuilder(
            PEHeaderBuilder.CreateLibraryHeader(),
            new MetadataRootBuilder(mdBuilder),
            patchedIl,
            fieldData);

        var peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);
        return peBlob.ToArray();
    }

    private static StubEnvironment CreateStubEnvironment()
    {
        var alc = new AssemblyLoadContext("ConflictScannerStubs", isCollectible: false);

        // === 1. 构建 Unity.Entities 桩程序集 ===
        var uePab = new PersistedAssemblyBuilder(new AssemblyName("Unity.Entities") { Version = new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        ModuleBuilder ueMod = uePab.DefineDynamicModule("Unity.Entities.dll");

        TypeBuilder csbTb = ueMod.DefineType("Unity.Entities.ComponentSystemBase", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class);
        csbTb.DefineDefaultConstructor(MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        PropertyBuilder enabledPropBuilder = csbTb.DefineProperty("Enabled", PropertyAttributes.None, typeof(bool), Type.EmptyTypes);
        MethodBuilder getEnabled = csbTb.DefineMethod("get_Enabled", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(bool), Type.EmptyTypes);
        ILGenerator getIl = getEnabled.GetILGenerator();
        getIl.Emit(OpCodes.Ldc_I4_1);
        getIl.Emit(OpCodes.Ret);
        MethodBuilder setEnabled = csbTb.DefineMethod("set_Enabled", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), [typeof(bool)]);
        setEnabled.GetILGenerator().Emit(OpCodes.Ret);
        enabledPropBuilder.SetGetMethod(getEnabled);
        enabledPropBuilder.SetSetMethod(setEnabled);

        MethodBuilder onCreateVirt = csbTb.DefineMethod("OnCreate", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot, typeof(void), Type.EmptyTypes);
        onCreateVirt.GetILGenerator().Emit(OpCodes.Ret);
        MethodBuilder onUpdateVirt = csbTb.DefineMethod("OnUpdate", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot, typeof(void), Type.EmptyTypes);
        onUpdateVirt.GetILGenerator().Emit(OpCodes.Ret);

        Type csbCreated = csbTb.CreateType();

        TypeBuilder worldTb = ueMod.DefineType("Unity.Entities.World", TypeAttributes.Public | TypeAttributes.Class);
        worldTb.DefineDefaultConstructor(MethodAttributes.Public);
        MethodBuilder getOrCreateMb = worldTb.DefineMethod("GetOrCreateSystemManaged", MethodAttributes.Public, CallingConventions.Standard);
        GenericTypeParameterBuilder[] gp1 = getOrCreateMb.DefineGenericParameters("T");
        gp1[0].SetBaseTypeConstraint(csbCreated);
        getOrCreateMb.SetReturnType(gp1[0]);
        getOrCreateMb.SetParameters(Type.EmptyTypes);
        ILGenerator gocIl = getOrCreateMb.GetILGenerator();
        LocalBuilder locDefault1 = gocIl.DeclareLocal(gp1[0]);
        gocIl.Emit(OpCodes.Ldloca_S, locDefault1);
        gocIl.Emit(OpCodes.Initobj, gp1[0]);
        gocIl.Emit(OpCodes.Ldloc_0);
        gocIl.Emit(OpCodes.Ret);

        MethodBuilder getExistingMb = worldTb.DefineMethod("GetExistingSystemManaged", MethodAttributes.Public, CallingConventions.Standard);
        GenericTypeParameterBuilder[] gp2 = getExistingMb.DefineGenericParameters("T");
        gp2[0].SetBaseTypeConstraint(csbCreated);
        getExistingMb.SetReturnType(gp2[0]);
        getExistingMb.SetParameters(Type.EmptyTypes);
        ILGenerator gexIl = getExistingMb.GetILGenerator();
        LocalBuilder locDefault2 = gexIl.DeclareLocal(gp2[0]);
        gexIl.Emit(OpCodes.Ldloca_S, locDefault2);
        gexIl.Emit(OpCodes.Initobj, gp2[0]);
        gexIl.Emit(OpCodes.Ldloc_0);
        gexIl.Emit(OpCodes.Ret);

        worldTb.CreateType();

        byte[] ueBytes = EmitDllBytes(uePab);
        Assembly ueAsm = alc.LoadFromStream(new MemoryStream(ueBytes));
        Type csbType = ueAsm.GetType("Unity.Entities.ComponentSystemBase")!;
        Type worldType = ueAsm.GetType("Unity.Entities.World")!;

        // === 2. 构建 Game 桩程序集 ===
        var gamePab = new PersistedAssemblyBuilder(new AssemblyName("Game") { Version = new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        ModuleBuilder gameMod = gamePab.DefineDynamicModule("Game.dll");

        EnumBuilder phaseEnum = gameMod.DefineEnum("Game.SystemUpdatePhase", TypeAttributes.Public, typeof(int));
        phaseEnum.DefineLiteral("Modification5", 8);
        phaseEnum.DefineLiteral("GameSimulation", 12);
        Type phaseCreated = phaseEnum.CreateTypeInfo();

        TypeBuilder updateSysTb = gameMod.DefineType("Game.UpdateSystem", TypeAttributes.Public | TypeAttributes.Class);
        updateSysTb.DefineDefaultConstructor(MethodAttributes.Public);
        MethodBuilder updateAfter2Mb = updateSysTb.DefineMethod("UpdateAfter", MethodAttributes.Public, CallingConventions.Standard);
        updateAfter2Mb.DefineGenericParameters("TSystem", "TOther");
        updateAfter2Mb.SetReturnType(typeof(void));
        updateAfter2Mb.SetParameters([phaseCreated]);
        updateAfter2Mb.GetILGenerator().Emit(OpCodes.Ret);
        Type updateSysCreated = updateSysTb.CreateType();

        TypeBuilder imodTb = gameMod.DefineType("Game.Modding.IMod", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        imodTb.DefineMethod("OnLoad", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual, typeof(void), [updateSysCreated]);
        imodTb.DefineMethod("OnDispose", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
        imodTb.CreateType();

        TypeBuilder gsbTb = gameMod.DefineType("Game.GameSystemBase", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class, csbType);
        gsbTb.DefineDefaultConstructor(MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        Type gsbCreated = gsbTb.CreateType();

        static Type DefineSimpleSystem(ModuleBuilder mod, string fullName, Type baseType, bool addGetData = false)
        {
            TypeBuilder tb = mod.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class, baseType);
            tb.DefineDefaultConstructor(MethodAttributes.Public);
            if (addGetData)
            {
                MethodBuilder gd = tb.DefineMethod("GetData", MethodAttributes.Public, typeof(int), [typeof(bool)]);
                ILGenerator il = gd.GetILGenerator();
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ret);
            }

            return tb.CreateType();
        }

        DefineSimpleSystem(gameMod, "Game.Simulation.LandValueSystem", gsbCreated, addGetData: true);
        TypeBuilder natResTb = gameMod.DefineType("Game.Simulation.NaturalResourceSystem", TypeAttributes.Public | TypeAttributes.Class, gsbCreated);
        natResTb.DefineDefaultConstructor(MethodAttributes.Public);
        natResTb.DefineMethod("AddWriter", MethodAttributes.Public, typeof(void), [typeof(int)]).GetILGenerator().Emit(OpCodes.Ret);
        natResTb.CreateType();
        DefineSimpleSystem(gameMod, "Game.Simulation.UpdateGroupSystem", gsbCreated);
        DefineSimpleSystem(gameMod, "Game.Simulation.RentAdjustSystem", gsbCreated);
        DefineSimpleSystem(gameMod, "Game.Simulation.HouseholdFindPropertySystem", gsbCreated);
        DefineSimpleSystem(gameMod, "Game.Simulation.ServiceCoverageSystem", gsbCreated);

        // WaterSystem 带 WaterSimSpeed 读写属性（原版为自动属性，外部写入编译为 set_WaterSimSpeed 调用）
        TypeBuilder waterTb = gameMod.DefineType("Game.Simulation.WaterSystem", TypeAttributes.Public | TypeAttributes.Class, gsbCreated);
        waterTb.DefineDefaultConstructor(MethodAttributes.Public);
        PropertyBuilder simSpeedProp = waterTb.DefineProperty("WaterSimSpeed", PropertyAttributes.None, typeof(int), Type.EmptyTypes);
        MethodBuilder getSimSpeed = waterTb.DefineMethod("get_WaterSimSpeed", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
        ILGenerator getSimSpeedIl = getSimSpeed.GetILGenerator();
        getSimSpeedIl.Emit(OpCodes.Ldc_I4_1);
        getSimSpeedIl.Emit(OpCodes.Ret);
        MethodBuilder setSimSpeed = waterTb.DefineMethod("set_WaterSimSpeed", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), [typeof(int)]);
        setSimSpeed.GetILGenerator().Emit(OpCodes.Ret);
        simSpeedProp.SetGetMethod(getSimSpeed);
        simSpeedProp.SetSetMethod(setSimSpeed);
        waterTb.CreateType();

        DefineSimpleSystem(gameMod, "Game.Simulation.TerrainSystem", gsbCreated);
        DefineSimpleSystem(gameMod, "Game.Simulation.PathfindSetupSystem", gsbCreated);
        DefineSimpleSystem(gameMod, "Game.Tools.AreaToolSystem", gsbCreated);

        TypeBuilder carNavTb = gameMod.DefineType("Game.Simulation.CarNavigationSystem", TypeAttributes.Public | TypeAttributes.Class, gsbCreated);
        carNavTb.DefineDefaultConstructor(MethodAttributes.Public);
        TypeBuilder actionsTb = carNavTb.DefineNestedType("Actions", TypeAttributes.NestedPublic | TypeAttributes.Class, gsbCreated);
        actionsTb.DefineDefaultConstructor(MethodAttributes.Public);
        actionsTb.CreateType();
        carNavTb.CreateType();

        // RealisticPathFinding 停用的 4 个原版系统（居民 AI 组 2 个、消费采购组 2 个）
        TypeBuilder residentAiTb = gameMod.DefineType("Game.Simulation.ResidentAISystem", TypeAttributes.Public | TypeAttributes.Class, gsbCreated);
        residentAiTb.DefineDefaultConstructor(MethodAttributes.Public);
        TypeBuilder residentActionsTb = residentAiTb.DefineNestedType("Actions", TypeAttributes.NestedPublic | TypeAttributes.Class, gsbCreated);
        residentActionsTb.DefineDefaultConstructor(MethodAttributes.Public);
        residentActionsTb.CreateType();
        residentAiTb.CreateType();
        DefineSimpleSystem(gameMod, "Game.Simulation.TripNeededSystem", gsbCreated);
        DefineSimpleSystem(gameMod, "Game.Simulation.ResourceBuyerSystem", gsbCreated);

        TypeBuilder gmTb = gameMod.DefineType("Game.SceneFlow.GameManager", TypeAttributes.Public | TypeAttributes.Class);
        gmTb.DefineDefaultConstructor(MethodAttributes.Public);
        gmTb.DefineMethod("Load", MethodAttributes.Public, typeof(void), Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
        gmTb.CreateType();

        byte[] gameBytes = EmitDllBytes(gamePab);
        Assembly gameAsm = alc.LoadFromStream(new MemoryStream(gameBytes));

        // === 3. 构建 0Harmony 桩程序集 ===
        var harmPab = new PersistedAssemblyBuilder(new AssemblyName("0Harmony") { Version = new Version(2, 2, 2, 0) }, typeof(object).Assembly);
        ModuleBuilder harmMod = harmPab.DefineDynamicModule("0Harmony.dll");

        EnumBuilder mtEnum = harmMod.DefineEnum("HarmonyLib.MethodType", TypeAttributes.Public, typeof(int));
        mtEnum.DefineLiteral("Normal", 0);
        mtEnum.DefineLiteral("Getter", 1);
        mtEnum.DefineLiteral("Setter", 2);
        Type mtCreated = mtEnum.CreateTypeInfo();

        TypeBuilder harmonyTb = harmMod.DefineType("HarmonyLib.Harmony", TypeAttributes.Public | TypeAttributes.Class);
        harmonyTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        harmonyTb.DefineMethod("UnpatchAll", MethodAttributes.Public, typeof(void), [typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        harmonyTb.DefineMethod("Patch", MethodAttributes.Public, typeof(MethodInfo), [typeof(MethodBase)]).GetILGenerator().Emit(OpCodes.Ldnull);
        // 补上 Ret
        TypeBuilder patchAttrTb = harmMod.DefineType("HarmonyLib.HarmonyPatch", TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
        patchAttrTb.DefineDefaultConstructor(MethodAttributes.Public);
        patchAttrTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type)]).GetILGenerator().Emit(OpCodes.Ret);
        patchAttrTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        patchAttrTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type), typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        patchAttrTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type), typeof(string), mtCreated]).GetILGenerator().Emit(OpCodes.Ret);
        patchAttrTb.CreateType();

        static void DefineMarkerAttr(ModuleBuilder mod, string name)
        {
            TypeBuilder tb = mod.DefineType(name, TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
            tb.DefineDefaultConstructor(MethodAttributes.Public);
            tb.CreateType();
        }

        DefineMarkerAttr(harmMod, "HarmonyLib.HarmonyPrefix");
        DefineMarkerAttr(harmMod, "HarmonyLib.HarmonyPostfix");
        DefineMarkerAttr(harmMod, "HarmonyLib.HarmonyTranspiler");
        DefineMarkerAttr(harmMod, "HarmonyLib.HarmonyTargetMethod");

        // 修正 Harmony.Patch 的 Ret 指令并创建类型
        TypeBuilder harmonyTb2 = harmMod.DefineType("HarmonyLib.Harmony2Unused", TypeAttributes.NotPublic);
        harmonyTb2.CreateType();

        // 重新单独定义干净的 HarmonyLib.Harmony：刚才 Patch 少了 Ret，改为干净构建一次
        var harmPabClean = new PersistedAssemblyBuilder(new AssemblyName("0Harmony") { Version = new Version(2, 2, 2, 0) }, typeof(object).Assembly);
        ModuleBuilder hm = harmPabClean.DefineDynamicModule("0Harmony.dll");

        EnumBuilder mtClean = hm.DefineEnum("HarmonyLib.MethodType", TypeAttributes.Public, typeof(int));
        mtClean.DefineLiteral("Normal", 0);
        mtClean.DefineLiteral("Getter", 1);
        mtClean.DefineLiteral("Setter", 2);
        Type mtCleanType = mtClean.CreateTypeInfo();

        TypeBuilder hTb = hm.DefineType("HarmonyLib.Harmony", TypeAttributes.Public | TypeAttributes.Class);
        hTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        hTb.DefineMethod("UnpatchAll", MethodAttributes.Public, typeof(void), [typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        MethodBuilder patchMb = hTb.DefineMethod("Patch", MethodAttributes.Public, typeof(MethodInfo), [typeof(MethodBase)]);
        ILGenerator patchIl = patchMb.GetILGenerator();
        patchIl.Emit(OpCodes.Ldnull);
        patchIl.Emit(OpCodes.Ret);
        hTb.CreateType();

        TypeBuilder hpTb = hm.DefineType("HarmonyLib.HarmonyPatch", TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
        hpTb.DefineDefaultConstructor(MethodAttributes.Public);
        hpTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type)]).GetILGenerator().Emit(OpCodes.Ret);
        hpTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        hpTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type), typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        hpTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string), typeof(string)]).GetILGenerator().Emit(OpCodes.Ret);
        hpTb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type), typeof(string), mtCleanType]).GetILGenerator().Emit(OpCodes.Ret);
        hpTb.CreateType();

        TypeBuilder atTb = hm.DefineType("HarmonyLib.AccessTools", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
        MethodBuilder atMethodMb = atTb.DefineMethod("Method", MethodAttributes.Public | MethodAttributes.Static, typeof(MethodInfo), [typeof(Type), typeof(string)]);
        ILGenerator atIl = atMethodMb.GetILGenerator();
        atIl.Emit(OpCodes.Ldnull);
        atIl.Emit(OpCodes.Ret);
        atTb.CreateType();

        DefineMarkerAttr(hm, "HarmonyLib.HarmonyPrefix");
        DefineMarkerAttr(hm, "HarmonyLib.HarmonyPostfix");
        DefineMarkerAttr(hm, "HarmonyLib.HarmonyTranspiler");
        DefineMarkerAttr(hm, "HarmonyLib.HarmonyTargetMethod");

        byte[] harmBytes = EmitDllBytes(harmPabClean);
        Assembly harmAsm = alc.LoadFromStream(new MemoryStream(harmBytes));

        Type landValueType = gameAsm.GetType("Game.Simulation.LandValueSystem")!;
        Type natResType = gameAsm.GetType("Game.Simulation.NaturalResourceSystem")!;
        Type waterSystemType = gameAsm.GetType("Game.Simulation.WaterSystem")!;
        Type updateSysRuntime = gameAsm.GetType("Game.UpdateSystem")!;
        Type harmonyType = harmAsm.GetType("HarmonyLib.Harmony")!;
        Type hpAttrType = harmAsm.GetType("HarmonyLib.HarmonyPatch")!;
        Type mtEnumType = harmAsm.GetType("HarmonyLib.MethodType")!;

        return new StubEnvironment(
            alc,
            ueAsm,
            gameAsm,
            harmAsm,
            ueBytes,
            gameBytes,
            harmBytes,
            csbType,
            csbType.GetProperty("Enabled")!,
            worldType,
            worldType.GetMethod("GetOrCreateSystemManaged")!,
            worldType.GetMethod("GetExistingSystemManaged")!,
            gameAsm.GetType("Game.Modding.IMod")!,
            gameAsm.GetType("Game.GameSystemBase")!,
            landValueType,
            natResType,
            gameAsm.GetType("Game.Simulation.UpdateGroupSystem")!,
            gameAsm.GetType("Game.SystemUpdatePhase")!,
            updateSysRuntime.GetMethod("UpdateAfter")!,
            natResType.GetMethod("AddWriter")!,
            gameAsm.GetType("Game.Simulation.RentAdjustSystem")!,
            gameAsm.GetType("Game.Simulation.HouseholdFindPropertySystem")!,
            gameAsm.GetType("Game.Simulation.ServiceCoverageSystem")!,
            waterSystemType,
            gameAsm.GetType("Game.Simulation.TerrainSystem")!,
            gameAsm.GetType("Game.Tools.AreaToolSystem")!,
            gameAsm.GetType("Game.Simulation.CarNavigationSystem+Actions")!,
            landValueType.GetMethod("GetData")!,
            harmonyType,
            harmonyType.GetConstructor([typeof(string)])!,
            harmonyType.GetMethod("UnpatchAll")!,
            harmonyType.GetMethod("Patch")!,
            hpAttrType,
            hpAttrType.GetConstructor([typeof(Type), typeof(string)])!,
            hpAttrType.GetConstructor([typeof(Type)])!,
            hpAttrType.GetConstructor([typeof(string)])!,
            hpAttrType.GetConstructor([typeof(Type), typeof(string), mtEnumType])!,
            harmAsm.GetType("HarmonyLib.HarmonyPrefix")!,
            harmAsm.GetType("HarmonyLib.HarmonyPostfix")!,
            harmAsm.GetType("HarmonyLib.HarmonyTranspiler")!,
            harmAsm.GetType("HarmonyLib.HarmonyTargetMethod")!,
            mtEnumType,
            waterSystemType.GetProperty("WaterSimSpeed")!.GetMethod!,
            waterSystemType.GetProperty("WaterSimSpeed")!.SetMethod!,
            gameAsm.GetType("Game.Simulation.ResidentAISystem")!,
            gameAsm.GetType("Game.Simulation.ResidentAISystem+Actions")!,
            gameAsm.GetType("Game.Simulation.TripNeededSystem")!,
            gameAsm.GetType("Game.Simulation.ResourceBuyerSystem")!,
            gameAsm.GetType("Game.SceneFlow.GameManager")!,
            hpAttrType.GetConstructor([typeof(string), typeof(string)])!,
            harmAsm.GetType("HarmonyLib.AccessTools")!.GetMethod("Method", [typeof(Type), typeof(string)])!);
    }
}
