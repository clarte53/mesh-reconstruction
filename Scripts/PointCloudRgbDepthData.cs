using System;
#if USE_STRUCTURECORE
using StructureCoreAPI;
#endif

[System.Serializable]
public class PointCloudRgbDepthData
{
	public int[] depthSize = new int[2];
	public float[] depthIntrinsec = new float[4];
	public int[] imageSize = new int[2];
	public float[] imageIntrinsec = new float[4];
	public float[] imagePoseToDepthCoords = new float[16];
	public float[] depthData;
	public byte[] colorData;
	public long millisec = 0;

	public PointCloudRgbDepthData()
	{
		millisec = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
	}

#if USE_STRUCTURECORE
	public void PopulateWithStructureCore()
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
		Buffer.BlockCopy(m, 0, imagePoseToDepthCoords, 0, m.Length * sizeof(float));

		depthSize[0] = StructureCore.Instance.DepthWidth;
		depthSize[1] = StructureCore.Instance.DepthHeight;

		imageSize[0] = StructureCore.Instance.ImageWidth;
		imageSize[1] = StructureCore.Instance.ImageHeight;

		if (depthData == null)
		{
			depthData = new float[StructureCore.Instance.DepthData.Length];
		}
		Buffer.BlockCopy(StructureCore.Instance.DepthData, 0, depthData, 0, StructureCore.Instance.DepthData.Length * sizeof(float));

		if (colorData == null)
		{
			colorData = new byte[StructureCore.Instance.ImageData.Length];
		}
		Buffer.BlockCopy(StructureCore.Instance.ImageData, 0, colorData, 0, StructureCore.Instance.ImageData.Length * sizeof(byte));
	}
#endif
}
