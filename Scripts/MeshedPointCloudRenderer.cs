using UnityEngine;

public class MeshedPointCloudRenderer : PointCloudRenderer
{
	[Range(0.001f, 0.3f)]
	[SerializeField]
	float maxEdgeLength = 0.05f;
    
    override protected void Update()
    {
		CLARTE.Dev.Profiling.Profiler.Start("MeshedPointCloudRenderer");
		vertexBufferGenerator.SetFloat("maxEdgeLength", maxEdgeLength);

		base.Update();
		CLARTE.Dev.Profiling.Profiler.Stop("MeshedPointCloudRenderer");
	}

	override protected int ComputeVertexBufferMaxSize()
	{
		return (width - 1) * (height - 1) * 2 * 3;
	}
}
