using System;
using System.Reflection;
using HarmonyLib;

namespace B323ServerSideFixes
{
    internal static class ReplayStickSimulationPatch
    {
        private static readonly MethodInfo stickFixedUpdatePrefix = AccessTools.Method(typeof(ReplayStickSimulationPatch), nameof(StickFixedUpdatePrefix));
        private static readonly MethodInfo stickPositionerFixedUpdatePrefix = AccessTools.Method(typeof(ReplayStickSimulationPatch), nameof(StickPositionerFixedUpdatePrefix));

        public static void Apply(Harmony harmony)
        {
            MethodInfo stickFixedUpdate = AccessTools.Method(typeof(Stick), "FixedUpdate");
            if (stickFixedUpdate == null)
            {
                throw new MissingMethodException(typeof(Stick).FullName, "FixedUpdate");
            }

            MethodInfo stickPositionerFixedUpdate = AccessTools.Method(typeof(StickPositioner), "FixedUpdate");
            if (stickPositionerFixedUpdate == null)
            {
                throw new MissingMethodException(typeof(StickPositioner).FullName, "FixedUpdate");
            }

            harmony.Patch(stickFixedUpdate, prefix: new HarmonyMethod(stickFixedUpdatePrefix));
            harmony.Patch(stickPositionerFixedUpdate, prefix: new HarmonyMethod(stickPositionerFixedUpdatePrefix));
        }

        private static bool StickFixedUpdatePrefix(Stick __instance)
        {
            return !IsReplayPlayer(__instance?.Player);
        }

        private static bool StickPositionerFixedUpdatePrefix(StickPositioner __instance)
        {
            return !IsReplayPlayer(__instance?.Player);
        }

        private static bool IsReplayPlayer(Player player)
        {
            return player != null && player.IsReplay != null && player.IsReplay.Value;
        }
    }
}
