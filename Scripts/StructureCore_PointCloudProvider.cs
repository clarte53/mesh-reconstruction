#if USE_STRUCTURECORE
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StructureCoreAPI;
using Unity.Collections;
using System.IO;

public class StructureCore_PointCloudProvider : PointCloudProvider
{

	bool _isInitialized = false;
	public bool useShader = false;
	public bool DepthCorrection = false;
	public bool flip_x;
	public bool flip_y;
	public bool flip_z;
	public bool flip_r;
	public bool flip_c;
	public float slide_u = 0.1f;
	public float slide_v = 0f;
	public Texture2D depthTexture;

	public ComputeShader TextureGenerator;

	private bool usingShader = false;
	private float[] depthIntrinsec = new float[4];
	private float[] imageIntrinsec = new float[4];
	private Matrix4x4 imagePoseToDepthCoords;

	protected int vertexTextureGeneratorKernelId;

	private void OnEnable()
	{
		if (!_isInitialized)
		{
			StructureCore.Instance.ApplyExpensiveCorrection = DepthCorrection;
			usingShader = useShader;
			if (!usingShader)
			{
				StructureCore.Instance.flip_x = flip_x;
				StructureCore.Instance.flip_y = flip_y;
				StructureCore.Instance.flip_z = flip_z;
				StructureCore.Instance.flip_r = flip_r;
				StructureCore.Instance.flip_c = flip_c;
				StructureCore.Instance.slide_u = slide_u;
				StructureCore.Instance.slide_v = slide_v;
			}
			StructureCore.Instance.PointCloudEnable = !usingShader;
			_isInitialized = true;
		}
	}

	System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

	public void CreatePointCloud()
	{
		Debug.Log("FPS=" + (1000f / (float)sw.ElapsedMilliseconds) + " ms=" + sw.ElapsedMilliseconds);
		sw.Restart();

		if (usingShader != useShader)
		{
			vertexTexture = null;
			usingShader = useShader;
			StructureCore.Instance.PointCloudEnable = !usingShader;
		}

		if (usingShader)
		{
			GPUComputePointCloudData();
		}
		else
		{
			CPUComputePointCloudData();
		}

		// Apply config change
		StructureCore.Instance.ApplyExpensiveCorrection = DepthCorrection;
		if (!usingShader)
		{
			StructureCore.Instance.flip_x = flip_x;
			StructureCore.Instance.flip_y = flip_y;
			StructureCore.Instance.flip_z = flip_z;
			StructureCore.Instance.flip_r = flip_r;
			StructureCore.Instance.flip_c = flip_c;
		}
		else
		{
			StructureCore.Instance.flip_x = false;
			StructureCore.Instance.flip_y = false;
			StructureCore.Instance.flip_z = false;
			StructureCore.Instance.flip_r = false;
			StructureCore.Instance.flip_c = false;
		}
		StructureCore.Instance.slide_u = slide_u;
		StructureCore.Instance.slide_v = slide_v;
	}

	private void GPUComputePointCloudData()
	{
		if (vertexTexture == null)
		{
			depthIntrinsec[0] = StructureCore.Instance.DepthIntrinsec.fx;
			depthIntrinsec[1] = StructureCore.Instance.DepthIntrinsec.fy;
			depthIntrinsec[2] = StructureCore.Instance.DepthIntrinsec.cx;
			depthIntrinsec[3] = StructureCore.Instance.DepthIntrinsec.cy;

			imageIntrinsec[0] = StructureCore.Instance.ImageIntrinsec.fx;
			imageIntrinsec[1] = StructureCore.Instance.ImageIntrinsec.fy;
			imageIntrinsec[2] = StructureCore.Instance.ImageIntrinsec.cx;
			imageIntrinsec[3] = StructureCore.Instance.ImageIntrinsec.cy;

			float[] m = StructureCore.Instance.ImagePoseInDepthCoords;
			imagePoseToDepthCoords = new Matrix4x4();
			imagePoseToDepthCoords.SetRow(0, new Vector4(m[0], m[1], m[2], m[3]));
			imagePoseToDepthCoords.SetRow(1, new Vector4(m[4], m[5], m[6], m[7]));
			imagePoseToDepthCoords.SetRow(2, new Vector4(m[8], m[9], m[10], m[11]));
			imagePoseToDepthCoords.SetRow(3, new Vector4(m[12], m[13], m[14], m[15]));

			depthTexture = new Texture2D(StructureCore.Instance.DepthWidth, StructureCore.Instance.DepthHeight, TextureFormat.RFloat, false);

			vertexTexture = new RenderTexture(StructureCore.Instance.DepthWidth, StructureCore.Instance.DepthHeight, 0, RenderTextureFormat.ARGBFloat);
			((RenderTexture)vertexTexture).enableRandomWrite = true;
			((RenderTexture)vertexTexture).useMipMap = false;
			((RenderTexture)vertexTexture).Create();

			colorTexture = new Texture2D(StructureCore.Instance.ImageWidth, StructureCore.Instance.ImageHeight, TextureFormat.RGB24, false);

			TextureGenerator.SetMatrix("ImagePoseInDepthCoords", imagePoseToDepthCoords);
			TextureGenerator.SetFloats("DepthIntrinsec", depthIntrinsec);
			TextureGenerator.SetFloats("ImageIntrinsec", imageIntrinsec);
			TextureGenerator.SetInt("DepthWidth", StructureCore.Instance.DepthWidth);
			TextureGenerator.SetInt("DepthHeight", StructureCore.Instance.DepthHeight);
			TextureGenerator.SetInt("ColorWidth", StructureCore.Instance.ImageWidth);
			TextureGenerator.SetInt("ColorHeight", StructureCore.Instance.ImageHeight);
		}

		var depth_data = depthTexture.GetRawTextureData<float>();
		depth_data.CopyFrom(StructureCore.Instance.DepthData);
		depthTexture.Apply();

		((Texture2D)colorTexture).LoadRawTextureData(StructureCore.Instance.ImageData);
		((Texture2D)colorTexture).Apply();

		vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");
		TextureGenerator.SetFloats("SlideUV", slide_u, slide_v);
		TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Input_Depth", depthTexture);
		TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", (RenderTexture)vertexTexture);
		TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, StructureCore.Instance.DepthWidth/8, StructureCore.Instance.DepthHeight/8, 1);
	}

	private void CPUComputePointCloudData()
	{
		if (vertexTexture == null)
		{
			vertexTexture = new Texture2D(StructureCore.Instance.DepthWidth, StructureCore.Instance.DepthHeight, TextureFormat.RGBAFloat, false);
			colorTexture = new Texture2D(StructureCore.Instance.ImageWidth, StructureCore.Instance.ImageHeight, TextureFormat.RGB24, false);
		}

		NativeArray<float> data = ((Texture2D)vertexTexture).GetRawTextureData<float>();
		data.CopyFrom(StructureCore.Instance.PointCloudData);
		((Texture2D)vertexTexture).Apply();

		((Texture2D)colorTexture).LoadRawTextureData(StructureCore.Instance.ImageData);
		((Texture2D)colorTexture).Apply();

		//if (!File.Exists("depth.png"))
		//{
		//	var tex = new Texture2D(StructureCore.Instance.DepthWidth, StructureCore.Instance.DepthHeight, TextureFormat.RFloat, false);
		//	NativeArray<float> td = tex.GetRawTextureData<float>();
		//	td.CopyFrom(StructureCore.Instance.DepthData);
		//	tex.Apply();
		//	File.WriteAllBytes("depth.png", tex.EncodeToPNG());
		//}
	}
}
#endif
