using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Record3D;
using System;
using Unity.Collections;

public class Record3D_PointCloudProvider : PointCloudProvider
{
    [SerializeField]
    protected int deviceId;

    protected bool isConnected = false;

    protected RenderTexture vertexRenderTexture;
    protected RenderTexture colorRenderTexture;

    public ComputeShader TextureGenerator;

    private ComputeBuffer inputPositionBuffer;
    private ComputeBuffer inputUVBuffer;
    private ComputeBuffer inputColorBuffer;

    protected int vertexTextureGeneratorKernelId;
    protected int colorTextureGeneratorKernelId;

    void OnEnable()
    {
        var frameMetadata = Record3DDeviceStream.frameMetadata;

        ReinitializeTextures(frameMetadata.width, frameMetadata.height);

        StartStreaming(deviceId);

        if(inputPositionBuffer == null)
        {
            inputPositionBuffer = new ComputeBuffer(frameMetadata.width * frameMetadata.height, 4 * sizeof(float));
        }

        //if(inputUVBuffer == null)
        //{
        //    inputUVBuffer = new ComputeBuffer(frameMetadata.width * frameMetadata.height, 2 * sizeof(float));
        //}

        //if(inputColorBuffer == null)
        //{
        //   inputColorBuffer = new ComputeBuffer(frameMetadata.width * frameMetadata.height, 3 * sizeof(byte));
        //}


        if(vertexRenderTexture == null)
        {
            vertexRenderTexture = new RenderTexture(frameMetadata.width, frameMetadata.height, 0, RenderTextureFormat.ARGBFloat);
            vertexRenderTexture.enableRandomWrite = true;
            vertexRenderTexture.useMipMap = false;
            vertexRenderTexture.filterMode = FilterMode.Point;
            vertexRenderTexture.Create();

            vertexTexture = (Texture)vertexRenderTexture;
        }

        //if(colorRenderTexture == null)
        //{
        //    colorRenderTexture = new RenderTexture(frameMetadata.width, frameMetadata.height, 0, RenderTextureFormat.BGRA32);
        //    colorRenderTexture.enableRandomWrite = true;
        //    colorRenderTexture.useMipMap = false;
        //    colorRenderTexture.Create();

        //    colorTexture = (Texture)colorRenderTexture;
        //}

        vertexTextureGeneratorKernelId = TextureGenerator.FindKernel("VertexGenerator");
        //colorTextureGeneratorKernelId = TextureGenerator.FindKernel("ColorGenerator");

        TextureGenerator.SetBuffer(vertexTextureGeneratorKernelId, "Input_positionData", inputPositionBuffer);
        //TextureGenerator.SetBuffer(colorTextureGeneratorKernelId, "Input_uvData", inputUVBuffer);
        //TextureGenerator.SetBuffer(colorTextureGeneratorKernelId, "Input_colorData", inputColorBuffer);

        TextureGenerator.SetInt("depthWidth", frameMetadata.width);
        TextureGenerator.SetInt("depthHeight", frameMetadata.height);
        TextureGenerator.SetInt("colorWidth", frameMetadata.width);
        TextureGenerator.SetInt("colorHeight", frameMetadata.height);

        TextureGenerator.SetTexture(vertexTextureGeneratorKernelId, "Output_vertexTexture", vertexRenderTexture);
        //TextureGenerator.SetTexture(colorTextureGeneratorKernelId, "Output_colorTexture", colorTexture);
    }

    // Update is called once per frame
    void Update()
    {
        if(isConnected)
        {
            //Record3DDeviceStream.positionsBuffer (4 floats per pixel)
            inputPositionBuffer.SetData(Record3DDeviceStream.positionsBuffer);


            ////Record3DDeviceStream.rgbBuffer (3 bytes per pixel)   
            //inputColorBuffer.SetData(Record3DDeviceStream.rgbBuffer);

            TextureGenerator.Dispatch(vertexTextureGeneratorKernelId, Record3DDeviceStream.frameMetadata.width, Record3DDeviceStream.frameMetadata.height, 1);
            //TextureGenerator.Dispatch(colorTextureGeneratorKernelId, Record3DDeviceStream.frameMetadata.width, Record3DDeviceStream.frameMetadata.height, 1);

            //var positionTexBufferSize = vertexTexture.width * vertexTexture.height * sizeof(float);
            //NativeArray<float>.Copy(Record3DDeviceStream.positionsBuffer, ((Texture2D)vertexTexture).GetRawTextureData<float>(), positionTexBufferSize);
            //((Texture2D)vertexTexture).Apply(false, false);


            const int numRGBChannels = 3;
            var colorTexBufferSize = colorTexture.width * colorTexture.height * numRGBChannels * sizeof(byte);
            NativeArray<byte>.Copy(Record3DDeviceStream.rgbBuffer, ((Texture2D)colorTexture).GetRawTextureData<byte>(), colorTexBufferSize);
            ((Texture2D)colorTexture).Apply(false, false);

        }
    }


    void StartStreaming(int devIdx)
    {
        var allAvailableDevices = Record3DDeviceStream.GetAvailableDevices();
        if(devIdx >= allAvailableDevices.Count)
        {
            Debug.LogError(string.Format("You selected device #{0} for streaming although only {1} devices was/were found. Please select appropriate device index (device IDs start from 0).", devIdx, allAvailableDevices.Count));
            return;
        }
        else
        {
            Debug.Log(string.Format("Device #{0} selected for streaming.", devIdx));
        }

        var selectedDevice = allAvailableDevices[devIdx];
        bool streamingSuccessfullyStarted = Record3DDeviceStream.StartStream(selectedDevice);

        isConnected = streamingSuccessfullyStarted;

        if(streamingSuccessfullyStarted)
        {
            Debug.Log(string.Format("Started Streaming with device #{0}.", selectedDevice));
        }
        else
        {
            Debug.LogError(string.Format("Could not start streaming with device #{0}. Ensure your iPhone/iPad is connected via USB, Record3D is running in USB Streaming Mode and that you have pressed the red toggle button. For more details read the Stick Note in VFX Graph Editor.", selectedDevice));
            return;
        }
    }

    void ReinitializeTextures(int width, int height)
    {
        Destroy(vertexTexture);
        Destroy(colorTexture);
        vertexTexture = null;
        colorTexture = null;
        Resources.UnloadUnusedAssets();

        vertexTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Point
        };

        colorTexture = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Point
        };

       
    }
}
