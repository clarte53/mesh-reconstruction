using UnityEngine;

public class RgbDepthMap_PointCloudProvider : PointCloudProvider
{

	public float slide_u = 0.0125f;
	public float slide_v = -0.005f;
	private Texture depthTexture;

	public ComputeShader TextureGenerator;

	private Vector2Int depthSize = Vector2Int.zero;
	private float[] depthIntrinsec = new float[4];
	private Vector2Int imageSize = Vector2Int.zero;
	private float[] imageIntrinsec = new float[4];
	private Matrix4x4 imagePoseToDepthCoords;

	protected int vertexTextureGeneratorKernelId;

	// Variables for edge smoothing
	public bool processDepthEdge = false;
	[HideInInspector]
	public ComputeShader edgeSmoothingShader;
	[HideInInspector]
	public float laplacianThreshold = 100;
	[HideInInspector]
	public int dilateRadius = 2;
	private Texture processedEdgeTexture;

	public void Awake()
	{
		TextureGenerator = ComputeShader.Instantiate(TextureGenerator);
		if (processDepthEdge)
		{
			edgeSmoothingShader = ComputeShader.Instantiate(edgeSmoothingShader);
		}
	}

	/// <summary>
	/// Set values related to depth and color images: image sizes, cameras intrinsics and transformation matrix between images.
	/// </summary>
	/// <param name="pcData"></param>
	private void SetData(PointCloudRgbDepthData pcData)
	{
		SetDepthParameters(pcData.depthSize[0], pcData.depthSize[1], pcData.depthIntrinsec[0], pcData.depthIntrinsec[1], pcData.depthIntrinsec[2], pcData.depthIntrinsec[3]);
		SetImageParameters(pcData.imageSize[0], pcData.imageSize[1], pcData.imageIntrinsec[0], pcData.imageIntrinsec[1], pcData.imageIntrinsec[2], pcData.imageIntrinsec[3]);
		SetImagePoseInDepthSpace(pcData.imagePoseToDepthCoords);
	}

	/// <summary>
	/// Update point cloud from depth and color images that are stored on CPU. Data is passed as native arrays
	/// </summary>
	/// <param name="pcData"></param>
	public void SetData(PointCloudRgbDepthData_GPU pcData)
	{
		SetData((PointCloudRgbDepthData)pcData);

		CreatePointCloud(pcData.depthData, pcData.colorData);
	}

	/// <summary>
	/// Update point cloud from depth and color images that are stored on GPU. Data is passed as Textures
	/// </summary>
	/// <param name="pcData"></param>
	public void SetData(PointCloudRgbDepthData_CPU pcData)
	{
		SetData((PointCloudRgbDepthData)pcData);

		CreatePointCloud(pcData.depthData, pcData.colorData);
	}

	public void SetData(Texture depth_texture, Texture color_texture, float[] intrinsics)
	{
		SetDepthParameters(depth_texture.width, depth_texture.height, intrinsics[0], intrinsics[1], intrinsics[2], intrinsics[3]);
		SetImageParameters(depth_texture.width, depth_texture.height, intrinsics[0], intrinsics[1], intrinsics[2], intrinsics[3]);
		SetImagePoseInDepthSpace(new float[] {1, 0, 0, 0,
											0, 1, 0, 0,
											0, 0, 1, 0,
											0, 0, 0, 1});

		CreatePointCloud(depth_texture, color_texture);
	}

	private void SetDepthParameters(int width, int height, float fx, float fy, float cx, float cy)
	{
		depthSize.x = width;
		depthSize.y = height;
		depthIntrinsec[0] = fx;
		depthIntrinsec[1] = fy;
		depthIntrinsec[2] = cx;
		depthIntrinsec[3] = cy;
	}

	private void SetImageParameters(int width, int height, float fx, float fy, float cx, float cy)
	{
		imageSize.x = width;
		imageSize.y = height;
		imageIntrinsec[0] = fx;
		imageIntrinsec[1] = fy;
		imageIntrinsec[2] = cx;
		imageIntrinsec[3] = cy;
	}

