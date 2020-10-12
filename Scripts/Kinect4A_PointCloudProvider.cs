#if USE_K4A
using System;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using CLARTE.Dev.Profiling;
using System.Threading.Tasks;
using Windows.Kinect;
using System.Threading;

public class Kinect4A_PointCloudProvider : PointCloudProvider
{
    #region Public Variables
    public ComputeShader TextureGenerator;

    [Header("Camera settings")]
    public ImageFormat _imageFormat = ImageFormat.ColorBGRA32;
    public ColorResolution _colorResolution = ColorResolution.R720p;
    public DepthMode _depthMode = DepthMode.NFOV_Unbinned;
    public bool _synchronizedImagesOnly = true;
    public FPS _cameraFPS = FPS.FPS30;
    #endregion

    #region Private Variables
    //Variable for handling Kinect
    private Device kinect;
    private bool isInitialized = false;
    //Number of all points of PointCloud 
    private int depthWidth;
    private int depthHeight;

    private byte[] vertices;
    private BGRA[] colors;

    private Image xyzImage;
    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    private Transformation transformation;

    private ComputeBuffer inputPositionBuffer;
    private ComputeBuffer inputColorBuffer;

    protected RenderTexture vertexRenderTexture;
    protected RenderTexture colorRenderTexture;

    protected int vertexTextureGeneratorKernelId;
    protected int colorTextureGeneratorKernelId;

    protected Thread captureThread;
    protected readonly System.Object doneLock = new System.Object();
    protected bool done = false;
    #endregion

    #region Monobehaviour callbacks
    void OnEnable()
    {
        if (!isInitialized)
        {
            InitKinect();
            InitValues();

            captureThread = new Thread(UpdateKinectData);
            captureThread.Start();

            isInitialized = true;
        }
    }

    private void Update()
    {
        ComputePointCloudData();
    }

    private void OnDisable()
    {
        // Close acquisition thread
        lock (doneLock)
        {
            done = true;
        }

        if (captureThread != null)
        {
            captureThread.Join();
        }

        inputPositionBuffer.Dispose();
        inputColorBuffer.Dispose();

        vertexRenderTexture.Release();
        colorRenderTexture.Release();
        
        LogInFile.DumpAllLogs();
    }

    //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        kinect.StopCameras();
    }
    #endregion


    //Initialization of Kinect
    private void InitKinect()
    {
        //Connect with the 0th Kinect
        kinect = Device.Open(0);

        //Setting the Kinect operation mode and starting it
        kinect.StartCameras(new DeviceConfiguration
        {
            ColorFormat = _imageFormat,
            ColorResolution = _colorResolution,
            DepthMode = _depthMode,
            SynchronizedImagesOnly = _synchronizedImagesOnly,
            CameraFPS = _cameraFPS
        });
        //Access to coordinate transformation information
        transformation = kinect.GetCalibration().CreateTransformation();
    }

    public Short3[] GetPointCloud()
    {
        Memory<Short3> xyzMemory;
        if (xyzImage != null)
        {
            lock (xyzImage)
            {
                xyzMemory = xyzImage.GetPixels<Short3>();
            }
            return xyzMemory.ToArray();
        }
        else
        {
            return null;
        }
    }

    //Prepare to draw point cloud.
    private void InitValues()
    {
        depthWidth = kinect.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = kinect.GetCalibration().DepthCameraCalibration.ResolutionHeight;

        vertices = new byte[depthWidth * depthHeight * 3 * 2]; // short3 = 3 * 2byte
        colors = new BGRA[depthWidth * depthHeight];
    }

    private void UpdateDepthData(Capture capture)
    {
        //Getting vertices of point cloud
        xyzImage = transformation.DepthImageToPointCloud(capture.Depth);
        if (xyzImage != null)
        {
            lock (vertices)
            {
                xyzImage.CopyBytesTo(vertices, 0, 0, vertices.Length);
            }
            xyzImage.Dispose();
        }
    }

    private void UpdateColorData(Capture capture)
    {
        //Getting color information
        Image colorImage = transformation.ColorImageToDepthCamera(capture);
        if (colorImage != null)
        {
            lock (colors)
            {
                colors = colorImage.GetPixels<BGRA>().ToArray();
            }
            colorImage.Dispose();
        }

    }

    private void UpdateKinectData()
    {
        bool _done = false;

        Chrono chrono = new Chrono();
        chrono.Start();

        double dt;
        switch (_cameraFPS)
        {
            case FPS.FPS15:
                dt = 1 / 15;
                break;
            case FPS.FPS30:
                dt = 1 / 30;
                break;
            case FPS.FPS5:
                dt = 1 / 5;
                break;
            default:
                dt = 1 / 60; // default unity fps
                break;
        }
        double next_schedule = 0.0;

        while (!_done)
        {
            double current_time = chrono.GetElapsedTime();

            if (current_time > next_schedule)
            {
                next_schedule += dt;

                Capture capture = kinect.GetCapture();
                if (capture != null)
                {
                    UpdateDepthData(capture);
                    UpdateColorData(capture);
                }
            }

            lock (doneLock)
            {
                _done = done;
            }

            Thread.Sleep(0);
        }
    }

    private void ComputePointCloudData()
    {
        if (isInitialized)
        {
            // Initialize compute buffers if needed
            if (inputPositionBuffer == null)
            {
                inputPositionBuffer = new ComputeBuffer(depthWidth * depthHeight * 3 / 2, sizeof(uint)); // bytes
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
            lock (vertices)
            {
                inputPositionBuffer.SetData(vertices);
            }

            TextureGenerator.SetInt("depthWidth", depthWidth);
            TextureGenerator.SetInt("depthHeight", depthHeight);
            TextureGenerator.SetInt("colorWidth", depthWidth);
            TextureGenerator.SetInt("colorHeight", depthHeight);

            TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", vertexRenderTexture);

            colorTextureGeneratorKernelId = TextureGenerator.FindKernel("ColorGenerator");
            TextureGenerator.SetBuffer(colorTextureGeneratorKernelId, "Input_colorData", inputColorBuffer);
            lock (colors)
            {
                inputColorBuffer.SetData(colors);
            }

            TextureGenerator.SetTexture(colorTextureGeneratorKernelId, "Output_colorTexture", colorTexture);
            TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, 10, 36, 1);
            TextureGenerator.Dispatch(colorTextureGeneratorKernelId, 10, 36, 1);
        }
    }
}
#endif