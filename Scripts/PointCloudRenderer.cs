using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PointCloudProvider))]
public class PointCloudRenderer : MonoBehaviour
{
	#region Attributes
	//The size of the buffer that holds the verts.
	protected Vector3Int vertexBufferMaxSize = Vector3Int.zero;

	[SerializeField]
	protected ComputeShader vertexBufferGenerator;

	[SerializeField]
	protected Material drawBuffer;

	[SerializeField]
	protected BoxCollider clippingBox;

	// Whether the point cloud to braw is static or continuously updated
	[SerializeField]
	protected bool isStatic;

	protected ComputeBuffer vertexBuffer;

	protected PointCloudProvider provider;

	protected bool isInitialized;

	protected int width, height;

	protected Vector3Int clearThreadCount;

	protected int clearBufferKernelId;

	protected Vector3Int fillThreadCount;

	protected int fillBufferKernelId;

	protected Matrix4x4 clippingBoxToPoints;
	#endregion

	#region Getter/Setters
	public BoxCollider ClippingBox { get { return clippingBox; } set { clippingBox = value; } }
	#endregion

	#region MonoBehavior callbacks
	protected void Awake()
	{
		// Retrieve with provider
		provider = gameObject.GetComponent<PointCloudProvider>();
		//drawBuffer = Material.Instantiate(drawBuffer);
		vertexBufferGenerator = ComputeShader.Instantiate(vertexBufferGenerator);
	}

	virtual protected void Update()
	{
		clippingBoxToPoints = ComputeClippingBoxToPointsTransformation();

		if(provider.VertexTexture == null || provider.ColorTexture == null)
		{
			//Debug.Log("Missing texture from point cloud provider");

			isInitialized = false;

			return;
		}

		if(!isInitialized)
		{
			Initialize(provider.VertexTexture);

			UpdateVertexBuffer();
		}
		else
		{
			if(!isStatic)
			{
				UpdateVertexBuffer();
			}
		}
	}

	/// <summary>
	/// Draw the mesh when on rendering of the object.
	/// </summary>
	protected void OnRenderObject()
	{
		//Since mesh is in a buffer need to use DrawProcedual called from OnPostRender or OnRenderObject
		drawBuffer.SetBuffer("vertexBuffer", vertexBuffer);
		drawBuffer.SetMatrix("modelMatrix", transform.localToWorldMatrix);
		drawBuffer.SetTexture("_MainTex", provider.ColorTexture);
		drawBuffer.SetPass(0);

		Graphics.DrawProceduralNow(MeshTopology.Triangles, vertexBufferMaxSize.x * vertexBufferMaxSize.y * vertexBufferMaxSize.z);
	}

	/// <summary>
	/// Clear buffers on destroy
	/// </summary>
	protected void OnDestroy()
	{
		if(vertexBuffer != null)
		{
			vertexBuffer.Release();
		}
	}
	#endregion

	#region Internal methods
	protected Matrix4x4 ComputeClippingBoxToPointsTransformation()
	{
		// Compute transformation from clipping box to point cloud referential
		Vector3 clipping_box_center_world = clippingBox.transform.TransformPoint(clippingBox.center);

		Vector3 clipping_box_half_size = 0.5f * clippingBox.size;

		Vector3 clipping_box_x_world = clippingBox.transform.right * clipping_box_half_size.x;
		Vector3 clipping_box_y_world = clippingBox.transform.up * clipping_box_half_size.y;
		Vector3 clipping_box_z_world = clippingBox.transform.forward * clipping_box_half_size.z;

		Matrix4x4 clipping_box_matrix_world = new Matrix4x4();
		clipping_box_matrix_world.SetColumn(0, clipping_box_x_world);
		clipping_box_matrix_world.SetColumn(1, clipping_box_y_world);
		clipping_box_matrix_world.SetColumn(2, clipping_box_z_world);
		clipping_box_matrix_world.SetColumn(3, clipping_box_center_world);
		clipping_box_matrix_world.m33 = 1.0f;

		Matrix4x4 clipping_box_to_points = clipping_box_matrix_world.inverse * transform.localToWorldMatrix;

		return clipping_box_to_points;
	}

