using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace B897ServerSideFixes
{
    internal static class ReplayStartGameCleanupPatch
    {
        private static readonly MethodInfo startGamePrefix = AccessTools.Method(typeof(ReplayStartGameCleanupPatch), nameof(StartGamePrefix));

        public static void Apply(Harmony harmony)
        {
            PatchStartGame<PublicGameModeConfig>(harmony);
            PatchStartGame<CompetitiveGameModeConfig>(harmony);
        }

        private static void PatchStartGame<TConfig>(Harmony harmony) where TConfig : StandardGameModeConfig, new()
        {
            MethodInfo original = AccessTools.Method(typeof(StandardGameMode<TConfig>), "StartGame");
            if (original == null)
            {
                throw new MissingMethodException(typeof(StandardGameMode<TConfig>).FullName, "StartGame");
            }

            harmony.Patch(original, prefix: new HarmonyMethod(startGamePrefix));
        }

        private static void StartGamePrefix(object __instance)
        {
            if (!IsServer())
            {
                return;
            }

            GameManager gameManager = GetFieldValue<GameManager>(__instance, "GameManager");
            if (gameManager == null || gameManager.Phase != GamePhase.Replay)
            {
                return;
            }

            ReplayManager replayManager = GetFieldValue<ReplayManager>(__instance, "ReplayManager");
            if (replayManager == null || replayManager.ReplayPlayer == null || !replayManager.ReplayPlayer.IsReplaying)
            {
                return;
            }

            replayManager.Server_StopReplaying();
            Debug.Log("B897 Server-Side Fixes: stopped active replay before restarting game.");
        }

        private static T GetFieldValue<T>(object instance, string fieldName) where T : class
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(instance.GetType(), fieldName);
            return field?.GetValue(instance) as T;
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
