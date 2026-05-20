using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToasterPerformance
{
	// Token: 0x02000004 RID: 4
	public static class PatchListenerlessEvents
	{
		// Token: 0x06000004 RID: 4 RVA: 0x000020E0 File Offset: 0x000002E0
		private static bool HasListener(string eventName)
		{
			Dictionary<string, Action<Dictionary<string, object>>> dictionary = (Dictionary<string, Action<Dictionary<string, object>>>)PatchListenerlessEvents.s_eventsField.GetValue(null);
			return dictionary != null && dictionary.ContainsKey(eventName);
		}

		// Token: 0x06000005 RID: 5 RVA: 0x0000210A File Offset: 0x0000030A
		public static void CleanupUIHUDListenersIfDedi()
		{
			if (!PatchListenerlessEvents.IsDedicatedServer())
			{
				return;
			}
			PatchListenerlessEvents.StripUIHUDListenersFor("Event_Everyone_OnPlayerBodySpeedChanged");
			PatchListenerlessEvents.StripUIHUDListenersFor("Event_Everyone_OnPlayerBodyStaminaChanged");
		}

		// Token: 0x06000006 RID: 6 RVA: 0x00002128 File Offset: 0x00000328
		private static void StripUIHUDListenersFor(string eventName)
		{
			Dictionary<string, Action<Dictionary<string, object>>> dictionary = (Dictionary<string, Action<Dictionary<string, object>>>)PatchListenerlessEvents.s_eventsField.GetValue(null);
			Action<Dictionary<string, object>> action;
			if (dictionary == null || !dictionary.TryGetValue(eventName, out action) || action == null)
			{
				return;
			}
			foreach (Delegate @delegate in action.GetInvocationList())
			{
				if (@delegate.Target is UIHUDController)
				{
					EventManager.RemoveEventListener(eventName, (Action<Dictionary<string, object>>)@delegate);
				}
			}
		}

		// Token: 0x06000007 RID: 7 RVA: 0x0000218D File Offset: 0x0000038D
		private static bool IsDedicatedServer()
		{
			return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
		}

		// Token: 0x0400000A RID: 10
		private static readonly FieldInfo s_eventsField = AccessTools.Field(typeof(EventManager), "events");

		// Token: 0x02000014 RID: 20
		[HarmonyPatch(typeof(PlayerBody), "OnSpeedChanged")]
		public static class PatchSpeedChanged
		{
			// Token: 0x06000028 RID: 40 RVA: 0x00002C1E File Offset: 0x00000E1E
			[HarmonyPrefix]
			public static bool Prefix()
			{
				return PatchListenerlessEvents.HasListener("Event_Everyone_OnPlayerBodySpeedChanged");
			}
		}

		// Token: 0x02000015 RID: 21
		[HarmonyPatch(typeof(PlayerBody), "OnStaminaChanged")]
		public static class PatchStaminaChanged
		{
			// Token: 0x06000029 RID: 41 RVA: 0x00002C2A File Offset: 0x00000E2A
			[HarmonyPrefix]
			public static bool Prefix()
			{
				return PatchListenerlessEvents.HasListener("Event_Everyone_OnPlayerBodyStaminaChanged");
			}
		}

		// Token: 0x02000016 RID: 22
		[HarmonyPatch(typeof(Player), "OnPlayerPingChanged")]
		public static class PatchPingChanged
		{
			// Token: 0x0600002A RID: 42 RVA: 0x00002C36 File Offset: 0x00000E36
			[HarmonyPrefix]
			public static bool Prefix()
			{
				return PatchListenerlessEvents.HasListener("Event_Everyone_OnPlayerPingChanged");
			}
		}
	}
}
