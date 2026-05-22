using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace B897ServerSideFixes
{
    internal static class FaceOffPositionDoubleClickPatch
    {
        private static readonly MethodInfo playerRequestPositionPrefix = AccessTools.Method(typeof(FaceOffPositionDoubleClickPatch), nameof(OnPlayerRequestPositionPrefix));

        public static void Apply(Harmony harmony)
        {
            PatchOnPlayerRequestPosition<PublicGameModeConfig>(harmony);
            PatchOnPlayerRequestPosition<CompetitiveGameModeConfig>(harmony);
        }

        private static void PatchOnPlayerRequestPosition<TConfig>(Harmony harmony) where TConfig : StandardGameModeConfig, new()
        {
            MethodInfo original = AccessTools.Method(typeof(StandardGameMode<TConfig>), "OnPlayerRequestPosition");
            if (original == null)
            {
                throw new MissingMethodException(typeof(StandardGameMode<TConfig>).FullName, "OnPlayerRequestPosition");
            }

            harmony.Patch(original, prefix: new HarmonyMethod(playerRequestPositionPrefix));
        }

        private static bool OnPlayerRequestPositionPrefix(object __instance, Player player, PlayerPosition position)
        {
            if (!IsServer())
            {
                return true;
            }

            if (player == null || position == null)
            {
                return true;
            }

            GameManager gameManager = GetGameManager(__instance);
            if (gameManager == null || gameManager.Phase != GamePhase.FaceOff)
            {
                return true;
            }

            if (position.IsClaimed && position.ClaimedByPlayer == player)
            {
                Debug.Log(string.Format(
                    "B897 Server-Side Fixes: ignored duplicate FaceOff position click for player {0} on position {1}.",
                    player.OwnerClientId,
                    position.Name));
                return false;
            }

            return true;
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }

        private static GameManager GetGameManager(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo gameManagerField = AccessTools.Field(instance.GetType(), "GameManager");
            return gameManagerField?.GetValue(instance) as GameManager;
        }
    }
}
