#if USE_KINECT
using CLARTE.Dev.Profiling;
using System.Threading;
using UnityEngine;
using Windows.Kinect;
using CLARTE.Geometry.Extensions;

public class Kinect_PointCloudProvider : PointCloudProvider
{
    #region Public Variables

    [HideInInspector]
    public int KinectDepthMapWidth;

    [HideInInspector]
    public int KinectDepthMapHeight;

    [HideInInspector]
    public int KinectColorMapWidth;

    [HideInInspector]
    public int KinectColorMapHeight;
    #endregion

    #region Private Variables

    private KinectSensor _sensor;
    private CoordinateMapper _mapper;

    private DepthFrameReader _depthReader;
    private ColorFrameReader _colorReader;

    private byte[] _colorData;
    private ushort[] _depthData;

    private CameraSpacePoint[] _cameraSpace;
    private ColorSpacePoint[] _colorSpace;


    protected bool _isInitialized = false;

    protected Thread captureThread;
    protected readonly System.Object doneLock = new System.Object();
    protected bool done = false;

	protected RenderTexture vertexRenderTexture;
	protected RenderTexture colorRenderTexture;
    #endregion

    #region Compute Shader Variables

    public ComputeShader TextureGenerator;

    private ComputeBuffer inputPositionBuffer;
    private ComputeBuffer inputUVBuffer;
    private ComputeBuffer inputColorBuffer;

	protected int vertexTextureGeneratorKernelId;
	protected int colorTextureGeneratorKernelId;
	

    #endregion

    #region Unity Functions

