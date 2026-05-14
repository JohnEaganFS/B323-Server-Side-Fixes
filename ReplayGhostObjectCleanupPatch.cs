using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace B323ServerSideFixes
{
    internal static class ReplayGhostObjectCleanupPatch
    {
        private static readonly MethodInfo spawnPlayerBodyPrefix = AccessTools.Method(typeof(ReplayGhostObjectCleanupPatch), nameof(ServerSpawnPlayerBodyPrefix));
        private static readonly MethodInfo spawnStickPrefix = AccessTools.Method(typeof(ReplayGhostObjectCleanupPatch), nameof(ServerSpawnStickPrefix));
        private static readonly MethodInfo stopReplayPrefix = AccessTools.Method(typeof(ReplayGhostObjectCleanupPatch), nameof(ServerStopReplayPrefix));
        private static readonly MethodInfo stopReplayPostfix = AccessTools.Method(typeof(ReplayGhostObjectCleanupPatch), nameof(ServerStopReplayPostfix));

        private static HashSet<ulong> replayOwnerClientIds = new HashSet<ulong>();

        public static void Apply(Harmony harmony)
        {
            MethodInfo spawnStick = AccessTools.Method(typeof(Player), "Server_SpawnStick", new[]
            {
                typeof(Vector3),
                typeof(Quaternion),
                typeof(PlayerRole)
            });
            MethodInfo spawnPlayerBody = AccessTools.Method(typeof(Player), "Server_SpawnPlayerBody", new[]
            {
                typeof(Vector3),
                typeof(Quaternion),
                typeof(PlayerRole)
            });
            if (spawnPlayerBody == null)
            {
                throw new MissingMethodException(typeof(Player).FullName, "Server_SpawnPlayerBody");
            }

            if (spawnStick == null)
            {
                throw new MissingMethodException(typeof(Player).FullName, "Server_SpawnStick");
            }

            MethodInfo stopReplay = AccessTools.Method(typeof(ReplayPlayer), "Server_StopReplay");
            if (stopReplay == null)
            {
                throw new MissingMethodException(typeof(ReplayPlayer).FullName, "Server_StopReplay");
            }

            harmony.Patch(spawnPlayerBody, prefix: new HarmonyMethod(spawnPlayerBodyPrefix));
            harmony.Patch(spawnStick, prefix: new HarmonyMethod(spawnStickPrefix));
            harmony.Patch(stopReplay, prefix: new HarmonyMethod(stopReplayPrefix), postfix: new HarmonyMethod(stopReplayPostfix));
        }

        private static void ServerSpawnPlayerBodyPrefix(Player __instance)
        {
            if (!IsReplayPlayer(__instance) || !__instance.PlayerBody || !__instance.PlayerBody.NetworkObject.IsSpawned)
            {
                return;
            }

            __instance.PlayerBody.transform.DOKill(false);
            __instance.Server_DespawnPlayerBody();
        }

        private static void ServerSpawnStickPrefix(Player __instance)
        {
            if (!IsReplayPlayer(__instance) || !__instance.Stick || !__instance.Stick.NetworkObject.IsSpawned)
            {
                return;
            }

            __instance.Stick.transform.DOKill(false);
            __instance.Server_DespawnStick();
        }

        private static void ServerStopReplayPrefix()
        {
            replayOwnerClientIds = new HashSet<ulong>();
            foreach (Player player in MonoBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayers())
            {
                replayOwnerClientIds.Add(player.OwnerClientId);
            }
        }

        private static void ServerStopReplayPostfix()
        {
            int despawnedBodyCount = DespawnReplayOwnedObjects<PlayerBody>(ShouldDespawnReplayPlayerBody);
            int despawnedCameraCount = DespawnReplayOwnedObjects<PlayerCamera>(ShouldDespawnReplayPlayerCamera);
            int despawnedStickPositionerCount = DespawnReplayOwnedObjects<StickPositioner>(ShouldDespawnReplayStickPositioner);
            int despawnedStickCount = 0;
            foreach (Stick stick in UnityEngine.Object.FindObjectsByType<Stick>(FindObjectsSortMode.None))
            {
                if (!ShouldDespawnReplayStick(stick))
                {
                    continue;
                }

                stick.transform.DOKill(false);
                if (stick.NetworkObject && stick.NetworkObject.IsSpawned)
                {
                    stick.NetworkObject.Despawn(true);
                    despawnedStickCount++;
                }
            }
            int despawnedPuckCount = DespawnReplayOwnedObjects<Puck>(ShouldDespawnReplayPuck);

            int despawnedObjectCount = despawnedBodyCount + despawnedCameraCount + despawnedStickPositionerCount + despawnedStickCount + despawnedPuckCount;
            if (despawnedObjectCount > 0)
            {
                Debug.Log(string.Format(
                    "B323 Server-Side Fixes: cleaned up orphaned replay objects. bodies={0}, cameras={1}, stickPositioners={2}, sticks={3}, pucks={4}",
                    despawnedBodyCount,
                    despawnedCameraCount,
                    despawnedStickPositionerCount,
                    despawnedStickCount,
                    despawnedPuckCount));
            }

            replayOwnerClientIds.Clear();
        }

        private static int DespawnReplayOwnedObjects<T>(Func<T, bool> shouldDespawn) where T : NetworkBehaviour
        {
            int despawnedCount = 0;
            foreach (T instance in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (!shouldDespawn(instance))
                {
                    continue;
                }

                instance.transform.DOKill(false);
                if (instance.NetworkObject && instance.NetworkObject.IsSpawned)
                {
                    instance.NetworkObject.Despawn(true);
                    despawnedCount++;
                }
            }

            return despawnedCount;
        }

        private static bool ShouldDespawnReplayPlayerBody(PlayerBody playerBody)
        {
            if (!playerBody)
            {
                return false;
            }

            Player player = playerBody.Player;
            if (IsReplayPlayer(player))
            {
                return true;
            }

            return replayOwnerClientIds.Contains(playerBody.OwnerClientId);
        }

        private static bool ShouldDespawnReplayPlayerCamera(PlayerCamera playerCamera)
        {
            if (!playerCamera)
            {
                return false;
            }

            Player player = playerCamera.Player;
            if (IsReplayPlayer(player))
            {
                return true;
            }

            return replayOwnerClientIds.Contains(playerCamera.OwnerClientId);
        }

        private static bool ShouldDespawnReplayStickPositioner(StickPositioner stickPositioner)
        {
            if (!stickPositioner)
            {
                return false;
            }

            Player player = stickPositioner.Player;
            if (IsReplayPlayer(player))
            {
                return true;
            }

            return replayOwnerClientIds.Contains(stickPositioner.OwnerClientId);
        }

        private static bool ShouldDespawnReplayStick(Stick stick)
        {
            if (!stick)
            {
                return false;
            }

            Player player = stick.Player;
            if (IsReplayPlayer(player))
            {
                return true;
            }

            return replayOwnerClientIds.Contains(stick.OwnerClientId);
        }

        private static bool ShouldDespawnReplayPuck(Puck puck)
        {
            return puck && puck.IsReplay != null && puck.IsReplay.Value;
        }

        private static bool IsReplayPlayer(Player player)
        {
            return player != null && player.IsReplay != null && player.IsReplay.Value;
        }
    }
}
