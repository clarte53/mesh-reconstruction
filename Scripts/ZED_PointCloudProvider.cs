#if USE_ZED
using sl;
using UnityEngine;

public class ZED_PointCloudProvider : PointCloudProvider
{
	#region Attributes
	/// <summary>
	/// Instance of the ZEDManager interface
	/// </summary>
	[SerializeField]
	protected ZEDManager zedManager = null;

	/// <summary>
	/// zed Camera controller by zedManager
	/// </summary>
	protected sl.ZEDCamera zed = null;

	/// <summary>
	/// Texture that holds the 3D position of the points.
	/// </summary>
	protected Texture2D XYZTexture;

	/// <summary>
	/// Texture that holds the colors of each point.
	/// </summary>
	protected Texture2D colTexture;

	protected RenderTexture vertexRenderTexture;

	protected RenderTexture colorRenderTexture;

	public ComputeShader TextureGenerator;

	protected int vertexTextureGeneratorKernelId;

	protected int colorTextureGeneratorKernelId;
	#endregion

	#region MonoBehavior callbacks
	void Start()
	{
		if(zedManager == null)
		{
			zedManager = FindObjectOfType<ZEDManager>();
			if(ZEDManager.GetInstances().Count > 1) //We chose a ZED arbitrarily, but there are multiple cams present. Warn the user. 
			{
				Debug.Log("Warning: " + gameObject.name + "'s zedManager was not specified, so the first available ZEDManager instance was " +
					"assigned. However, there are multiple ZEDManager's in the scene. It's recommended to specify which ZEDManager you want to " +
					"use to display a point cloud.");
			}
		}

		if(zedManager != null)
		{
			zed = zedManager.zedCamera;

			zedManager.OnZEDReady += OnZEDReady;
		}
	}

	private void Update()
	{
		CLARTE.Dev.Profiling.Profiler.Start("ZED_PointCloudProvider");
		if(zed.IsCameraReady && XYZTexture != null)
		{
			TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, XYZTexture.width, XYZTexture.height, 1);
		}

		if(zed.IsCameraReady && colTexture != null)
		{
			TextureGenerator.Dispatch(colorTextureGeneratorKernelId, colTexture.width, colTexture.height, 1);
		}
		CLARTE.Dev.Profiling.Profiler.Stop("ZED_PointCloudProvider");
	}

	private void OnDisable()
	{
		// Releasing Textures 
		vertexRenderTexture.Release();
		colorRenderTexture.Release();
	}
	#endregion

	#region Internal methods
	void OnZEDReady()
	{
		//Create the textures. These will be updated automatically by the ZED. 
		XYZTexture = zed.CreateTextureMeasureType(sl.MEASURE.XYZ);
		colTexture = zed.CreateTextureImageType(sl.VIEW.LEFT);

		// Initialize rendertexture if needed
		if(vertexRenderTexture == null)
		{
			vertexRenderTexture = new RenderTexture(XYZTexture.width, XYZTexture.height, 0, RenderTextureFormat.ARGBFloat);
			vertexRenderTexture.enableRandomWrite = true;
			vertexRenderTexture.useMipMap = false;
			vertexRenderTexture.filterMode = FilterMode.Point;
			vertexRenderTexture.Create();

			vertexTexture = (Texture)vertexRenderTexture;
		}

		if(colorRenderTexture == null)
		{
			colorRenderTexture = new RenderTexture(colTexture.width, colTexture.height, 0, RenderTextureFormat.BGRA32);
			colorRenderTexture.enableRandomWrite = true;
			colorRenderTexture.useMipMap = false;
			colorRenderTexture.Create();

			colorTexture = (Texture)colorRenderTexture;
		}

		vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");

		TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Input_positionData", XYZTexture);
		TextureGenerator.SetInt("depthWidth", XYZTexture.width);
		TextureGenerator.SetInt("depthHeight", XYZTexture.height);

		TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", vertexRenderTexture);


		colorTextureGeneratorKernelId = TextureGenerator.FindKernel("ColorGenerator");

		TextureGenerator.SetTexture(colorTextureGeneratorKernelId, "Input_colorData", colTexture);
		TextureGenerator.SetInt("colorWidth", colorTexture.width);
		TextureGenerator.SetInt("colorHeight", colorTexture.height);

		TextureGenerator.SetTexture(colorTextureGeneratorKernelId, "Output_colorTexture", colorRenderTexture);
	}
	#endregion
}
#endif
