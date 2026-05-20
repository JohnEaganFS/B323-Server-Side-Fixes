using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ToasterPerformance
{
	public static class PatchReplayDiagnostics
	{
		private const int MaxDetailedPuckMoveLogs = 25;
		private const int PuckMoveSummaryInterval = 100;

		private static readonly AccessTools.FieldRef<ReplayPlayer, Dictionary<ulong, ulong>> s_replayPuckNetworkObjectIdMap =
			AccessTools.FieldRefAccess<ReplayPlayer, Dictionary<ulong, ulong>>("replayPuckNetworkObjectIdMap");

		private static int s_replaySessionId;
		private static int s_puckMoveEvents;
		private static int s_puckMoveResolved;
		private static int s_puckMoveMissingMap;
		private static int s_puckMoveMissingReplayPuck;
		private static int s_puckMoveDetailedLogs;

		[HarmonyPatch(typeof(ReplayPlayer), "Server_StartReplay")]
		public static class PatchReplayPlayerStartReplay
		{
			[HarmonyPrefix]
			public static void Prefix(SortedList<int, List<ValueTuple<string, object>>> tickEventMap, int tickRate, int fromTick)
			{
				if (!IsServer())
				{
					return;
				}

				s_replaySessionId++;
				ResetPuckMoveCounters();

				EventCounts totalCounts = CountEvents(tickEventMap, int.MinValue, int.MaxValue);
				int minTick = tickEventMap.Count > 0 ? tickEventMap.Keys[0] : 0;
				int maxTick = tickEventMap.Count > 0 ? tickEventMap.Keys[tickEventMap.Count - 1] : 0;
				int clampedFromTick = tickEventMap.Count > 0 ? Mathf.Clamp(fromTick, minTick, maxTick) : fromTick;
				EventCounts replayWindowCounts = CountEvents(tickEventMap, clampedFromTick, int.MaxValue);

				Plugin.Log(string.Format(
					"ReplayDiag #{0}: Server_StartReplay tickRate={1}, requestedFromTick={2}, clampedFromTick={3}, minTick={4}, maxTick={5}, tickKeys={6}. Total events: puckSpawn={7}, puckMove={8}, puckDespawn={9}, playerSpawn={10}, bodyMove={11}, stickMove={12}. Replay window: puckSpawn={13}, puckMove={14}, puckDespawn={15}.",
					s_replaySessionId,
					tickRate,
					fromTick,
					clampedFromTick,
					minTick,
					maxTick,
					tickEventMap.Count,
					totalCounts.PuckSpawned,
					totalCounts.PuckMove,
					totalCounts.PuckDespawned,
					totalCounts.PlayerSpawned,
					totalCounts.PlayerBodyMove,
					totalCounts.StickMove,
					replayWindowCounts.PuckSpawned,
					replayWindowCounts.PuckMove,
					replayWindowCounts.PuckDespawned));

				LogPuckCounts("Server_StartReplay prefix");
			}
		}

		[HarmonyPatch(typeof(ReplayPlayer), "Server_StopReplay")]
		public static class PatchReplayPlayerStopReplay
		{
			[HarmonyPrefix]
			public static void Prefix()
			{
				if (!IsServer())
				{
					return;
				}

				Plugin.Log(string.Format(
					"ReplayDiag #{0}: Server_StopReplay summary: puckMoveEvents={1}, resolved={2}, missingMap={3}, missingReplayPuck={4}.",
					s_replaySessionId,
					s_puckMoveEvents,
					s_puckMoveResolved,
					s_puckMoveMissingMap,
					s_puckMoveMissingReplayPuck));

				LogPuckCounts("Server_StopReplay prefix");
			}
		}

		[HarmonyPatch(typeof(ReplayPlayer), "Server_ReplayEvent")]
		public static class PatchReplayPlayerReplayEvent
		{
			[HarmonyPrefix]
			public static void Prefix(ReplayPlayer __instance, string eventName, object eventData)
			{
				if (!IsServer())
				{
					return;
				}

				if (eventName == "PuckMove")
				{
					LogPuckMoveResolution(__instance, (ReplayPuckMove)eventData);
					return;
				}

				if (eventName == "PuckSpawned" && s_puckMoveDetailedLogs < MaxDetailedPuckMoveLogs)
				{
					ReplayPuckSpawned puckSpawned = (ReplayPuckSpawned)eventData;
					Plugin.Log(string.Format(
						"ReplayDiag #{0}: replaying PuckSpawned originalId={1}, position={2}, rotation={3}.",
						s_replaySessionId,
						puckSpawned.NetworkObjectId,
						puckSpawned.Position,
						puckSpawned.Rotation));
				}
			}

			[HarmonyPostfix]
			public static void Postfix(ReplayPlayer __instance, string eventName, object eventData)
			{
				if (!IsServer())
				{
					return;
				}

				if (eventName != "PuckSpawned")
				{
					return;
				}

				ReplayPuckSpawned puckSpawned = (ReplayPuckSpawned)eventData;
				Dictionary<ulong, ulong> replayPuckMap = s_replayPuckNetworkObjectIdMap(__instance);
				ulong replayNetworkObjectId;
				if (!replayPuckMap.TryGetValue(puckSpawned.NetworkObjectId, out replayNetworkObjectId))
				{
					Plugin.LogWarning(string.Format(
						"ReplayDiag #{0}: after PuckSpawned originalId={1}, replay puck map has no entry.",
						s_replaySessionId,
						puckSpawned.NetworkObjectId));
					return;
				}

				Puck replayPuck = MonoBehaviourSingleton<PuckManager>.Instance.GetReplayPuckByNetworkObjectId(replayNetworkObjectId);
				Plugin.Log(string.Format(
					"ReplayDiag #{0}: after PuckSpawned originalId={1}, replayId={2}, lookupFound={3}.",
					s_replaySessionId,
					puckSpawned.NetworkObjectId,
					replayNetworkObjectId,
					replayPuck ? "yes" : "no"));
				LogPuckCounts("PuckSpawned postfix");
			}
		}

		private static void LogPuckMoveResolution(ReplayPlayer replayPlayer, ReplayPuckMove puckMove)
		{
			s_puckMoveEvents++;

			Dictionary<ulong, ulong> replayPuckMap = s_replayPuckNetworkObjectIdMap(replayPlayer);
			ulong replayNetworkObjectId;
			if (!replayPuckMap.TryGetValue(puckMove.NetworkObjectId, out replayNetworkObjectId))
			{
				s_puckMoveMissingMap++;
				LogDetailedPuckMoveWarning(string.Format(
					"ReplayDiag #{0}: PuckMove originalId={1} has no replay map entry. position={2}",
					s_replaySessionId,
					puckMove.NetworkObjectId,
					puckMove.Position));
				LogPeriodicPuckMoveSummary();
				return;
			}

			Puck replayPuck = MonoBehaviourSingleton<PuckManager>.Instance.GetReplayPuckByNetworkObjectId(replayNetworkObjectId);
			if (!replayPuck)
			{
				s_puckMoveMissingReplayPuck++;
				LogDetailedPuckMoveWarning(string.Format(
					"ReplayDiag #{0}: PuckMove originalId={1} mappedReplayId={2}, but GetReplayPuckByNetworkObjectId returned null. intendedPosition={3}",
					s_replaySessionId,
					puckMove.NetworkObjectId,
					replayNetworkObjectId,
					puckMove.Position));
				LogPuckCounts("missing replay puck during PuckMove");
				LogPeriodicPuckMoveSummary();
				return;
			}

			s_puckMoveResolved++;
			if (s_puckMoveDetailedLogs < MaxDetailedPuckMoveLogs)
			{
				s_puckMoveDetailedLogs++;
				Plugin.Log(string.Format(
					"ReplayDiag #{0}: PuckMove resolved originalId={1}, replayId={2}, currentPosition={3}, intendedPosition={4}.",
					s_replaySessionId,
					puckMove.NetworkObjectId,
					replayNetworkObjectId,
					replayPuck.transform.position,
					puckMove.Position));
			}

			LogPeriodicPuckMoveSummary();
		}

		private static void LogDetailedPuckMoveWarning(string message)
		{
			if (s_puckMoveDetailedLogs >= MaxDetailedPuckMoveLogs)
			{
				return;
			}

			s_puckMoveDetailedLogs++;
			Plugin.LogWarning(message);
		}

		private static void LogPeriodicPuckMoveSummary()
		{
			if (s_puckMoveEvents % PuckMoveSummaryInterval != 0)
			{
				return;
			}

			Plugin.Log(string.Format(
				"ReplayDiag #{0}: PuckMove progress events={1}, resolved={2}, missingMap={3}, missingReplayPuck={4}.",
				s_replaySessionId,
				s_puckMoveEvents,
				s_puckMoveResolved,
				s_puckMoveMissingMap,
				s_puckMoveMissingReplayPuck));
		}

		private static void LogPuckCounts(string context)
		{
			PuckManager puckManager = MonoBehaviourSingleton<PuckManager>.Instance;
			int cachedAll = SafeCount(() => puckManager.GetPucks(true));
			int cachedNoReplay = SafeCount(() => puckManager.GetPucks(false));
			int cachedReplay = SafeCount(() => puckManager.GetReplayPucks());

			int liveAll = 0;
			int liveReplay = 0;
			int liveNoReplay = 0;
			foreach (Puck puck in UnityEngine.Object.FindObjectsByType<Puck>(FindObjectsSortMode.None))
			{
				if (!puck)
				{
					continue;
				}

				liveAll++;
				if (puck.IsReplay != null && puck.IsReplay.Value)
				{
					liveReplay++;
				}
				else
				{
					liveNoReplay++;
				}
			}

			Plugin.Log(string.Format(
				"ReplayDiag #{0}: puck counts at {1}: managerAll={2}, managerNoReplay={3}, managerReplay={4}, liveAll={5}, liveNoReplay={6}, liveReplay={7}.",
				s_replaySessionId,
				context,
				cachedAll,
				cachedNoReplay,
				cachedReplay,
				liveAll,
				liveNoReplay,
				liveReplay));
		}

		private static int SafeCount(Func<List<Puck>> getPucks)
		{
			try
			{
				List<Puck> pucks = getPucks();
				return pucks == null ? -1 : pucks.Count;
			}
			catch (Exception exception)
			{
				Plugin.LogWarning("ReplayDiag: failed to count manager pucks: " + exception.Message);
				return -1;
			}
		}

		private static EventCounts CountEvents(SortedList<int, List<ValueTuple<string, object>>> tickEventMap, int minTickInclusive, int maxTickInclusive)
		{
			EventCounts counts = new EventCounts();
			foreach (KeyValuePair<int, List<ValueTuple<string, object>>> tickEvents in tickEventMap)
			{
				if (tickEvents.Key < minTickInclusive || tickEvents.Key > maxTickInclusive)
				{
					continue;
				}

				foreach (ValueTuple<string, object> tickEvent in tickEvents.Value)
				{
					counts.Total++;
					switch (tickEvent.Item1)
					{
						case "PuckSpawned":
							counts.PuckSpawned++;
							break;
						case "PuckMove":
							counts.PuckMove++;
							break;
						case "PuckDespawned":
							counts.PuckDespawned++;
							break;
						case "PlayerSpawned":
							counts.PlayerSpawned++;
							break;
						case "PlayerBodyMove":
							counts.PlayerBodyMove++;
							break;
						case "StickMove":
							counts.StickMove++;
							break;
					}
				}
			}

			return counts;
		}

		private static void ResetPuckMoveCounters()
		{
			s_puckMoveEvents = 0;
			s_puckMoveResolved = 0;
			s_puckMoveMissingMap = 0;
			s_puckMoveMissingReplayPuck = 0;
			s_puckMoveDetailedLogs = 0;
		}

		private static bool IsServer()
		{
			return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
		}

		private struct EventCounts
		{
			public int Total;
			public int PuckSpawned;
			public int PuckMove;
			public int PuckDespawned;
			public int PlayerSpawned;
			public int PlayerBodyMove;
			public int StickMove;
		}
	}
}