	private void SetImagePoseInDepthSpace(float[] m)
	{
		imagePoseToDepthCoords = new Matrix4x4();
		imagePoseToDepthCoords.SetRow(0, new Vector4(m[0], m[1], m[2], m[3]));
		imagePoseToDepthCoords.SetRow(1, new Vector4(m[4], m[5], m[6], m[7]));
		imagePoseToDepthCoords.SetRow(2, new Vector4(m[8], m[9], m[10], m[11]));
		imagePoseToDepthCoords.SetRow(3, new Vector4(m[12], m[13], m[14], m[15]));
	}

	void InitTextures()
	{
		if (vertexTexture == null)
		{
			depthTexture = new Texture2D(depthSize.x, depthSize.y, TextureFormat.RFloat, false);

			vertexTexture = new RenderTexture(depthSize.x, depthSize.y, 0, RenderTextureFormat.ARGBFloat);
			((RenderTexture)vertexTexture).enableRandomWrite = true;
			((RenderTexture)vertexTexture).useMipMap = false;
			((RenderTexture)vertexTexture).Create();

			colorTexture = new Texture2D(imageSize.x, imageSize.y, TextureFormat.RGB24, false);

			TextureGenerator.SetMatrix("ImagePoseInDepthCoords", imagePoseToDepthCoords);
			TextureGenerator.SetFloats("DepthIntrinsec", depthIntrinsec);
			TextureGenerator.SetFloats("ImageIntrinsec", imageIntrinsec);
			TextureGenerator.SetInt("DepthWidth", depthTexture.width);
			TextureGenerator.SetInt("DepthHeight", depthTexture.height);
			TextureGenerator.SetInt("ColorWidth", colorTexture.width);
			TextureGenerator.SetInt("ColorHeight", colorTexture.height);
		}
	}

	/// <summary>
	/// Create a mask on the edges of the texture depth map using a Laplacian operator followed by a Dilate operator. 
	/// The depth of the pixels that are affected by the mask is set to 0;
	/// </summary>
	/// <param name="depth_texture"></param>
	/// <returns></returns>
	private Texture ProcessEdge(Texture depth_texture)
	{
		if (processedEdgeTexture == null)
		{
			processedEdgeTexture = new RenderTexture(depthSize.x, depthSize.y, 0, RenderTextureFormat.RHalf);
			((RenderTexture)processedEdgeTexture).enableRandomWrite = true;
			((RenderTexture)processedEdgeTexture).useMipMap = false;
			((RenderTexture)processedEdgeTexture).Create();
		}

		edgeSmoothingShader.SetFloat("threshold", laplacianThreshold);
		edgeSmoothingShader.SetInt("radius", dilateRadius);
		int kernel_id = edgeSmoothingShader.FindKernel("DepthProcessing");
		edgeSmoothingShader.SetTexture(kernel_id, "InputDepth", depth_texture);
		edgeSmoothingShader.SetTexture(kernel_id, "OutputTexture", processedEdgeTexture);
		edgeSmoothingShader.Dispatch(kernel_id, depthTexture.width, depthTexture.height, 1);

		return processedEdgeTexture;
	}

	/// <summary>
	/// Process data that is stored on GPU to create a point cloud out of the depth map. 
	/// Point UV volor mapping is computed using cameras intrinsics and transformation.
	/// </summary>
	/// <param name="depthData"></param>
	/// <param name="imageData"></param>
	private void CreatePointCloud(Texture depthData, Texture imageData)
	{
		InitTextures();
		Texture depth_texture = depthData;
		if (processDepthEdge)
		{
			depth_texture = ProcessEdge(depthData);
		}
		colorTexture = imageData;
		vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");
		TextureGenerator.SetFloats("SlideUV", slide_u, slide_v);
		TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Input_Depth", depth_texture);
		TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", (RenderTexture)vertexTexture);
		TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, depthTexture.width / 8, depthTexture.height / 8, 1);
	}

	/// <summary>
	/// Transfer the CPU data to GPU so that it can be processed by compute shader.
	/// </summary>
	/// <param name="depthData"></param>
	/// <param name="imageData"></param>
	private void CreatePointCloud(float[] depthData, byte[] imageData)
	{
		InitTextures();

		var depth_data = ((Texture2D)depthTexture).GetRawTextureData<float>();
		depth_data.CopyFrom(depthData);
		((Texture2D)depthTexture).Apply();

		((Texture2D)colorTexture).LoadRawTextureData(imageData);
		((Texture2D)colorTexture).Apply();

		CreatePointCloud(depthTexture, colorTexture);
	}
}
