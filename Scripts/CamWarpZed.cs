#if USE_ZED
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamWarpZed : CamWarp
{
	/// <summary>
	/// Instance of the ZEDManager interface
	/// </summary>
	[SerializeField]
	protected ZEDManager zedManager = null;

	protected override void OnEnable()
	{
		hmdToCamera = new Vector3(-0.0315f, 0, 0.115f);
		
		if (zedManager == null)
		{
			zedManager = FindObjectOfType<ZEDManager>();
			if (ZEDManager.GetInstances().Count > 1) //We chose a ZED arbitrarily, but there are multiple cams present. Warn the user. 
			{
				Debug.Log("Warning: " + gameObject.name + "'s zedManager was not specified, so the first available ZEDManager instance was " +
					"assigned. However, there are multiple ZEDManager's in the scene. It's recommended to specify which ZEDManager you want to " +
					"use to display a point cloud.");
			}
		}

		if (zedManager != null)
		{
			zedManager.OnGrab += UpdateMeshPosition;
		}

		base.OnEnable();
	}

	protected override void OnDisable()
	{
		if (zedManager != null)
		{
			zedManager.OnGrab -= UpdateMeshPosition;
		}

		base.OnDisable();
	}

	protected override ulong GetImageTimeStamp()
	{
		return zedManager.ImageTimeStamp;
	}
}

#endif
