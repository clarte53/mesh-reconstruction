using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RgbDepthMap_PointCloudProvider))]
public class RgbDepthMap_PointCloudProviderEditor : UnityEditor.Editor
{

    public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		RgbDepthMap_PointCloudProvider myScript = (RgbDepthMap_PointCloudProvider)target;

		if(myScript.processDepthEdge)
		{
			myScript.edgeSmoothingShader = EditorGUILayout.ObjectField("Edge Filtering Shader", myScript.edgeSmoothingShader,
				typeof(ComputeShader), true) as ComputeShader;
			myScript.laplacianThreshold = EditorGUILayout.FloatField("Laplacian threshold", myScript.laplacianThreshold);
			myScript.dilateRadius = EditorGUILayout.IntField("Dilate radius", myScript.dilateRadius);
		}
	}
}
