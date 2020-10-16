using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CLARTE.Geometry.Extensions;

public class SimplePointCloudRenderer : PointCloudRenderer
{
	[Range(0.0001f, 0.3f)]
	[SerializeField]
	float size = 0.05f;

	override protected void Update()
	{
		vertexBufferGenerator.SetFloat("size", size);

		if (Camera.main != null)
		{
			Vector3 dir_x = Camera.main.transform.Right(transform);
			Vector3 dir_y = Camera.main.transform.Up(transform);
			vertexBufferGenerator.SetVector("dir_x", dir_x);
			vertexBufferGenerator.SetVector("dir_y", dir_y);
		}

		base.Update();
	}

	override protected Vector3Int ComputeVertexBufferMaxSize()
	{
		return new Vector3Int(width, height, 6);
	}
}
