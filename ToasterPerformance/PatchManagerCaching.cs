using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToasterPerformance
{
	// Token: 0x02000006 RID: 6
	public static class PatchManagerCaching
	{
		private const int MaxDetailedPuckCacheLogs = 80;
		private const int PuckCacheSummaryInterval = 100;

		private static int s_puckCacheLogCount;
		private static int s_getPucksCalls;
		private static int s_getReplayPucksCalls;
		private static int s_puckCacheRebuilds;

		// Token: 0x0600000B RID: 11 RVA: 0x000021E0 File Offset: 0x000003E0
		private static bool ValidatePlayerCache()
		{
			List<Player> list = PatchManagerCaching.s_cachedPlayersAll;
			for (int i = 0; i < list.Count; i++)
			{
				Player player = list[i];
				if (player == null || player.NetworkObject == null || !player.NetworkObject.IsSpawned)
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x0600000C RID: 12 RVA: 0x00002234 File Offset: 0x00000434
		private static void RebuildPlayerCache(PlayerManager mgr)
		{
			List<Player> list = PatchManagerCaching.s_playersField.Invoke(mgr);
			List<Player> list2 = new List<Player>(list.Count);
			List<Player> list3 = new List<Player>(list.Count);
			List<Player> list4 = new List<Player>(4);
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Player player = list[i];
				if (!(player == null) && !(player.NetworkObject == null) && player.NetworkObject.IsSpawned)
				{
					list[num++] = player;
					list2.Add(player);
					if (player.IsReplay.Value)
					{
						list4.Add(player);
					}
					else
					{
						list3.Add(player);
					}
				}
			}
			if (num < list.Count)
			{
				list.RemoveRange(num, list.Count - num);
			}
			PatchManagerCaching.s_cachedPlayersAll = list2;
			PatchManagerCaching.s_cachedPlayersNoReplay = list3;
			PatchManagerCaching.s_cachedReplayPlayers = list4;
			PatchManagerCaching.s_playersDirty = false;
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00002320 File Offset: 0x00000520
		private static bool ValidatePuckCache()
		{
			List<Puck> list = PatchManagerCaching.s_cachedPucksAll;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] == null)
				{
					return false;
				}
			}
			return true;
		}

		private static bool PuckCacheMatchesManager(PuckManager mgr)
		{
			List<Puck> rawPucks = PatchManagerCaching.s_pucksField.Invoke(mgr);
			if (rawPucks == null)
			{
				return PatchManagerCaching.s_cachedPucksAll.Count == 0
					&& PatchManagerCaching.s_cachedPucksNoReplay.Count == 0
					&& PatchManagerCaching.s_cachedReplayPucks.Count == 0;
			}

			int rawAll = 0;
			int rawNoReplay = 0;
			int rawReplay = 0;
			for (int i = 0; i < rawPucks.Count; i++)
			{
				Puck puck = rawPucks[i];
				if (!puck)
				{
					continue;
				}

				if (rawAll >= PatchManagerCaching.s_cachedPucksAll.Count || PatchManagerCaching.s_cachedPucksAll[rawAll] != puck)
				{
					return false;
				}

				rawAll++;
				if (puck.IsReplay != null && puck.IsReplay.Value)
				{
					rawReplay++;
				}
				else
				{
					rawNoReplay++;
				}
			}

			return rawAll == PatchManagerCaching.s_cachedPucksAll.Count
				&& rawNoReplay == PatchManagerCaching.s_cachedPucksNoReplay.Count
				&& rawReplay == PatchManagerCaching.s_cachedReplayPucks.Count;
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00002358 File Offset: 0x00000558
		private static void RebuildPuckCache(PuckManager mgr)
		{
			List<Puck> list = PatchManagerCaching.s_pucksField.Invoke(mgr);
			int rawCountBefore = list.Count;
			List<Puck> list2 = new List<Puck>(list.Count);
			List<Puck> list3 = new List<Puck>(list.Count);
			List<Puck> list4 = new List<Puck>(4);
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Puck puck = list[i];
				if (!(puck == null))
				{
					list[num++] = puck;
					list2.Add(puck);
					if (puck.IsReplay.Value)
					{
						list4.Add(puck);
					}
					else
					{
						list3.Add(puck);
					}
				}
			}
			if (num < list.Count)
			{
				list.RemoveRange(num, list.Count - num);
			}
			PatchManagerCaching.s_cachedPucksAll = list2;
			PatchManagerCaching.s_cachedPucksNoReplay = list3;
			PatchManagerCaching.s_cachedReplayPucks = list4;
			PatchManagerCaching.s_pucksDirty = false;
			PatchManagerCaching.s_puckCacheRebuilds++;
			PatchManagerCaching.LogPuckCacheState(
				"RebuildPuckCache rawBefore=" + rawCountBefore + " rawAfter=" + list.Count,
				mgr,
				true);
		}

		private static void LogPuckCacheState(string context, PuckManager mgr, bool forceDetailed = false)
		{
			if (!forceDetailed && PatchManagerCaching.s_puckCacheLogCount >= MaxDetailedPuckCacheLogs)
			{
				return;
			}

			List<Puck> rawPucks = PatchManagerCaching.s_pucksField.Invoke(mgr);
			int rawAll = rawPucks == null ? -1 : rawPucks.Count;
			int rawNoReplay = 0;
			int rawReplay = 0;
			if (rawPucks != null)
			{
				for (int i = 0; i < rawPucks.Count; i++)
				{
					Puck puck = rawPucks[i];
					if (!puck)
					{
						continue;
					}

					if (puck.IsReplay != null && puck.IsReplay.Value)
					{
						rawReplay++;
					}
					else
					{
						rawNoReplay++;
					}
				}
			}

			int liveAll = 0;
			int liveNoReplay = 0;
			int liveReplay = 0;
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

			PatchManagerCaching.s_puckCacheLogCount++;
			Plugin.Log(string.Format(
				"PuckCacheDiag: {0}: dirty={1}, rawAll={2}, rawNoReplay={3}, rawReplay={4}, cachedAll={5}, cachedNoReplay={6}, cachedReplay={7}, liveAll={8}, liveNoReplay={9}, liveReplay={10}, getPucksCalls={11}, getReplayCalls={12}, rebuilds={13}.",
				context,
				PatchManagerCaching.s_pucksDirty,
				rawAll,
				rawNoReplay,
				rawReplay,
				PatchManagerCaching.s_cachedPucksAll.Count,
				PatchManagerCaching.s_cachedPucksNoReplay.Count,
				PatchManagerCaching.s_cachedReplayPucks.Count,
				liveAll,
				liveNoReplay,
				liveReplay,
				PatchManagerCaching.s_getPucksCalls,
				PatchManagerCaching.s_getReplayPucksCalls,
				PatchManagerCaching.s_puckCacheRebuilds));
		}

		private static void LogPeriodicPuckCacheSummary(string context, PuckManager mgr, int callCount)
		{
			if (callCount % PuckCacheSummaryInterval != 0)
			{
				return;
			}

			PatchManagerCaching.LogPuckCacheState(context + " periodic", mgr, true);
		}

		// Token: 0x0400000C RID: 12
		private static readonly AccessTools.FieldRef<PlayerManager, List<Player>> s_playersField = AccessTools.FieldRefAccess<PlayerManager, List<Player>>("players");

		// Token: 0x0400000D RID: 13
		private static List<Player> s_cachedPlayersAll = new List<Player>(16);

		// Token: 0x0400000E RID: 14
		private static List<Player> s_cachedPlayersNoReplay = new List<Player>(16);

		// Token: 0x0400000F RID: 15
		private static List<Player> s_cachedReplayPlayers = new List<Player>(4);

		// Token: 0x04000010 RID: 16
		private static bool s_playersDirty = true;

		// Token: 0x04000011 RID: 17
		private static readonly AccessTools.FieldRef<PuckManager, List<Puck>> s_pucksField = AccessTools.FieldRefAccess<PuckManager, List<Puck>>("pucks");

		// Token: 0x04000012 RID: 18
		private static List<Puck> s_cachedPucksAll = new List<Puck>(8);

		// Token: 0x04000013 RID: 19
		private static List<Puck> s_cachedPucksNoReplay = new List<Puck>(8);

		// Token: 0x04000014 RID: 20
		private static List<Puck> s_cachedReplayPucks = new List<Puck>(4);

		// Token: 0x04000015 RID: 21
		private static bool s_pucksDirty = true;

		// Token: 0x02000017 RID: 23
		[HarmonyPatch(typeof(PlayerManager), "AddPlayer")]
		public static class PatchAddPlayer
		{
			// Token: 0x0600002B RID: 43 RVA: 0x00002C42 File Offset: 0x00000E42
			[HarmonyPostfix]
			public static void Postfix()
			{
				PatchManagerCaching.s_playersDirty = true;
			}
		}

		// Token: 0x02000018 RID: 24
		[HarmonyPatch(typeof(PlayerManager), "RemovePlayer")]
		public static class PatchRemovePlayer
		{
			// Token: 0x0600002C RID: 44 RVA: 0x00002C4A File Offset: 0x00000E4A
			[HarmonyPostfix]
			public static void Postfix()
			{
				PatchManagerCaching.s_playersDirty = true;
			}
		}

		// Token: 0x02000019 RID: 25
		[HarmonyPatch(typeof(PlayerManager), "GetPlayers")]
		public static class PatchGetPlayers
		{
			// Token: 0x0600002D RID: 45 RVA: 0x00002C52 File Offset: 0x00000E52
			[HarmonyPrefix]
			public static bool Prefix(PlayerManager __instance, bool includeReplay, ref List<Player> __result)
			{
				if (!PatchManagerCaching.s_playersDirty && !PatchManagerCaching.ValidatePlayerCache())
				{
					PatchManagerCaching.s_playersDirty = true;
				}
				if (PatchManagerCaching.s_playersDirty)
				{
					PatchManagerCaching.RebuildPlayerCache(__instance);
				}
				__result = (includeReplay ? PatchManagerCaching.s_cachedPlayersAll : PatchManagerCaching.s_cachedPlayersNoReplay);
				return false;
			}
		}

		// Token: 0x0200001A RID: 26
		[HarmonyPatch(typeof(PlayerManager), "GetReplayPlayers")]
		public static class PatchGetReplayPlayers
		{
			// Token: 0x0600002E RID: 46 RVA: 0x00002C87 File Offset: 0x00000E87
			[HarmonyPrefix]
			public static bool Prefix(PlayerManager __instance, ref List<Player> __result)
			{
				if (!PatchManagerCaching.s_playersDirty && !PatchManagerCaching.ValidatePlayerCache())
				{
					PatchManagerCaching.s_playersDirty = true;
				}
				if (PatchManagerCaching.s_playersDirty)
				{
					PatchManagerCaching.RebuildPlayerCache(__instance);
				}
				__result = PatchManagerCaching.s_cachedReplayPlayers;
				return false;
			}
		}

		// Token: 0x0200001B RID: 27
		[HarmonyPatch(typeof(PuckManager), "AddPuck")]
		public static class PatchAddPuck
		{
			// Token: 0x0600002F RID: 47 RVA: 0x00002CB2 File Offset: 0x00000EB2
			[HarmonyPostfix]
			public static void Postfix(PuckManager __instance, Puck puck)
			{
				PatchManagerCaching.s_pucksDirty = true;
				PatchManagerCaching.LogPuckCacheState(
					string.Format("AddPuck postfix puckId={0}, isReplay={1}", puck ? puck.NetworkObjectId.ToString() : "null", puck && puck.IsReplay != null && puck.IsReplay.Value),
					__instance,
					true);
			}
		}

		// Token: 0x0200001C RID: 28
		[HarmonyPatch(typeof(PuckManager), "RemovePuck")]
		public static class PatchRemovePuck
		{
			// Token: 0x06000030 RID: 48 RVA: 0x00002CBA File Offset: 0x00000EBA
			[HarmonyPostfix]
			public static void Postfix(PuckManager __instance, Puck puck)
			{
				PatchManagerCaching.s_pucksDirty = true;
				PatchManagerCaching.LogPuckCacheState(
					string.Format("RemovePuck postfix puckId={0}, isReplay={1}", puck ? puck.NetworkObjectId.ToString() : "null", puck && puck.IsReplay != null && puck.IsReplay.Value),
					__instance,
					true);
			}
		}

		// Token: 0x0200001D RID: 29
		[HarmonyPatch(typeof(PuckManager), "GetPucks")]
		public static class PatchGetPucks
		{
			// Token: 0x06000031 RID: 49 RVA: 0x00002CC2 File Offset: 0x00000EC2
			[HarmonyPrefix]
			public static bool Prefix(PuckManager __instance, bool includeReplay, ref List<Puck> __result)
			{
				PatchManagerCaching.s_getPucksCalls++;
				if (!PatchManagerCaching.s_pucksDirty && (!PatchManagerCaching.ValidatePuckCache() || !PatchManagerCaching.PuckCacheMatchesManager(__instance)))
				{
					PatchManagerCaching.s_pucksDirty = true;
					PatchManagerCaching.LogPuckCacheState("GetPucks cache mismatch includeReplay=" + includeReplay, __instance, true);
				}
				if (PatchManagerCaching.s_pucksDirty)
				{
					PatchManagerCaching.LogPuckCacheState("GetPucks rebuilding includeReplay=" + includeReplay, __instance);
					PatchManagerCaching.RebuildPuckCache(__instance);
				}
				__result = (includeReplay ? PatchManagerCaching.s_cachedPucksAll : PatchManagerCaching.s_cachedPucksNoReplay);
				if (__result.Count == 0 || PatchManagerCaching.s_getPucksCalls <= 20)
				{
					PatchManagerCaching.LogPuckCacheState("GetPucks result includeReplay=" + includeReplay + " resultCount=" + __result.Count, __instance);
				}
				PatchManagerCaching.LogPeriodicPuckCacheSummary("GetPucks includeReplay=" + includeReplay + " resultCount=" + __result.Count, __instance, PatchManagerCaching.s_getPucksCalls);
				return false;
			}
		}

		// Token: 0x0200001E RID: 30
		[HarmonyPatch(typeof(PuckManager), "GetReplayPucks")]
		public static class PatchGetReplayPucks
		{
			// Token: 0x06000032 RID: 50 RVA: 0x00002CF7 File Offset: 0x00000EF7
			[HarmonyPrefix]
			public static bool Prefix(PuckManager __instance, ref List<Puck> __result)
			{
				PatchManagerCaching.s_getReplayPucksCalls++;
				if (!PatchManagerCaching.s_pucksDirty && (!PatchManagerCaching.ValidatePuckCache() || !PatchManagerCaching.PuckCacheMatchesManager(__instance)))
				{
					PatchManagerCaching.s_pucksDirty = true;
					PatchManagerCaching.LogPuckCacheState("GetReplayPucks cache mismatch", __instance, true);
				}
				if (PatchManagerCaching.s_pucksDirty)
				{
					PatchManagerCaching.LogPuckCacheState("GetReplayPucks rebuilding", __instance);
					PatchManagerCaching.RebuildPuckCache(__instance);
				}
				__result = PatchManagerCaching.s_cachedReplayPucks;
				if (__result.Count == 0 || PatchManagerCaching.s_getReplayPucksCalls <= 20)
				{
					PatchManagerCaching.LogPuckCacheState("GetReplayPucks resultCount=" + __result.Count, __instance);
				}
				PatchManagerCaching.LogPeriodicPuckCacheSummary("GetReplayPucks resultCount=" + __result.Count, __instance, PatchManagerCaching.s_getReplayPucksCalls);
				return false;
			}
		}
	}
}
