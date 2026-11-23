// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Colossal.Serialization.Entities;
using Game.Simulation;
using MapExtPDX.CellShared;
using Unity.Jobs;

// === CellMap 存读档 Job 的 Burst 登记 ===
// 基类 CellMapReplacementSystem 的存档、读档 Job 都是泛型，封闭类型要等引擎经 IJobSerializable 调用时才确定，
// Burst 自己发现不了，不登记会在 Mono 里执行（大贴图读档开销显著偏高）。
// 引擎存读档只用 BinaryWriter／BinaryReader（Game.Serialization.SerializerSystem）；基类的类型参数都是原版类型，
// ModeA–D 与 ModeE 的 D2 共用同一组封闭类型，所以 12 个系统 × 2 = 24 条。
// 新增 CellMap 替换系统要在这里补两行。漏了不报错、不写日志，只会在建置日志的 Burst 段少两条 Method:。

// --- 空气污染 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<AirPollution, AirPollutionSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<AirPollution, AirPollutionSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 服务可达性 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<AvailabilityInfoCell, AvailabilityInfoToGridSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<AvailabilityInfoCell, AvailabilityInfoToGridSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 地面污染 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<GroundPollution, GroundPollutionSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<GroundPollution, GroundPollutionSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 地下水 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<GroundWater, GroundWaterSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<GroundWater, GroundWaterSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 地价（ModeA–D 与 ModeE 的 D2）---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<LandValueCell, LandValueSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<LandValueCell, LandValueSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 自然资源 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<NaturalResourceCell, NaturalResourceSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<NaturalResourceCell, NaturalResourceSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 噪音污染 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<NoisePollution, NoisePollutionSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<NoisePollution, NoisePollutionSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 人口 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<PopulationCell, PopulationToGridSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<PopulationCell, PopulationToGridSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 土壤水 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<SoilWater, SoilWaterSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<SoilWater, SoilWaterSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 地形吸引力 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<TerrainAttractiveness, TerrainAttractivenessSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<TerrainAttractiveness, TerrainAttractivenessSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 交通环境 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<TrafficAmbienceCell, TrafficAmbienceSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<TrafficAmbienceCell, TrafficAmbienceSystem>.DeserializeJobResetMismatch<BinaryReader>))]

// --- 区域环境 ---
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<ZoneAmbienceCell, ZoneAmbienceSystem>.SerializeJobMod<BinaryWriter>))]
[assembly: RegisterGenericJobType(typeof(CellMapReplacementSystem<ZoneAmbienceCell, ZoneAmbienceSystem>.DeserializeJobResetMismatch<BinaryReader>))]
