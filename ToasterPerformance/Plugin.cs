using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToasterPerformance
{
	// Token: 0x0200000A RID: 10
	public class Plugin : IPuckPlugin
	{
		// Token: 0x06000016 RID: 22 RVA: 0x00002754 File Offset: 0x00000954
		public bool OnEnable()
		{
			Plugin.Log("Enabling " + Plugin.MOD_VERSION + "...");
			bool result;
			try
			{
				if (Plugin.IsDedicatedServer())
				{
					Plugin.Log("Environment: dedicated server.");
				}
				else
				{
					Plugin.Log("Environment: client.");
				}
				Plugin.Log("Patching methods...");
				Plugin.harmony.PatchAll();
				Plugin.Log("All patched!");
				if (Plugin.IsDedicatedServer())
				{
					GameObject gameObject = new GameObject("ToasterPerformance_HUDListenerCleanup");
					UnityEngine.Object.DontDestroyOnLoad(gameObject);
					gameObject.AddComponent<HUDListenerCleanupDriver>();
				}
				result = true;
			}
			catch (Exception arg)
			{
				Plugin.LogError(string.Format("Failed to Enable: {0}", arg));
				result = false;
			}
			return result;
		}

		// Token: 0x06000017 RID: 23 RVA: 0x000027FC File Offset: 0x000009FC
		public bool OnDisable()
		{
			bool result;
			try
			{
				Plugin.Log("Disabling...");
				Plugin.harmony.UnpatchSelf();
				Plugin.Log("Disabled.");
				result = true;
			}
			catch (Exception arg)
			{
				Plugin.LogError(string.Format("Failed to disable: {0}", arg));
				result = false;
			}
			return result;
		}

		// Token: 0x06000018 RID: 24 RVA: 0x00002854 File Offset: 0x00000A54
		public static bool IsDedicatedServer()
		{
			return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
		}

		// Token: 0x06000019 RID: 25 RVA: 0x0000285E File Offset: 0x00000A5E
		public static void Log(string message)
		{
			Debug.Log("[" + Plugin.MOD_NAME + "] " + message);
		}

		// Token: 0x0600001A RID: 26 RVA: 0x0000287A File Offset: 0x00000A7A
		public static void LogError(string message)
		{
			Debug.LogError("[" + Plugin.MOD_NAME + "] " + message);
		}

		// Token: 0x0600001B RID: 27 RVA: 0x00002896 File Offset: 0x00000A96
		public static void LogWarning(string message)
		{
			Debug.LogWarning("[" + Plugin.MOD_NAME + "] " + message);
		}

		// Token: 0x04000031 RID: 49
		public static string MOD_NAME = "ToasterPerformance";

		// Token: 0x04000032 RID: 50
		public static string MOD_VERSION = "0.1.0";

		// Token: 0x04000033 RID: 51
		public static string MOD_GUID = "pw.stellaric.toaster.performance";

		// Token: 0x04000034 RID: 52
		private static readonly Harmony harmony = new Harmony(Plugin.MOD_GUID);
	}
}
