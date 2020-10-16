using Microsoft.Azure.Kinect.Sensor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Kinect4AManager : MonoBehaviour
{
    // This class centralizes the different calls to kinect so the device is only opened and closed once
    #region Members
    [Header("Camera settings")]
    public ImageFormat _imageFormat = ImageFormat.ColorBGRA32;
    public ColorResolution _colorResolution = ColorResolution.R720p;
    public DepthMode _depthMode = DepthMode.NFOV_Unbinned;
    public bool _synchronizedImagesOnly = true;
    public FPS _cameraFPS = FPS.FPS30;

    private Device kinect;
    private int deviceID = 0;
    private bool kinectIsOn = false;
    #endregion

    #region Getter
    public Device GetKinect()
    {
        if (!kinectIsOn)
        {
            return null;
        }
        else
        {
            return kinect;
        }
    }
    #endregion

    #region public methods
    /// <summary>
    /// Open kinect if it has not been done already 
    /// </summary>
    public void StartCamera()
    {
        if (!kinectIsOn)
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

    /// <summary>
    /// Stop kinect if it has not been done already
    /// </summary>
    public void StopCamera()
    {
        if (kinectIsOn)
        {
            kinectIsOn = false;
            kinect.StopCameras();
            kinect = null;
        }
    }
    #endregion 
}
