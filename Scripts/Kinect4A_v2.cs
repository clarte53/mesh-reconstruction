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

    private ComputeBuffer inputPositionBuffer;
    private ComputeBuffer inputColorBuffer;
    //Variable for handling Kinect

    Device kinect;
    //Number of all points of PointCloud 
    int depthWidth;
    int depthHeight;
  
    //Used to draw a set of points
    Mesh mesh;
    //Array of coordinates for each point in PointCloud
    Vector3[] vertices;
    Color[] testArray;
    byte[] vs;
    //Array of colors corresponding to each point in PointCloud
    BGRA[] colors;
    public Texture2D tex;
    public Renderer quad;

    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    Transformation transformation;
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
        Capture capture = kinect.GetCapture();

        //Getting color information
        Image colorImage = transformation.ColorImageToDepthCamera(capture);
        colors = colorImage.GetPixels<BGRA>().ToArray();

        //Getting vertices of point cloud
        Image xyzImage = transformation.DepthImageToPointCloud(capture.Depth);
        Short3[] xyzArray = xyzImage.GetPixels<Short3>().ToArray();
        xyzImage.CopyBytesTo(vs, 0, 0, vs.Length);

        for (int i = 0; i < depthWidth * depthHeight; i++)
        {
            vertices[i].x = xyzArray[i].X * 0.001f;
            vertices[i].y = -xyzArray[i].Y * 0.001f;
            vertices[i].z = xyzArray[i].Z * 0.001f;

            //int r = BitConverter.ToInt16(new byte[2] { vs[6 * i], vs[6 * i + 1] }, 0);
            //int g = BitConverter.ToInt16(new byte[2] { vs[6 * i + 2], vs[6 * i + 3] }, 0);
            //int b = BitConverter.ToInt16(new byte[2] { vs[6 * i + 4], vs[6 * i + 5] }, 0);

            //testArray[i] = new Color(r * 0.001f, g * 0.001f, b * 0.001f);
        }

        ComputePointCloudData();
        quad.material.mainTexture = vertexTexture;
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
        //Get the width and height of the Depth image and calculate the number of all points
        depthWidth = kinect.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = kinect.GetCalibration().DepthCameraCalibration.ResolutionHeight;
        int num = depthWidth * depthHeight;

        //Allocation of vertex and color storage space for the total number of pixels in the depth image
        vertices = new Vector3[depthWidth * depthHeight];
        vs = new byte[depthWidth * depthHeight * 3 * 2]; // short3 = 3 * 2byte
        testArray = new Color[depthWidth * depthHeight];
        tex = new Texture2D(depthWidth, depthHeight);
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
        inputPositionBuffer.SetData(vs);
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

    float EncodeUV(Vector2 uv)
    {
        //quantize from 0-1 floats, to 0-65535 integers, which can be represented in 16 bits
        uint u = (uint)Mathf.Round(uv.x * 65535.0f);
        uint v = (uint)Mathf.Round(uv.y * 65535.0f);

        uint encoded = u << 16 | v;

        return (float)encoded;
    }
}
