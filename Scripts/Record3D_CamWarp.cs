#if USE_RECORD3D
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Record3D_CamWarp : CamWarp
{

    // Update is called once per frame
    void Update()
    {
        UpdateMeshPosition();
    }

    protected override ulong GetImageTimeStamp()
    {
        ulong unixTimestamp = (ulong)((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds * 1000000.0);
        //print("     ImageTS: " + unixTimestamp);
        return unixTimestamp;
    }
}
#endif