﻿using UnityEngine;

public class MeshedPointCloudRenderer : PointCloudRenderer
{
	[Range(0.001f, 0.3f)]
	[SerializeField]
	float maxEdgeLength = 0.05f;

	[Range(0.0f, 90.0f)]
	[SerializeField]
	float startFadeAngle = 63.0f;

	[Range(0.0f, 90.0f)]
	[SerializeField]
	float endFadeAngle = 80.0f;
    
    override protected void Update()
    {
		CLARTE.Dev.Profiling.Profiler.Start("MeshedPointCloudRenderer");

		vertexBufferGenerator.SetFloat("maxEdgeLength", maxEdgeLength);

		vertexBufferGenerator.SetFloat("startFadeAngle", startFadeAngle * Mathf.Deg2Rad);

		vertexBufferGenerator.SetFloat("endFadeAngle", endFadeAngle * Mathf.Deg2Rad);

		base.Update();

		CLARTE.Dev.Profiling.Profiler.Stop("MeshedPointCloudRenderer");
	}

	override protected Vector3Int ComputeVertexBufferMaxSize()
	{
		return new Vector3Int(width, height, 6);
	}
}
