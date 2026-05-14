using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace B323ServerSideFixes
{
    internal static class ReplayFaceOffLifecyclePatch
    {
        private static readonly MethodInfo faceOffStartedPrefix = AccessTools.Method(typeof(ReplayFaceOffLifecyclePatch), nameof(OnFaceOffStartedPrefix));

        public static void Apply(Harmony harmony)
        {
            PatchFaceOffStarted<PublicGameModeConfig>(harmony);
            PatchFaceOffStarted<CompetitiveGameModeConfig>(harmony);
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

        private static void OnFaceOffStartedPrefix(object __instance)
        {
            ReplayManager replayManager = GetReplayManager(__instance);
            if (replayManager == null)
            {
                Debug.LogWarning("B323 Server-Side Fixes: could not find ReplayManager before FaceOff replay reset.");
                return;
            }

            replayManager.Server_StopReplaying();
            replayManager.Server_StopRecording();
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
