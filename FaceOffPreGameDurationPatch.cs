using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace B897ServerSideFixes
{
    internal static class FaceOffPreGameDurationPatch
    {
        private static readonly MethodInfo preGameTimedOutPrefix = AccessTools.Method(typeof(FaceOffPreGameDurationPatch), nameof(OnPreGameTimedOutPrefix));

        public static void Apply(Harmony harmony)
        {
            PatchOnPreGameTimedOut<PublicGameModeConfig>(harmony);
            PatchOnPreGameTimedOut<CompetitiveGameModeConfig>(harmony);
        }

        private static void PatchOnPreGameTimedOut<TConfig>(Harmony harmony) where TConfig : StandardGameModeConfig, new()
        {
            MethodInfo original = AccessTools.Method(typeof(StandardGameMode<TConfig>), "OnPreGameTimedOut");
            if (original == null)
            {
                throw new MissingMethodException(typeof(StandardGameMode<TConfig>).FullName, "OnPreGameTimedOut");
            }

            harmony.Patch(original, prefix: new HarmonyMethod(preGameTimedOutPrefix));
        }

        private static void OnPreGameTimedOutPrefix(object __instance)
        {
            if (!IsServer())
            {
                return;
            }

            StandardGameModeConfig config = GetConfig(__instance);
            if (config == null || !config.phaseDurationMap.TryGetValue(GamePhase.Play, out int playDuration))
            {
                return;
            }

            SetTickRemainder(__instance, playDuration);
            Debug.Log(string.Format(
                "B897 Server-Side Fixes: seeded faceoff play duration before PreGame timeout. tickRemainder={0}",
                playDuration));
        }

        private static StandardGameModeConfig GetConfig(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            PropertyInfo configProperty = AccessTools.Property(instance.GetType(), "Config");
            return configProperty?.GetValue(instance, null) as StandardGameModeConfig;
        }

        private static void SetTickRemainder(object instance, int tickRemainder)
        {
            FieldInfo tickRemainderField = AccessTools.Field(instance.GetType(), "tickRemainder");
            tickRemainderField?.SetValue(instance, tickRemainder);
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
