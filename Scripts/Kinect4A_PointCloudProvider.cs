using com.rfilkov.kinect;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;

public class Kinect4A_PointCloudProvider : PointCloudProvider
{

    public KinectManager kinectManager;
    public int sensorIndex;

    private bool texInitialized = false;
    private Texture2D tex;
    private ushort[] rawData;
    private Color[] pixels;
    private int imWidth;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        rawData = kinectManager.GetRawDepthMap(sensorIndex);
        colorTexture = kinectManager.GetColorImageTex(sensorIndex);

        if (! texInitialized)
        {
            tex = new Texture2D(kinectManager.GetDepthImageWidth(sensorIndex), kinectManager.GetDepthImageHeight(sensorIndex));
            pixels = new Color[rawData.Length];
            imWidth = kinectManager.GetDepthImageWidth(sensorIndex);
            texInitialized = true;
        }
        

        for (int i = 0; i < rawData.Length; i++)
        {
            int x = Mathf.FloorToInt(i / imWidth);
            int y = i % imWidth;
            Vector3 pos = kinectManager.MapDepthPointToSpaceCoords(sensorIndex, new Vector2(x, y), rawData[i], false);
            //Vector2 uv = kinectManager.MapDepthPointToColorCoords(sensorIndex, new Vector2(i, j), rawValue);
            pixels[i] = new Color(pos.x, pos.y, pos.z);//, EncodeUV(uv));
        }
        // = new Texture2D(depthTexture.width, depthTexture.height, TextureFormat.ARGB32, true);
        //for (int i = 0; i < depthTexture.width; i++)
        //{
        //    for (int j = 0; j < depthTexture.height; j++)
        //    {
        //        ushort rawValue = rawData[j * depthTexture.width + i];
        //        Vector3 pos = kinectManager.MapDepthPointToSpaceCoords(sensorIndex, new Vector2(i, j), rawValue, false);
        //        //Vector2 uv = kinectManager.MapDepthPointToColorCoords(sensorIndex, new Vector2(i, j), rawValue);
        //        Color c = new Color(pos.x, pos.y, pos.z);//, EncodeUV(uv));
        //        tex.SetPixel(i, j, c);

        //    }
        //}
        
        tex.SetPixels( pixels);
        tex.Apply();
        vertexTexture = tex;
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
