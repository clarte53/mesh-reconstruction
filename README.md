Unity plugin for real-time reconstruction with an RGBD camera.
Depends on [clarte-utils](https://github.com/clarte53/clarte-utils.git "clarte-utils") module.
Currently available sources include StereoLabs Zed/ZedMini and Kinect v2. A PLY file loader is available as well.

Getting started
===============
First, this repository as well as [clarte-utils](https://github.com/clarte53/clarte-utils.git "clarte-utils")
repository must be cloned into your Unity project as submodules inside the "Assets" directory. The required commit hash of the clarte-utils repository can be found in the "dependencies.xml" file. More recent commits might also work, but have not extensively been tested. clarte-utils is not directly provided as a submodule to avoid problems of multiple inclusion of the same submodule (in case of multiple modules depending on the same dependency). Therefore, the task to maintain coherency between armine and clarte-utils repositories is left to the developers.

Similarly, required sources must be cloned in the "Assets" folder as well. The following sources are currently supported:
- [Zed/ZedMini](https://github.com/stereolabs/zed-unity)
- [Kinect v2](https://github.com/clarte53/kinectv2-unity)

The only exception is the PLY file loader that does not require any dependency.

Then to enable a source it should be activated by adding USE_[source_name] to the scripting define symbols. Currently supported flags are USE_ZED, USE_KINECT, and USE_PLY.

In the case of a head-mounted RGBD camera, a [Camera Time Warping](https://ieeexplore.ieee.org/document/8699171) algoritm has been implemented. To achieve good results, head tracking sampling has to be performed at high frequency, which is not possible within Unity. Therefore an external process is required to allow for such high-freq sampling and then feed the Unity module. This process can be built from [this](https://github.com/clarte53/openvr-tracking) repo (commit bfe4ac1, others not tested). Pre-built binaries are available from the [Release](https://github.com/clarte53/openvr-tracking/releases/tag/0.1) tab. The executable is expected in the same folder as the calling application (or in the project folder for use in the Editor) and to be named 'openvr-tracking.exe'.

Usage
=====
- Add a point cloud provider component on a GameObject (Zed_PointCloudProvider, Kinect_PointCloudProvider, PLY_PointCloudProvider...)
- On the same GameObject, add a point cloud renderer component:
    - SimplePointCloudRenderer: draws every vertex as a quad (2 triangles). Edge size can be adjusted through the Size parameter;
    - MeshedPointCloudRenderer: requires a point cloud organized as an array of points. Connects every neighbour vertex with a triangle. Too long edges may be discarded by adjusting the MaxEdgeLength parameter.

    The ClippingBox  parameter allows vertices outside the box to be discarded, while isStatic disables continuous updates. 

Adding a new source
===================
To add a new source:
- Add the ad-hoc driver/sdk for Unity as a dependency in a sibling folder (making it a submodule is a good idea)
```bash
Assets
├───mesh-reconstruction
├───zed-unity
├───kinectv2-unity
├───mynewsource-unity
```
- In the `mesh-reconstruction > script` folder, create a new script that inherits from PointCloudProvider.
- In every Update, fill `PointCloudProvider.vertexTexture` and `PointCloudProvider.colorTexture` textures:
    - vertexTexture: 4 float-format texture (e.g. ARGBFloat) containing vertex attributes:
        - 1st float: x position
        - 2nd float: y position
        - 3rd float: z position
        - 4th float: uv texture coordinates, quantized to fit in 1 float (see methods EncodeUV() and DecodeUV() in ProceduralRenderingHelpers.cginc)
    - colorTexture: RGB texture from the camera, correctly oriented (correctly = consistent when displayed in Unity inspector panel).