using System;
using UnityEngine;

namespace ToasterPerformance
{
	// Token: 0x02000005 RID: 5
	internal class HUDListenerCleanupDriver : MonoBehaviour
	{
		// Token: 0x06000009 RID: 9 RVA: 0x000021B2 File Offset: 0x000003B2
		private void Update()
		{
			if (Time.unscaledTime < this._nextScan)
			{
				return;
			}
			this._nextScan = Time.unscaledTime + 5f;
			PatchListenerlessEvents.CleanupUIHUDListenersIfDedi();
		}

		// Token: 0x0400000B RID: 11
		private float _nextScan;
	}
}