	protected Vector3Int ComputeThreadCountPerThreadGroup(int max_x, int max_y, int max_z, int max_threads_per_group)
	{
		int x = 1;
		int y = 1;
		int z = 1;

		int min_error = int.MaxValue;

		for(int candidate_x = max_threads_per_group; candidate_x > 0; candidate_x--)
		{
			if(max_x % candidate_x != 0)
			{
				continue;
			}

			for(int candidate_y = max_threads_per_group; candidate_y > 0; candidate_y--)
			{
				if(max_y % candidate_y != 0)
				{
					continue;
				}

				for(int candidate_z = max_threads_per_group; candidate_z > 0; candidate_z--)
				{
					if(max_z % candidate_z != 0)
					{
						continue;
					}

					if(candidate_x * candidate_y * candidate_z <= max_threads_per_group)
					{
						int error = max_threads_per_group - (candidate_x * candidate_y * candidate_z);

						if(error < min_error)
						{
							x = candidate_x;

							y = candidate_y;

							z = candidate_z;

							min_error = error;

							if(error == 0)
							{
								return new Vector3Int(x, y, z);
							}
						}
					}
					candidate_z--;
				}
				candidate_y--;
			}
			candidate_x--;
		}

		return new Vector3Int(x, y, z);
	}

	protected void Initialize(Texture vertex_texture)
	{
		width = vertex_texture.width;

		height = vertex_texture.height;

		vertexBufferMaxSize = ComputeVertexBufferMaxSize();

		vertexBuffer = new ComputeBuffer(vertexBufferMaxSize.x * vertexBufferMaxSize.y * vertexBufferMaxSize.z, sizeof(float) * 6); // 4 position, 2 uv

		clearThreadCount = ComputeThreadCountPerThreadGroup(vertexBufferMaxSize.x, vertexBufferMaxSize.y, vertexBufferMaxSize.z, 1024);

		string clear_buffer_kernel_name = "ClearVertexBuffer_" + clearThreadCount.x + "_" + clearThreadCount.y + "_" + clearThreadCount.z;

		try
		{
			clearBufferKernelId = vertexBufferGenerator.FindKernel(clear_buffer_kernel_name);
		}
		catch(System.Exception)
		{
			Debug.LogWarning("Clear buffer kernel not found, falling back to default.");

			clearBufferKernelId = vertexBufferGenerator.FindKernel("ClearVertexBuffer_1_1_1");
			clearThreadCount.x = 1;
			clearThreadCount.y = 1;
			clearThreadCount.z = 1;
		}



		fillThreadCount = ComputeThreadCountPerThreadGroup(vertex_texture.width, vertex_texture.height, 1, 1024);

		string fill_buffer_kernel_name = "FillVertexBuffer_" + fillThreadCount.x + "_" + fillThreadCount.y + "_" + fillThreadCount.z;

		try
		{
			fillBufferKernelId = vertexBufferGenerator.FindKernel(fill_buffer_kernel_name);
		}
		catch(System.Exception)
		{
			Debug.LogWarning("Fill buffer kernel not found, falling back to default.");

			fillBufferKernelId = vertexBufferGenerator.FindKernel("FillVertexBuffer_1_1_1");
			fillThreadCount.x = 1;
			fillThreadCount.y = 1;
			fillThreadCount.z = 1;
		}

		isInitialized = true;
	}

	protected void UpdateVertexBuffer()
	{
		vertexBufferGenerator.SetBuffer(clearBufferKernelId, "vertexBuffer", vertexBuffer);
		vertexBufferGenerator.SetInt("clear_width", vertexBufferMaxSize.x);
		vertexBufferGenerator.SetInt("clear_height", vertexBufferMaxSize.y);
		vertexBufferGenerator.Dispatch(clearBufferKernelId, vertexBufferMaxSize.x / clearThreadCount.x, vertexBufferMaxSize.y / clearThreadCount.y, vertexBufferMaxSize.z / clearThreadCount.z);

		vertexBufferGenerator.SetTexture(fillBufferKernelId, "Input_vertexTexture", provider.VertexTexture);
		vertexBufferGenerator.SetMatrix("pointsToClippingBox", clippingBoxToPoints);
		vertexBufferGenerator.SetInt("width", width);
		vertexBufferGenerator.SetBuffer(fillBufferKernelId, "vertexBuffer", vertexBuffer);
		vertexBufferGenerator.Dispatch(fillBufferKernelId, provider.VertexTexture.width / fillThreadCount.x, provider.VertexTexture.height / fillThreadCount.y, 1);
	}

	virtual protected Vector3Int ComputeVertexBufferMaxSize()
	{
		return Vector3Int.zero;
	}
	#endregion
}
