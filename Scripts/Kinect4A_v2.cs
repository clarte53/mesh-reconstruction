using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using System.Threading.Tasks;

public class Kinect4A_v2 : PointCloudProvider
{
    public ComputeShader TextureGenerator;

    private ComputeBuffer inputPositionBuffer;
    //Variable for handling Kinect
    Device kinect;
    //Number of all points of PointCloud 
    int depthWidth;
    int depthHeight;
    int colorWidth;
    int colorHeight;
    //Used to draw a set of points
    Mesh mesh;
    //Array of coordinates for each point in PointCloud
    Vector3[] vertices;
    //Array of colors corresponding to each point in PointCloud
    Color[] colors;
    //List of indexes of points to be rendered
    int[] indices;
    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    Transformation transformation;
    protected RenderTexture vertexRenderTexture;
    protected int vertexTextureGeneratorKernelId;

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
        
        Texture2D colorTex = new Texture2D(colorWidth, colorHeight);
        Texture2D depthTex = new Texture2D(depthWidth, depthHeight);

        //Getting color information
        Image colorImage = capture.Color;
        BGRA[] colorArray = colorImage.GetPixels<BGRA>().ToArray();
        ////Getting vertices of point cloud
        Image xyzImage = transformation.DepthImageToPointCloud(capture.Depth);
        Short3[] xyzArray = xyzImage.GetPixels<Short3>().ToArray();

        //Image depthToColorImage = transformation.DepthImageToColorCamera(capture.Depth);

        for (int i = 0; i < depthWidth * depthHeight; i++)
        {
            vertices[i].x = xyzArray[i].X * 0.001f;
            vertices[i].y = -xyzArray[i].Y * 0.001f;
            vertices[i].z = xyzArray[i].Z * 0.001f;

            int x = Mathf.FloorToInt(i / depthWidth);
            int y = i % depthWidth;
            //vertices[i].a = EncodeUV(new Vector2(Mathf.Floor((x / depthWidth) * colorWidth),
            //    Mathf.Floor( (y / depthHeight) * colorHeight)));
           // vertices[i].a = EncodeUV(new Vector2(x, y));
        }
        
        for (int i = 0; i < colorWidth * colorHeight; i++)
        {
            
            colors[i].b = colorArray[i].B / 255f;
            colors[i].g = colorArray[i].G / 255f;
            colors[i].r = colorArray[i].R / 255f;
        }
        colorTex.SetPixels(colors);
        colorTex.Apply();
        colorTexture = colorTex;

        ComputePointCloudData();
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

        colorWidth = kinect.GetCalibration().ColorCameraCalibration.ResolutionWidth;
        colorHeight = kinect.GetCalibration().ColorCameraCalibration.ResolutionHeight;
        
        //Allocation of vertex and color storage space for the total number of pixels in the depth image
        vertices = new Vector3[depthWidth * depthHeight];
        colors = new Color[colorWidth * colorHeight];
    }
    
    float EncodeUV(Vector2 uv)
    {
        //quantize from 0-1 floats, to 0-65535 integers, which can be represented in 16 bits
        uint u = (uint)Mathf.Round(uv.x * 65535.0f);
        uint v = (uint)Mathf.Round(uv.y * 65535.0f);

        uint encoded = u << 16 | v;

        return (float)encoded;
    }

    //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        kinect.StopCameras();
    }

    private void ComputePointCloudData()
    {
        // Initialize compute buffers if needed
        if (inputPositionBuffer == null)
        {
            inputPositionBuffer = new ComputeBuffer(depthWidth * depthHeight, 3 * sizeof(float));
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

        // Vertex generator
        vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");
        TextureGenerator.SetBuffer(vertexTextureGeneratorKernelId, "Input_positionData", inputPositionBuffer);
        inputPositionBuffer.SetData(vertices);

        TextureGenerator.SetInt("depthWidth", depthWidth);
        TextureGenerator.SetInt("depthHeight", depthHeight);
        TextureGenerator.SetInt("colorWidth", colorWidth);
        TextureGenerator.SetInt("colorHeight", colorHeight);

        TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", vertexRenderTexture);
        TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, depthWidth, depthHeight, 1);
    }
}
