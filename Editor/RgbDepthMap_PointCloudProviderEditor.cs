using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RgbDepthMap_PointCloudProvider))]
public class RgbDepthMap_PointCloudProviderEditor : UnityEditor.Editor
{
	SerializedProperty computeShaderProperty;
	SerializedProperty laplaceProperty;
	SerializedProperty dilateProperty;

	private void OnEnable()
	{
		computeShaderProperty = serializedObject.FindProperty("edgeSmoothingShader");
		laplaceProperty = serializedObject.FindProperty("laplacianThreshold");
		dilateProperty = serializedObject.FindProperty("dilateRadius");
	}
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		RgbDepthMap_PointCloudProvider myScript = (RgbDepthMap_PointCloudProvider)target;

		if (myScript.processDepthEdge)
		{
			EditorGUILayout.PropertyField(computeShaderProperty);
			EditorGUILayout.PropertyField(laplaceProperty);
			EditorGUILayout.PropertyField(dilateProperty);

		}

		serializedObject.ApplyModifiedProperties();
	}
}
