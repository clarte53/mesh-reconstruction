using Microsoft.Azure.Kinect.Sensor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Kinect4AManager : MonoBehaviour
{
    [Header("Camera settings")]
    public ImageFormat _imageFormat = ImageFormat.ColorBGRA32;
    public ColorResolution _colorResolution = ColorResolution.R720p;
    public DepthMode _depthMode = DepthMode.NFOV_Unbinned;
    public bool _synchronizedImagesOnly = true;
    public FPS _cameraFPS = FPS.FPS30;

    private Device kinect;
    private int deviceID = 0;
    private bool kinectIsOn = false;
    
    public Device GetKinect()
    {
        if(!kinectIsOn)
        {
            return null;
        } else
        {
            return kinect;
        }
    }
    public void StartCamera()
    {
        if(!kinectIsOn)
        {
            DeviceConfiguration deviceConfiguration = new DeviceConfiguration
            {
                ColorFormat = _imageFormat,
                ColorResolution = _colorResolution,
                DepthMode = _depthMode,
                SynchronizedImagesOnly = _synchronizedImagesOnly,
                CameraFPS = _cameraFPS
            };
            kinect = Device.Open(deviceID);
            kinect.StartCameras(deviceConfiguration);
            kinectIsOn = true;
        }
    }

    public void StopCamera()
    {
        if(kinectIsOn)
        {
            kinectIsOn = false;
            kinect.StopCameras();
            kinect = null;
            
        }
    }
}
