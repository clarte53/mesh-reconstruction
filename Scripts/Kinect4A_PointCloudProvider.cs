#if USE_K4A
using UnityEngine;
using K4AdotNet.Sensor;
using CLARTE.Dev.Profiling;
using System.Threading;
using K4AdotNet.Samples.Unity;

public class Kinect4A_PointCloudProvider : PointCloudProvider
{
    #region Public Variables
    public ComputeShader TextureGenerator;
    #endregion

    #region Private Variables
    private Kinect4AManager kinectManager;
    
    private bool isInitialized = false;

    // Values for data storage
    private int depthWidth;
    private int depthHeight;

    private byte[] vertices;
    private byte[] colors;
    
    private Image xyzImage;

    // Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    private Transformation transformation;

    // Values for rendering
    private ComputeBuffer inputPositionBuffer;
    private ComputeBuffer inputColorBuffer;

    protected RenderTexture vertexRenderTexture;
    protected RenderTexture colorRenderTexture;

    protected int vertexTextureGeneratorKernelId;
    protected int colorTextureGeneratorKernelId;

    // Values for threading
    protected Thread captureThread;
    protected readonly System.Object doneLock = new System.Object();
    protected bool done = false;
    Capture capture = new Capture();
    bool isCaptureDirty;

    #endregion

    #region Getter
    public byte[] GetPointCloud()
    {
        if(xyzImage != null)
        {
            return vertices;
        } else
        {
            return null;
        }
    }
    #endregion

    #region Monobehaviour callbacks
    void OnEnable()
    {
        if (!isInitialized)
        {
            InitKinect();

            
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
        
        captureThread?.Join();
        inputPositionBuffer?.Dispose();
        inputColorBuffer?.Dispose();
        vertexRenderTexture?.Release();
        colorRenderTexture?.Release();

        kinectManager.CaptureReady -= HandleCapture;
        //Profiler.DisplayAllAverages();
    }
    #endregion


    #region Private methods
    //Initialization of Kinect
    private void InitKinect()
    {
        kinectManager = FindObjectOfType<Kinect4AManager>();
        if (kinectManager == null)
        {
            Debug.LogError("Could not find object of type Kinect4AManager, please add one to your scene");
        }
        kinectManager.CaptureReady += HandleCapture;

        

    }

    //Prepare to draw point cloud.
    private void InitValues()
    {
        depthWidth = kinectManager.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = kinectManager.GetCalibration().DepthCameraCalibration.ResolutionHeight;

        vertices = new byte[depthWidth * depthHeight * 3 * 2]; // short3 = 3 * 2byte
        colors = new byte[depthWidth * depthHeight * 4];

        xyzImage = new Image(ImageFormat.Custom, depthWidth, depthHeight, 6 * depthWidth);
    }

    private void HandleCapture(object sender, CaptureEventArgs captureArg)
    {
        lock (capture)
        {
            capture = captureArg.Capture;
            isCaptureDirty = true;
        }

		if(xyzImage == null)
		{
			InitValues();

			captureThread = new Thread(UpdateKinectData);
			captureThread.Start();

			//Access to coordinate transformation information
			transformation = kinectManager.GetTransformation();

			isInitialized = true;
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

    private void UpdateKinectData()
    {
        bool _done = false;

        Capture _capture = null;
        Chrono chrono = new Chrono();
        chrono.Start();

        double dt;
        switch (kinectManager._cameraFPS)
        {
            case FrameRate.Fifteen:
                dt = 1 / 15;
                break;
            case FrameRate.Thirty:
                dt = 1 / 30;
                break;
            case FrameRate.Five:
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

                lock (capture)
                {
                    if (isCaptureDirty)
                    {
                        _capture = capture;
                        isCaptureDirty = false;
                    }
                }
                if(_capture != null)
                {
                    UpdateDepthData(_capture);
                    UpdateColorData(_capture);
                    _capture = null;
                }
            }

            lock (doneLock)
            {
                _done = done;
            }

            Thread.Sleep(0);
        }
    }

    private void UpdateDepthData(Capture capture)
    {
        //Getting vertices of point cloud
        transformation.DepthImageToPointCloud(capture.DepthImage, CalibrationGeometry.Depth, xyzImage);
        if (xyzImage != null)
        {
            lock (vertices)
            {
                xyzImage.CopyTo(vertices);
            }
        }
    }

    private void UpdateColorData(Capture capture)
    {
        //Getting color information
        Image colorImage = new Image(ImageFormat.ColorBgra32, depthWidth, depthHeight);
        transformation.ColorImageToDepthCamera(capture.DepthImage, capture.ColorImage, colorImage);
        if (colorImage != null)
        {
            lock (colors)
            {
                colorImage.CopyTo(colors);
            }
            colorImage.Dispose();
        }
    }
    
    #endregion
}
#endif