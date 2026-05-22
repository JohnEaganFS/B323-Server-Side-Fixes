using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace B323ServerSideFixes
{
    internal static class ReplayFaceOffLifecyclePatch
    {
        private static readonly MethodInfo faceOffStartedPrefix = AccessTools.Method(typeof(ReplayFaceOffLifecyclePatch), nameof(OnFaceOffStartedPrefix));
        private static readonly MethodInfo warmupStartedPrefix = AccessTools.Method(typeof(ReplayFaceOffLifecyclePatch), nameof(OnWarmupStartedPrefix));

        public static void Apply(Harmony harmony)
        {
            PatchFaceOffStarted<PublicGameModeConfig>(harmony);
            PatchFaceOffStarted<CompetitiveGameModeConfig>(harmony);
            PatchWarmupStarted<PublicGameModeConfig>(harmony);
            PatchWarmupStarted<CompetitiveGameModeConfig>(harmony);
        }

        private static void PatchFaceOffStarted<TConfig>(Harmony harmony) where TConfig : StandardGameModeConfig, new()
        {
            MethodInfo original = AccessTools.Method(typeof(StandardGameMode<TConfig>), "OnFaceOffStarted");
            if (original == null)
            {
                throw new MissingMethodException(typeof(StandardGameMode<TConfig>).FullName, "OnFaceOffStarted");
            }

            harmony.Patch(original, prefix: new HarmonyMethod(faceOffStartedPrefix));
        }

        private static void PatchWarmupStarted<TConfig>(Harmony harmony) where TConfig : StandardGameModeConfig, new()
        {
            MethodInfo original = AccessTools.Method(typeof(StandardGameMode<TConfig>), "OnWarmupStarted");
            if (original == null)
            {
                throw new MissingMethodException(typeof(StandardGameMode<TConfig>).FullName, "OnWarmupStarted");
            }

            harmony.Patch(original, prefix: new HarmonyMethod(warmupStartedPrefix));
        }

        private static void OnFaceOffStartedPrefix(object __instance)
        {
            StopReplayLifecycle(__instance, "FaceOff replay reset");
        }

        private static void OnWarmupStartedPrefix(object __instance)
        {
            StopReplayLifecycle(__instance, "Warmup replay reset");
        }

        private static void StopReplayLifecycle(object instance, string context)
        {
            if (!IsServer())
            {
                return;
            }

            ReplayManager replayManager = GetReplayManager(instance);
            if (replayManager == null)
            {
                Debug.LogWarning("B323 Server-Side Fixes: could not find ReplayManager before " + context + ".");
                return;
            }

            replayManager.Server_StopReplaying();
            replayManager.Server_StopRecording();
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }

        private static ReplayManager GetReplayManager(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo replayManagerField = AccessTools.Field(instance.GetType(), "ReplayManager");
            return replayManagerField?.GetValue(instance) as ReplayManager;
        }
    }
}
