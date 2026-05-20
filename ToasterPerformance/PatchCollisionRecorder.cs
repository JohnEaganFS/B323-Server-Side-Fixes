using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace ToasterPerformance
{
	// Token: 0x02000003 RID: 3
	public static class PatchCollisionRecorder
	{
		// Token: 0x06000002 RID: 2 RVA: 0x000020A4 File Offset: 0x000002A4
		private static PatchCollisionRecorder.CacheEntry GetOrCreate(NetworkObjectCollisionRecorder recorder)
		{
			PatchCollisionRecorder.CacheEntry cacheEntry;
			if (!PatchCollisionRecorder.s_cache.TryGetValue(recorder, out cacheEntry))
			{
				cacheEntry = new PatchCollisionRecorder.CacheEntry();
				PatchCollisionRecorder.s_cache[recorder] = cacheEntry;
			}
			return cacheEntry;
		}

		// Token: 0x04000009 RID: 9
		private static readonly Dictionary<NetworkObjectCollisionRecorder, PatchCollisionRecorder.CacheEntry> s_cache = new Dictionary<NetworkObjectCollisionRecorder, PatchCollisionRecorder.CacheEntry>();

		// Token: 0x0200000F RID: 15
		private sealed class CacheEntry
		{
			// Token: 0x04000036 RID: 54
			public readonly List<NetworkObjectCollision> List = new List<NetworkObjectCollision>(8);

			// Token: 0x04000037 RID: 55
			public bool Dirty = true;
		}

		// Token: 0x02000010 RID: 16
		[HarmonyPatch(typeof(NetworkObjectCollisionRecorder), "NetworkObjectCollisions", MethodType.Getter)]
		public static class PatchGetter
		{
			// Token: 0x06000023 RID: 35 RVA: 0x00002AB8 File Offset: 0x00000CB8
			[HarmonyPrefix]
			public static bool Prefix(NetworkObjectCollisionRecorder __instance, ref List<NetworkObjectCollision> __result)
			{
				PatchCollisionRecorder.CacheEntry orCreate = PatchCollisionRecorder.GetOrCreate(__instance);
				if (orCreate.Dirty)
				{
					orCreate.List.Clear();
					if (__instance.Buffer != null)
					{
						NativeArray<NetworkObjectCollision>.ReadOnly readOnly = __instance.Buffer.AsNativeArray();
						for (int i = 0; i < readOnly.Length; i++)
						{
							orCreate.List.Add(readOnly[i]);
						}
					}
					orCreate.Dirty = false;
				}
				__result = orCreate.List;
				return false;
			}
		}

		// Token: 0x02000011 RID: 17
		[HarmonyPatch]
		public static class PatchOnBufferChanged
		{
			// Token: 0x06000024 RID: 36 RVA: 0x00002B28 File Offset: 0x00000D28
			public static MethodBase TargetMethod()
			{
				return AccessTools.Method(typeof(NetworkObjectCollisionRecorder), "OnBufferChanged", null, null);
			}

			// Token: 0x06000025 RID: 37 RVA: 0x00002B40 File Offset: 0x00000D40
			[HarmonyPostfix]
			public static void Postfix(NetworkObjectCollisionRecorder __instance)
			{
				PatchCollisionRecorder.CacheEntry cacheEntry;
				if (PatchCollisionRecorder.s_cache.TryGetValue(__instance, out cacheEntry))
				{
					cacheEntry.Dirty = true;
				}
			}
		}

		// Token: 0x02000012 RID: 18
		[HarmonyPatch(typeof(NetworkObjectCollisionRecorder), "OnNetworkDespawn")]
		public static class PatchDespawn
		{
			// Token: 0x06000026 RID: 38 RVA: 0x00002B63 File Offset: 0x00000D63
			[HarmonyPostfix]
			public static void Postfix(NetworkObjectCollisionRecorder __instance)
			{
				PatchCollisionRecorder.s_cache.Remove(__instance);
			}
		}

		// Token: 0x02000013 RID: 19
		[HarmonyPatch(typeof(PuckManager), "GetPlayerPuck")]
		public static class PatchGetPlayerPuck
		{
			// Token: 0x06000027 RID: 39 RVA: 0x00002B74 File Offset: 0x00000D74
			[HarmonyPrefix]
			public static bool Prefix(ulong clientId, ref Puck __result)
			{
				Player playerByClientId = MonoBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
				if (playerByClientId == null || !playerByClientId)
				{
					__result = null;
					return false;
				}
				if (playerByClientId.Stick == null || !playerByClientId.Stick)
				{
					__result = null;
					return false;
				}
				NetworkObjectCollisionRecorder networkObjectCollisionRecorder = playerByClientId.Stick.NetworkObjectCollisionRecorder;
				if (networkObjectCollisionRecorder == null || networkObjectCollisionRecorder.Buffer == null)
				{
					__result = null;
					return false;
				}
				NativeArray<NetworkObjectCollision>.ReadOnly readOnly = networkObjectCollisionRecorder.Buffer.AsNativeArray();
				if (readOnly.Length == 0)
				{
					__result = null;
					return false;
				}
				NetworkObjectReference networkObjectReference = readOnly[readOnly.Length - 1].NetworkObjectReference;
				__result = NetworkingUtils.GetPuckFromNetworkObjectReference(networkObjectReference);
				return false;
			}
		}
	}
}
