using UnityEngine;

/// <summary>
/// Each data provider for a point cloud should inherit from this class.
/// Used by ShaderPointCloudRenderer
/// </summary>
public abstract class PointCloudProvider : MonoBehaviour
{
	/// <summary>
	/// RGBAFloat texture storing points positions
	/// </summary>
	protected Texture vertexTexture;

	/// <summary>
	/// RGBAFloat texture storing color map
	/// </summary>
	protected Texture colorTexture;

	public Texture VertexTexture
	{
		get	{ return vertexTexture;	}
	}

	public Texture ColorTexture
	{
		get { return colorTexture; }
	}
}