    /// <summary>
    /// Initialize Sensor
    /// </summary>
    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeKinect();
        }
    }

    /// <summary>
    /// Update point cloud Data every frame based on kinect data
    /// </summary>
	private void Update()
    {
        ComputePointCloudData();
    }

    /// <summary>
    /// Releasing Textures and buffers
    /// </summary>
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

        // Releasing compute buffers
        inputPositionBuffer.Dispose();
        inputColorBuffer.Dispose();
        inputUVBuffer.Dispose();

		// Releasing Textures 
		vertexRenderTexture.Release();
		

		
		colorRenderTexture.Release();
		
    }

    #endregion

    #region Private Functions

    /// <summary>
    /// Initialize the sensor
    /// </summary>
    private void InitializeKinect()
    {
        // Init Kinect
        _sensor = KinectSensor.GetDefault();

        if (_sensor != null)
        {
            // Depth
            _depthReader = _sensor.DepthFrameSource.OpenReader();

            FrameDescription depthFrameDesc = _sensor.DepthFrameSource.FrameDescription;

            KinectDepthMapWidth = depthFrameDesc.Width;

            KinectDepthMapHeight = depthFrameDesc.Height;

            _depthData = new ushort[depthFrameDesc.LengthInPixels];

            _depthData.Populate<ushort>(0);

            // Color
            _colorReader = _sensor.ColorFrameSource.OpenReader();

            FrameDescription colorFrameDesc = _sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            KinectColorMapWidth = colorFrameDesc.Width;

            KinectColorMapHeight = colorFrameDesc.Height;

            _colorData = new byte[colorFrameDesc.BytesPerPixel * colorFrameDesc.LengthInPixels];

            // coordinates mapper
            _mapper = _sensor.CoordinateMapper;

            // Open sensor
            if (!_sensor.IsOpen)
            {
                _sensor.Open();
            }

            captureThread = new Thread(UpdateKinectData);

            captureThread.Start();

            _isInitialized = true;
        }
        else
        {
            Debug.LogError(this.name + " sensor not found");
        }
    }

    /// <summary>
    /// Update Kinect depth map
    /// </summary>
    private void UpdateDepthData()
    {
        if (_isInitialized)
        {
            DepthFrame depthFrame = _depthReader.AcquireLatestFrame();

            if (depthFrame != null)
            {
                depthFrame.CopyFrameDataToArray(_depthData);

                depthFrame.Dispose();

                depthFrame = null;
            }
        }
    }

    /// <summary>
    /// Update Kinect Color map
    /// </summary>
    private void UpdateColorData()
    {
        ColorFrame colorFrame = _colorReader.AcquireLatestFrame();

        if (colorFrame != null)
        {
            lock (_colorData)
            {
                colorFrame.CopyConvertedFrameDataToArray(_colorData, ColorImageFormat.Bgra);
            }

            colorFrame.Dispose();

            colorFrame = null;
        }
    }

    /// <summary>
    /// Compute XYZ and colors datas by generating rendertextures
    /// Uses a compute shader TextureGPUGenerator
    /// Fills XYZRenderTexture and ColorRenderTexture
    /// </summary>
    private void ComputePointCloudData()
    {
        if (_isInitialized && _cameraSpace != null && _colorSpace != null)
        {
            // Initialize compute buffers if needed
            if (inputPositionBuffer == null)
            {
                lock (_cameraSpace)
                {
                    inputPositionBuffer = new ComputeBuffer(_cameraSpace.Length, 3 * sizeof(float));
                }
            }


            if (inputUVBuffer == null)
            {
                lock (_colorSpace)
                {
                    inputUVBuffer = new ComputeBuffer(_colorSpace.Length, 2 * sizeof(float));
                }
            }

            if (inputColorBuffer == null)
            {
                lock (_colorData)
                {
                    inputColorBuffer = new ComputeBuffer(_colorData.Length / 4, sizeof(uint));
                }
            }

          

            // Initialize rendertexture if needed
            if (vertexRenderTexture == null)
            {
				vertexRenderTexture = new RenderTexture(KinectDepthMapWidth, KinectDepthMapHeight, 0, RenderTextureFormat.ARGBFloat);
				vertexRenderTexture.enableRandomWrite = true;
				vertexRenderTexture.useMipMap = false;
				vertexRenderTexture.filterMode = FilterMode.Point;
				vertexRenderTexture.Create();

				vertexTexture = (Texture)vertexRenderTexture;
			}

            if (colorRenderTexture == null)
            {
				colorRenderTexture = new RenderTexture(KinectColorMapWidth, KinectColorMapHeight, 0, RenderTextureFormat.BGRA32);
				colorRenderTexture.enableRandomWrite = true;
				colorRenderTexture.useMipMap = false;
				colorRenderTexture.Create();

				colorTexture = (Texture)colorRenderTexture;
            }


			// Vertex generator
			vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");

            TextureGenerator.SetBuffer(vertexTextureGeneratorKernelId, "Input_positionData", inputPositionBuffer);

            lock (_cameraSpace)
            {
                inputPositionBuffer.SetData(_cameraSpace);
            }

			TextureGenerator.SetBuffer(colorTextureGeneratorKernelId, "Input_uvData", inputUVBuffer);

			lock(_colorSpace)
			{
				inputUVBuffer.SetData(_colorSpace);
			}

			TextureGenerator.SetInt("depthWidth", KinectDepthMapWidth);
			TextureGenerator.SetInt("depthHeight", KinectDepthMapHeight);
			TextureGenerator.SetInt("colorWidth", KinectColorMapWidth);
			TextureGenerator.SetInt("colorHeight", KinectColorMapHeight);

			// output
			TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", vertexRenderTexture);

			// Color generator
			colorTextureGeneratorKernelId = TextureGenerator.FindKernel("ColorGenerator");

            TextureGenerator.SetBuffer(colorTextureGeneratorKernelId, "Input_colorData", inputColorBuffer);

            lock (_colorData)
            {
                inputColorBuffer.SetData(_colorData);
			}
		
			// output
			TextureGenerator.SetTexture(colorTextureGeneratorKernelId, "Output_colorTexture", colorTexture);

            TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, KinectDepthMapWidth, KinectDepthMapHeight, 1);
            TextureGenerator.Dispatch(colorTextureGeneratorKernelId, KinectColorMapWidth, KinectColorMapHeight, 1);
            // Texture filled.
        }
    }

    /// <summary>
    /// Update kinect data in multithread
    /// </summary>
    private void UpdateKinectData()
    {
        bool _done = false;

        Chrono chrono = new Chrono();

        chrono.Start();

        double dt = 0.033;

        double next_schedule = 0.0;

        Debug.Log("Kinect acquisition started");

        while (!_done)
        {
            double current_time = chrono.GetElapsedTime();

            if (current_time > next_schedule)
            {
                next_schedule += dt;

                // Get depth map
                UpdateDepthData();

                // Get color map
                UpdateColorData();

                // Convert depth map into point cloud
                _cameraSpace = new CameraSpacePoint[_depthData.Length];

                lock (_cameraSpace)
                {
                    _mapper.MapDepthFrameToCameraSpace(_depthData, _cameraSpace);
                }

                // Map depth map to color map
                _colorSpace = new ColorSpacePoint[_depthData.Length];

                lock (_colorSpace)
                {
                    _mapper.MapDepthFrameToColorSpace(_depthData, _colorSpace);
                }
            }

            lock (doneLock)
            {
                _done = done;
            }

            Thread.Sleep(0);
        }

        Debug.Log("Kinect acquisition stopped");
    }
	#endregion
}
#endif
