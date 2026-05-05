using System.Collections.Generic;

namespace EconomyEX.Helpers
{
    public class JobPatchTarget
    {
        public string TargetTypeName { get; set; }
        public string TargetMethodName { get; set; }
        public string OriginalJobFullName { get; set; }
        public string ReplacementJobFullName { get; set; }
        public string[] MethodParamTypes { get; set; }

        public JobPatchTarget(string targetType, string targetMethod, string originalJob, string replacementJob, string[] methodParams = null)
        {
            TargetTypeName = targetType;
            TargetMethodName = targetMethod;
            MethodParamTypes = methodParams;
            OriginalJobFullName = originalJob.Replace('/', '+');
            ReplacementJobFullName = replacementJob.Replace('/', '+');
        }
    }

    public static class JobPatchDefinitions
    {
        private class TargetInfo
        {
            public string TargetType { get; }
            public string TargetMethod { get; }
            public string OriginalJob { get; }
            public string ReplacementJob { get; }

            public TargetInfo(string type, string method, string orig, string repl)
            {
                TargetType = type;
                TargetMethod = method;
                OriginalJob = orig;
                ReplacementJob = repl;
            }
        }
        
        // These targets MUST match the classes in EconomyEX/Systems/
        // A1-A3: Job structs are top-level (not nested) — 用于 Transpiler 替换原版 Job
        // 注意: D1 (RentAdjust) 已通过 SystemRegistrar 做完整系统替换，不再需要 Job Transpiler
        private static readonly List<TargetInfo> EcoTargets = new List<TargetInfo>
        {
            // A1: ResidentialDemand — 顶级 Job struct
            new TargetInfo("Game.Simulation.ResidentialDemandSystem", "OnUpdate", "Game.Simulation.ResidentialDemandSystem/UpdateResidentialDemandJob", "EconomyEX.Systems.UpdateResidentialDemandJob"),
            // A3: IndustrialDemand — 顶级 Job struct
            new TargetInfo("Game.Simulation.IndustrialDemandSystem", "OnUpdate", "Game.Simulation.IndustrialDemandSystem/UpdateIndustrialDemandJob", "EconomyEX.Systems.UpdateIndustrialDemandJob"),
            // A2: CommercialDemand — 顶级 Job struct
            new TargetInfo("Game.Simulation.CommercialDemandSystem", "OnUpdate", "Game.Simulation.CommercialDemandSystem/UpdateCommercialDemandJob", "EconomyEX.Systems.UpdateCommercialDemandJob"),
        };

        public static List<JobPatchTarget> GetEcoSystemTargets()
        {
            var results = new List<JobPatchTarget>();
            foreach (var t in EcoTargets)
            {
                results.Add(new JobPatchTarget(t.TargetType, t.TargetMethod, t.OriginalJob, t.ReplacementJob));
            }
            return results;
        }
    }
}
