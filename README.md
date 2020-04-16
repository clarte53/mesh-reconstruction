Unity plugin for real-time reconstruction with an RGBD camera.
See dependencies.xml for required dependencies.

To add a new source:
- Add the ad-hoc driver/sdk for Unity as a dependency in a sibling folder
```bash
Assets
├───mesh-reconstruction
├───zed-unity
├───kinectv2-unity
├───mynewsource-unity
```
- In the `mesh-reconstruction > script folder`, create a new script that inherits from PointCloudProvider.
- In every Update, fill `PointCloudProvider.vertexTexture` and `PointCloudProvider.colorTexture` textures:
    - vertexTexture: 4 float-format texture (e.g. ARGBFloat) containing vertex attributes:
        - 1st float: x position
        - 2nd float: y position
        - 3rd float: z position
        - 4th float: uv texture coordinates, quantized to fit in 1 float (see methods EncodeUV() and DecodeUV() in ProceduralRenderingHelpers.cginc)
    - colorTexture: RGB texture from the camera, correctly oriented (correctly = consistent when displayed in Unity inspector panel).

To enable a source, add USE_[source_name] to the scripting define symbols. Currently supported sources are USE_ZED, USE_KINECT, and USE_PLY.