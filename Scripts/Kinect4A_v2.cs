using System;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using CLARTE.Dev.Profiling;
using System.Threading.Tasks;
using Windows.Kinect;

public class Kinect4A_v2 : PointCloudProvider
{
    public ComputeShader TextureGenerator;
    //Variable for handling Kinect
    private Device kinect;
    //Number of all points of PointCloud 
    private int depthWidth;
    private int depthHeight;

    private byte[] vertices;
    private BGRA[] colors;

    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    private Transformation transformation;

    private ComputeBuffer inputPositionBuffer;
    private ComputeBuffer inputColorBuffer;

    protected RenderTexture vertexRenderTexture;
    protected RenderTexture colorRenderTexture;

    protected int vertexTextureGeneratorKernelId;
    protected int colorTextureGeneratorKernelId;

    void Start()
    {
        //The method to initialize Kinect
        InitKinect();
        //Initialization for point cloud rendering
        InitValues();
    }

    private void Update()
    {
        Profiler.Start("kinect capture");
        Capture capture = kinect.GetCapture();
        Profiler.Stop("kinect capture");

        //Getting color information
        Profiler.Start("Image array assignment");
        Image colorImage = transformation.ColorImageToDepthCamera(capture);
        colors = colorImage.GetPixels<BGRA>().ToArray();
        Profiler.Stop("Image array assignment");

        //Getting vertices of point cloud
        Profiler.Start("Position bytes assignment");
        Image xyzImage = transformation.DepthImageToPointCloud(capture.Depth);
        xyzImage.CopyBytesTo(vertices, 0, 0, vertices.Length);
        Profiler.Stop("Position bytes assignment");

        Profiler.Start("PointCloud computation");
        ComputePointCloudData();
        Profiler.Stop("PointCloud computation");
    }

    public static Vector3 ShortToVector(Short3 sh)
    {
        return new Vector3(sh.X, -sh.Y, sh.Z) * 0.001f;
    }

    private void OnDisable()
    {
        Profiler.DisplayAllAverages();
    }

    //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        kinect.StopCameras();
    }

    //Initialization of Kinect
    private void InitKinect()
    {
        //Connect with the 0th Kinect
        kinect = Device.Open(0);

        //Setting the Kinect operation mode and starting it
        kinect.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NFOV_Unbinned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30
        });
        //Access to coordinate transformation information
        transformation = kinect.GetCalibration().CreateTransformation();
        
    }

    //Prepare to draw point cloud.
    private void InitValues()
    {
        depthWidth = kinect.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = kinect.GetCalibration().DepthCameraCalibration.ResolutionHeight;
        
        vertices = new byte[depthWidth * depthHeight * 3 * 2]; // short3 = 3 * 2byte
    }
    
    private void ComputePointCloudData()
    {
        // Initialize compute buffers if needed
        if (inputPositionBuffer == null)
        {
            inputPositionBuffer = new ComputeBuffer(depthWidth * depthHeight * 3 / 2, sizeof(uint)); // bytes
            //inputPositionBuffer = new ComputeBuffer(depthWidth * depthHeight, 3 * sizeof(float)); // vectors
        }

        if (inputColorBuffer == null)
        {
            inputColorBuffer = new ComputeBuffer(depthWidth * depthHeight, sizeof(uint)); // Color
        }

        // Initialize rendertexture if needed
        if (vertexRenderTexture == null)
        {
            vertexRenderTexture = new RenderTexture(depthWidth, depthHeight, 0, RenderTextureFormat.ARGBFloat);
            vertexRenderTexture.enableRandomWrite = true;
            vertexRenderTexture.useMipMap = false;
            vertexRenderTexture.filterMode = FilterMode.Point;
            vertexRenderTexture.Create();

            vertexTexture = (Texture)vertexRenderTexture;
        }

        if (colorRenderTexture == null)
        {
            colorRenderTexture = new RenderTexture(depthWidth, depthHeight, 0, RenderTextureFormat.BGRA32);
            colorRenderTexture.enableRandomWrite = true;
            colorRenderTexture.useMipMap = false;
            colorRenderTexture.Create();

            colorTexture = (Texture)colorRenderTexture;
        }


        // Vertex generator
        vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");
        TextureGenerator.SetBuffer(vertexTextureGeneratorKernelId, "Input_positionData", inputPositionBuffer);
        inputPositionBuffer.SetData(vertices);
        //inputPositionBuffer.SetData(vertices);

        TextureGenerator.SetInt("depthWidth", depthWidth);
        TextureGenerator.SetInt("depthHeight", depthHeight);
        TextureGenerator.SetInt("colorWidth", depthWidth);
        TextureGenerator.SetInt("colorHeight", depthHeight);

        TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", vertexRenderTexture);

        colorTextureGeneratorKernelId = TextureGenerator.FindKernel("ColorGenerator");
        TextureGenerator.SetBuffer(colorTextureGeneratorKernelId, "Input_colorData", inputColorBuffer);
        inputColorBuffer.SetData(colors);

        TextureGenerator.SetTexture(colorTextureGeneratorKernelId, "Output_colorTexture", colorTexture);
        TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, depthWidth, depthHeight, 1);
        TextureGenerator.Dispatch(colorTextureGeneratorKernelId, depthWidth, depthHeight, 1);
    }
}
