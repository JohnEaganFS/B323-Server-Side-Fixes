using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterPerformance
{
	// Token: 0x02000007 RID: 7
	public static class PatchSpectatorAndMinimap
	{
		// Token: 0x06000010 RID: 16 RVA: 0x000024A4 File Offset: 0x000006A4
		private static Vector2 WorldToMinimap(UIMinimap mgr, Vector3 pos, Bounds bounds)
		{
			VisualElement visualElement = PatchSpectatorAndMinimap.s_minimapContent.Invoke(mgr);
			Vector2 vector = new Vector2(visualElement.resolvedStyle.width, visualElement.resolvedStyle.height);
			Vector2 vector2 = new Vector2((pos.x + bounds.center.x) / bounds.size.x, (pos.z + bounds.center.z) / bounds.size.z);
			return new Vector2(vector.x * vector2.x, vector.y * vector2.y);
		}

		// Token: 0x04000016 RID: 22
		private static readonly AccessTools.FieldRef<SpectatorManager, Dictionary<SpectatorPosition, Spectator>> s_specMap = AccessTools.FieldRefAccess<SpectatorManager, Dictionary<SpectatorPosition, Spectator>>("spectatorPositionSpectatorMap");

		// Token: 0x04000017 RID: 23
		private static readonly AccessTools.FieldRef<SpectatorManager, int> s_updateBatch = AccessTools.FieldRefAccess<SpectatorManager, int>("updateBatch");

		// Token: 0x04000018 RID: 24
		private static FieldInfo s_specUpdatesPerFrameField;

		// Token: 0x04000019 RID: 25
		private static readonly List<Spectator> s_spectatorsCache = new List<Spectator>(128);

		// Token: 0x0400001A RID: 26
		private static bool s_specDirty = true;

		// Token: 0x0400001B RID: 27
		private static readonly Dictionary<VisualElement, VisualElement> s_minimapBodyCache = new Dictionary<VisualElement, VisualElement>();

		// Token: 0x0400001C RID: 28
		private static readonly AccessTools.FieldRef<UIMinimap, float> s_minimapAccum = AccessTools.FieldRefAccess<UIMinimap, float>("updateAccumulator");

		// Token: 0x0400001D RID: 29
		private static readonly AccessTools.FieldRef<UIMinimap, int> s_minimapUpdateRate = AccessTools.FieldRefAccess<UIMinimap, int>("updateRate");

		// Token: 0x0400001E RID: 30
		private static readonly AccessTools.FieldRef<UIMinimap, PlayerTeam> s_minimapTeam = AccessTools.FieldRefAccess<UIMinimap, PlayerTeam>("Team");

		// Token: 0x0400001F RID: 31
		private static readonly AccessTools.FieldRef<UIMinimap, Dictionary<PlayerBody, VisualElement>> s_minimapPlayerMap = AccessTools.FieldRefAccess<UIMinimap, Dictionary<PlayerBody, VisualElement>>("playerBodyVisualElementMap");

		// Token: 0x04000020 RID: 32
		private static readonly AccessTools.FieldRef<UIMinimap, Dictionary<Puck, VisualElement>> s_minimapPuckMap = AccessTools.FieldRefAccess<UIMinimap, Dictionary<Puck, VisualElement>>("puckVisualElementMap");

		// Token: 0x04000021 RID: 33
		private static readonly AccessTools.FieldRef<UIMinimap, VisualElement> s_minimapContent = AccessTools.FieldRefAccess<UIMinimap, VisualElement>("content");

		// Token: 0x0200001F RID: 31
		[HarmonyPatch(typeof(SpectatorManager), "RegisterSpectatorPosition")]
		public static class PatchSpecAdd
		{
			// Token: 0x06000033 RID: 51 RVA: 0x00002D22 File Offset: 0x00000F22
			[HarmonyPostfix]
			public static void Postfix()
			{
				PatchSpectatorAndMinimap.s_specDirty = true;
			}
		}

		// Token: 0x02000020 RID: 32
		[HarmonyPatch(typeof(SpectatorManager), "UnregisterSpectatorPosition")]
		public static class PatchSpecRemove
		{
			// Token: 0x06000034 RID: 52 RVA: 0x00002D2A File Offset: 0x00000F2A
			[HarmonyPostfix]
			public static void Postfix()
			{
				PatchSpectatorAndMinimap.s_specDirty = true;
			}
		}

		// Token: 0x02000021 RID: 33
		[HarmonyPatch(typeof(SpectatorManager), "Update")]
		public static class PatchSpecUpdate
		{
			// Token: 0x06000035 RID: 53 RVA: 0x00002D34 File Offset: 0x00000F34
			[HarmonyPrefix]
			public static bool Prefix(SpectatorManager __instance)
			{
				Dictionary<SpectatorPosition, Spectator> dictionary = PatchSpectatorAndMinimap.s_specMap.Invoke(__instance);
				if (PatchSpectatorAndMinimap.s_specDirty)
				{
					PatchSpectatorAndMinimap.s_spectatorsCache.Clear();
					foreach (KeyValuePair<SpectatorPosition, Spectator> keyValuePair in dictionary)
					{
						PatchSpectatorAndMinimap.s_spectatorsCache.Add(keyValuePair.Value);
					}
					PatchSpectatorAndMinimap.s_specDirty = false;
				}
				int count = PatchSpectatorAndMinimap.s_spectatorsCache.Count;
				if (count == 0)
				{
					return false;
				}
				if (PatchSpectatorAndMinimap.s_specUpdatesPerFrameField == null)
				{
					PatchSpectatorAndMinimap.s_specUpdatesPerFrameField = AccessTools.Field(typeof(SpectatorManager), "spectatorUpdatesPerFrame");
				}
				int num = (int)PatchSpectatorAndMinimap.s_specUpdatesPerFrameField.GetValue(__instance);
				int num2 = PatchSpectatorAndMinimap.s_updateBatch.Invoke(__instance);
				for (int i = 0; i < num; i++)
				{
					int index = (num2 + i) % count;
					Spectator spectator = PatchSpectatorAndMinimap.s_spectatorsCache[index];
					if (spectator != null)
					{
						spectator.UpdateAnimation();
					}
				}
				PatchSpectatorAndMinimap.s_updateBatch.Invoke(__instance) = (num2 + num) % count;
				return false;
			}
		}

		// Token: 0x02000022 RID: 34
		[HarmonyPatch(typeof(UIMinimap), "Update")]
		public static class PatchMinimapUpdate
		{
			// Token: 0x06000036 RID: 54 RVA: 0x00002E50 File Offset: 0x00001050
			[HarmonyPrefix]
			public static bool Prefix(UIMinimap __instance)
			{
				if (ApplicationManager.IsDedicatedGameServer)
				{
					return false;
				}
				ref float ptr = ref PatchSpectatorAndMinimap.s_minimapAccum.Invoke(__instance);
				int num = PatchSpectatorAndMinimap.s_minimapUpdateRate.Invoke(__instance);
				ptr += Time.deltaTime;
				if (ptr < 1f / (float)num)
				{
					return false;
				}
				ptr = 0f;
				PlayerTeam team = PatchSpectatorAndMinimap.s_minimapTeam.Invoke(__instance);
				Bounds bounds = __instance.Bounds;
				bool flag = team == PlayerTeam.Blue;
				foreach (KeyValuePair<PlayerBody, VisualElement> keyValuePair in PatchSpectatorAndMinimap.s_minimapPlayerMap.Invoke(__instance))
				{
					PlayerBody key = keyValuePair.Key;
					VisualElement value = keyValuePair.Value;
					if (!(key == null))
					{
						VisualElement visualElement;
						if (!PatchSpectatorAndMinimap.s_minimapBodyCache.TryGetValue(value, out visualElement))
						{
							visualElement = UQueryExtensions.Q<VisualElement>(value, "Body", (string)null);
							if (visualElement != null)
							{
								PatchSpectatorAndMinimap.s_minimapBodyCache[value] = visualElement;
							}
						}
						Vector3 pos = flag ? key.transform.position : (-key.transform.position);
						float num3 = flag ? key.transform.rotation.eulerAngles.y : (key.transform.rotation.eulerAngles.y + 180f);
						Vector2 vector = PatchSpectatorAndMinimap.WorldToMinimap(__instance, pos, bounds);
						value.style.translate = new Translate(-vector.x, vector.y);
						if (visualElement != null)
						{
							visualElement.style.rotate = new Rotate(num3);
						}
					}
				}
				foreach (KeyValuePair<Puck, VisualElement> keyValuePair2 in PatchSpectatorAndMinimap.s_minimapPuckMap.Invoke(__instance))
				{
					Puck key2 = keyValuePair2.Key;
					VisualElement value2 = keyValuePair2.Value;
					if (!(key2 == null))
					{
						Vector3 pos2 = flag ? key2.transform.position : (-key2.transform.position);
						float num4 = flag ? key2.transform.rotation.eulerAngles.y : (key2.transform.rotation.eulerAngles.y + 180f);
						Vector2 vector2 = PatchSpectatorAndMinimap.WorldToMinimap(__instance, pos2, bounds);
						value2.style.translate = new Translate(-vector2.x, vector2.y);
						value2.style.rotate = new Rotate(num4);
					}
				}
				return false;
			}
		}

		// Token: 0x02000023 RID: 35
		[HarmonyPatch(typeof(UIMinimap), "RemovePlayerBody")]
		public static class PatchMinimapRemovePlayer
		{
			// Token: 0x06000037 RID: 55 RVA: 0x00003150 File Offset: 0x00001350
			[HarmonyPrefix]
			public static void Prefix(UIMinimap __instance, PlayerBody playerBody)
			{
				if (playerBody == null)
				{
					return;
				}
				VisualElement visualElement;
				if (PatchSpectatorAndMinimap.s_minimapPlayerMap.Invoke(__instance).TryGetValue(playerBody, out visualElement) && visualElement != null)
				{
					PatchSpectatorAndMinimap.s_minimapBodyCache.Remove(visualElement);
				}
			}
		}
	}
}
