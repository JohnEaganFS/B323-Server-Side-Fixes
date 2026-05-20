using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ToasterPerformance
{
	// Token: 0x02000008 RID: 8
	public static class PatchSyncObjects
	{
		// Token: 0x06000012 RID: 18 RVA: 0x000025E8 File Offset: 0x000007E8
		private static void EnsureDictPopulated(SynchronizedObjectManager mgr)
		{
			List<SynchronizedObject> list = PatchSyncObjects.s_synchronizedObjects.Invoke(mgr);
			if (PatchSyncObjects.s_objectsById.Count == list.Count)
			{
				return;
			}
			PatchSyncObjects.s_objectsById.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				SynchronizedObject synchronizedObject = list[i];
				if (synchronizedObject != null)
				{
					PatchSyncObjects.s_objectsById[synchronizedObject.NetworkObjectId] = synchronizedObject;
				}
			}
		}

		// Token: 0x04000022 RID: 34
		private static readonly Dictionary<ulong, SynchronizedObject> s_objectsById = new Dictionary<ulong, SynchronizedObject>(64);

		// Token: 0x04000023 RID: 35
		private static readonly List<SynchronizedObjectData> s_gatherStaging = new List<SynchronizedObjectData>(64);

		// Token: 0x04000024 RID: 36
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, List<SynchronizedObject>> s_synchronizedObjects = AccessTools.FieldRefAccess<SynchronizedObjectManager, List<SynchronizedObject>>("synchronizedObjects");

		// Token: 0x04000025 RID: 37
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, double> s_serverLastSentServerTime = AccessTools.FieldRefAccess<SynchronizedObjectManager, double>("serverLastSentServerTime");

		// Token: 0x04000026 RID: 38
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, bool> s_clientHasReceivedFirstTick = AccessTools.FieldRefAccess<SynchronizedObjectManager, bool>("clientHasReceivedFirstTick");

		// Token: 0x04000027 RID: 39
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, SortedList<double, SynchronizedObjectsSnapshot>> s_snapshots = AccessTools.FieldRefAccess<SynchronizedObjectManager, SortedList<double, SynchronizedObjectsSnapshot>>("snapshots");

		// Token: 0x04000028 RID: 40
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, double> s_clientLocalTimeline = AccessTools.FieldRefAccess<SynchronizedObjectManager, double>("clientLocalTimeline");

		// Token: 0x04000029 RID: 41
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, double> s_clientLocalTimescale = AccessTools.FieldRefAccess<SynchronizedObjectManager, double>("clientLocalTimescale");

		// Token: 0x0400002A RID: 42
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, ExponentialMovingAverage> s_driftEma = AccessTools.FieldRefAccess<SynchronizedObjectManager, ExponentialMovingAverage>("driftEma");

		// Token: 0x0400002B RID: 43
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, ExponentialMovingAverage> s_deliveryTimeEma = AccessTools.FieldRefAccess<SynchronizedObjectManager, ExponentialMovingAverage>("deliveryTimeEma");

		// Token: 0x0400002C RID: 44
		private static readonly AccessTools.FieldRef<SynchronizedObjectManager, SnapshotInterpolationSettings> s_snapshotInterpolationSettings = AccessTools.FieldRefAccess<SynchronizedObjectManager, SnapshotInterpolationSettings>("snapshotInterpolationSettings");

		// Token: 0x0400002D RID: 45
		private static readonly FieldInfo s_eventsField = AccessTools.Field(typeof(EventManager), "events");

		// Token: 0x0400002E RID: 46
		private const string OnSynchronizeObjectsEventName = "Event_OnSynchronizeObjects";

		// Token: 0x02000024 RID: 36
		[HarmonyPatch(typeof(SynchronizedObjectManager), "AddSynchronizedObject")]
		public static class PatchAdd
		{
			// Token: 0x06000038 RID: 56 RVA: 0x0000318C File Offset: 0x0000138C
			[HarmonyPostfix]
			public static void Postfix(SynchronizedObject synchronizedObject)
			{
				if (synchronizedObject != null)
				{
					PatchSyncObjects.s_objectsById[synchronizedObject.NetworkObjectId] = synchronizedObject;
				}
			}
		}

		// Token: 0x02000025 RID: 37
		[HarmonyPatch(typeof(SynchronizedObjectManager), "RemoveSynchronizedObject")]
		public static class PatchRemove
		{
			// Token: 0x06000039 RID: 57 RVA: 0x000031A8 File Offset: 0x000013A8
			[HarmonyPostfix]
			public static void Postfix(SynchronizedObject synchronizedObject)
			{
				if (synchronizedObject != null)
				{
					PatchSyncObjects.s_objectsById.Remove(synchronizedObject.NetworkObjectId);
				}
			}
		}

		// Token: 0x02000026 RID: 38
		[HarmonyPatch]
		public static class PatchClear
		{
			// Token: 0x0600003A RID: 58 RVA: 0x000031C4 File Offset: 0x000013C4
			public static MethodBase TargetMethod()
			{
				return AccessTools.Method(typeof(SynchronizedObjectManager), "ClearSynchronizedObjects", null, null);
			}

			// Token: 0x0600003B RID: 59 RVA: 0x000031DC File Offset: 0x000013DC
			[HarmonyPostfix]
			public static void Postfix()
			{
				PatchSyncObjects.s_objectsById.Clear();
				PatchSyncObjects.s_gatherStaging.Clear();
			}
		}

		// Token: 0x02000027 RID: 39
		[HarmonyPatch]
		public static class PatchServerGather
		{
			// Token: 0x0600003C RID: 60 RVA: 0x000031F2 File Offset: 0x000013F2
			public static MethodBase TargetMethod()
			{
				return AccessTools.Method(typeof(SynchronizedObjectManager), "Server_GatherSynchronizedObjectData", null, null);
			}

			// Token: 0x0600003D RID: 61 RVA: 0x0000320C File Offset: 0x0000140C
			[HarmonyPrefix]
			public static bool Prefix(SynchronizedObjectManager __instance, bool forceAllObjects, ref SynchronizedObjectData[] __result)
			{
				List<SynchronizedObject> list = PatchSyncObjects.s_synchronizedObjects.Invoke(__instance);
				int tickRate = __instance.TickRate;
				float num = (float)(NetworkManager.Singleton.ServerTime.Time - PatchSyncObjects.s_serverLastSentServerTime.Invoke(__instance));
				List<SynchronizedObjectData> s_gatherStaging = PatchSyncObjects.s_gatherStaging;
				s_gatherStaging.Clear();
				for (int i = 0; i < list.Count; i++)
				{
					SynchronizedObject synchronizedObject = list[i];
					if (!(synchronizedObject == null) && (forceAllObjects || synchronizedObject.ShouldSendPosition(tickRate) || synchronizedObject.ShouldSendRotation(tickRate)))
					{
						ValueTuple<Vector3, Quaternion, ulong> valueTuple = synchronizedObject.OnServerTick(num);
						Vector3 item = valueTuple.Item1;
						Quaternion item2 = valueTuple.Item2;
						ulong item3 = valueTuple.Item3;
						List<SynchronizedObjectData> list2 = s_gatherStaging;
						SynchronizedObjectData item4 = default(SynchronizedObjectData);
						item4.NetworkObjectId = (ushort)item3;
						item4.X = (short)(item.x * 655f);
						item4.Y = (short)(item.y * 655f);
						item4.Z = (short)(item.z * 655f);
						item4.Rx = (short)(item2.x * 32767f);
						item4.Ry = (short)(item2.y * 32767f);
						item4.Rz = (short)(item2.z * 32767f);
						item4.Rw = (short)(item2.w * 32767f);
						list2.Add(item4);
					}
				}
				__result = s_gatherStaging.ToArray();
				return false;
			}
		}

		// Token: 0x02000028 RID: 40
		[HarmonyPatch]
		public static class PatchClientSync
		{
			// Token: 0x0600003E RID: 62 RVA: 0x0000337C File Offset: 0x0000157C
			public static MethodBase TargetMethod()
			{
				return AccessTools.Method(typeof(SynchronizedObjectManager), "Client_SynchronizeObjects", null, null);
			}

			// Token: 0x0600003F RID: 63 RVA: 0x00003394 File Offset: 0x00001594
			private static void Decode(in SynchronizedObjectData d, out Vector3 pos, out Quaternion rot)
			{
				pos = new Vector3((float)d.X / 655f, (float)d.Y / 655f, (float)d.Z / 655f);
				rot = new Quaternion((float)d.Rx / 32767f, (float)d.Ry / 32767f, (float)d.Rz / 32767f, (float)d.Rw / 32767f);
			}

			// Token: 0x06000040 RID: 64 RVA: 0x00003414 File Offset: 0x00001614
			[HarmonyPrefix]
			public static bool Prefix(SynchronizedObjectManager __instance, SynchronizedObjectData[] synchronizedObjectsData, float serverDeltaTime, double serverTime)
			{
				if (synchronizedObjectsData == null)
				{
					return false;
				}
				PatchSyncObjects.EnsureDictPopulated(__instance);
				Dictionary<ulong, SynchronizedObject> s_objectsById = PatchSyncObjects.s_objectsById;
				if (__instance.UseNetworkSmoothing && PatchSyncObjects.s_clientHasReceivedFirstTick.Invoke(__instance))
				{
					List<SynchronizedObjectSnapshot> list = new List<SynchronizedObjectSnapshot>(synchronizedObjectsData.Length);
					for (int i = 0; i < synchronizedObjectsData.Length; i++)
					{
						ref SynchronizedObjectData ptr = ref synchronizedObjectsData[i];
						Vector3 vector;
						Quaternion quaternion;
						PatchSyncObjects.PatchClientSync.Decode(ptr, out vector, out quaternion);
						SynchronizedObject synchronizedObject;
						if (s_objectsById.TryGetValue((ulong)ptr.NetworkObjectId, out synchronizedObject) && !(synchronizedObject == null))
						{
							list.Add(synchronizedObject.OnClientSmoothTick(vector, quaternion, synchronizedObject, serverDeltaTime));
						}
					}
					SynchronizedObjectsSnapshot synchronizedObjectsSnapshot = new SynchronizedObjectsSnapshot(serverTime, NetworkManager.Singleton.LocalTime.Time, list);
					SnapshotInterpolationSettings snapshotInterpolationSettings = PatchSyncObjects.s_snapshotInterpolationSettings.Invoke(__instance);
					if (snapshotInterpolationSettings.dynamicAdjustment)
					{
						snapshotInterpolationSettings.bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment((double)__instance.TickInterval, PatchSyncObjects.s_deliveryTimeEma.Invoke(__instance).StandardDeviation, (double)snapshotInterpolationSettings.dynamicAdjustmentTolerance) * (double)__instance.NetworkSmoothingStrength;
					}
					double num = (double)__instance.TickInterval * snapshotInterpolationSettings.bufferTimeMultiplier;
					SnapshotInterpolation.InsertAndAdjust<SynchronizedObjectsSnapshot>(
						PatchSyncObjects.s_snapshots.Invoke(__instance),
						snapshotInterpolationSettings.bufferLimit,
						synchronizedObjectsSnapshot,
						ref PatchSyncObjects.s_clientLocalTimeline.Invoke(__instance),
						ref PatchSyncObjects.s_clientLocalTimescale.Invoke(__instance),
						__instance.TickInterval,
						num,
						snapshotInterpolationSettings.catchupSpeed,
						snapshotInterpolationSettings.slowdownSpeed,
						ref PatchSyncObjects.s_driftEma.Invoke(__instance),
						snapshotInterpolationSettings.catchupNegativeThreshold,
						snapshotInterpolationSettings.catchupPositiveThreshold,
						ref PatchSyncObjects.s_deliveryTimeEma.Invoke(__instance));
				}
				else
				{
					for (int j = 0; j < synchronizedObjectsData.Length; j++)
					{
						ref SynchronizedObjectData ptr2 = ref synchronizedObjectsData[j];
						Vector3 vector2;
						Quaternion quaternion2;
						PatchSyncObjects.PatchClientSync.Decode(ptr2, out vector2, out quaternion2);
						SynchronizedObject synchronizedObject2;
						if (s_objectsById.TryGetValue((ulong)ptr2.NetworkObjectId, out synchronizedObject2) && !(synchronizedObject2 == null))
						{
							synchronizedObject2.OnClientTick(vector2, quaternion2, serverDeltaTime);
						}
					}
				}
				return false;
			}
		}

		// Token: 0x02000029 RID: 41
		[HarmonyPatch(typeof(EventManager), "TriggerEvent")]
		public static class PatchEventManagerTrigger
		{
			// Token: 0x06000041 RID: 65 RVA: 0x000035D8 File Offset: 0x000017D8
			[HarmonyPrefix]
			public static bool Prefix(string eventName, Dictionary<string, object> message)
			{
				Dictionary<string, Action<Dictionary<string, object>>> dictionary = (Dictionary<string, Action<Dictionary<string, object>>>)PatchSyncObjects.s_eventsField.GetValue(null);
				return dictionary != null && dictionary.ContainsKey(eventName);
			}
		}
	}
}
