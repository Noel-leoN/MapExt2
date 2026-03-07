// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using Game.Simulation;
using EconomyEX.Systems;
using Unity.Entities;

namespace EconomyEX.Detection
{
    public partial class ConflictMonitoringSystem : SystemBase
    {
        private int _ticker = 0;
        private const int CheckInterval = 300; // Check every ~5 seconds (60fps)

        protected override void OnUpdate()
        {
            if (!Mod.IsActive) return;

            _ticker++;
            if (_ticker < CheckInterval) return;
            _ticker = 0;

            CheckForConflicts();
        }

        private void CheckForConflicts()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // 1. Check Vanilla Systems (Should be DISABLED)
            var vanillaSystem = world.GetExistingSystemManaged<HouseholdFindPropertySystem>();
            if (vanillaSystem != null && vanillaSystem.Enabled)
            {
                Mod.Warn("Conflict Detected: 'HouseholdFindPropertySystem' (Vanilla) was re-enabled!");
                SetWarning("Severe Conflict: Vanilla systems re-enabled by another mod!");
                return;
            }

            // 2. Check Mod Systems (Should be ENABLED)
            var modSystem = world.GetExistingSystemManaged<HouseholdFindPropertySystemMod>();
            if (modSystem != null && !modSystem.Enabled)
            {
                Mod.Warn("Conflict Detected: 'HouseholdFindPropertySystemMod' (EconomyEX) was disabled!");
                SetWarning("Conflict: EconomyEX systems disabled by another mod!");
                return;
            }
            
            // Clear warning if everything is fine
            if (Mod.Instance?.Settings != null && Mod.Instance.Settings.ConflictWarning.StartsWith("Conflict"))
            {
                SetWarning("None");
            }
        }

        private void SetWarning(string msg)
        {
             if (Mod.Instance?.Settings != null && Mod.Instance.Settings.ConflictWarning != msg)
             {
                 Mod.Instance.Settings.ConflictWarning = msg;
             }
        }
    }
}
