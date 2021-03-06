﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Location))]
public class LocationEditor : GameEventEditor
{

	SerializedProperty encounterTable;
	SerializedProperty icon;
	SerializedProperty color;

	public override void OnEnable()
	{
		base.OnEnable();
		encounterTable = serializedObject.FindProperty(nameof(Location.EncounterTable));
		icon = serializedObject.FindProperty(nameof(Location.Icon));
		color = serializedObject.FindProperty(nameof(Location.Color));
	}

	public override void OnInspectorGUI()
	{

		base.OnInspectorGUI();

		Rect rect = new Rect(0, position.y, EditorGUIUtility.currentViewWidth -40, 16);

		EditorGUI.BeginChangeCheck();

		EditorGUI.PropertyField(rect, encounterTable);
		rect.y += 16;
		EditorGUI.PropertyField(rect, icon);
		rect.y += 16;
		EditorGUI.PropertyField(rect, color);

		if (EditorGUI.EndChangeCheck())
		{
			serializedObject.ApplyModifiedProperties();
		}
	}
}

[CustomEditor(typeof(Map))]
public class MapEditor : Editor
{
	public Vector2 scrollPosition = Vector2.zero;
	public float scale = 1;
	SerializedProperty locations;
	SerializedProperty width;
	SerializedProperty height;

	Vector2 initialButtonSize = new Vector2(48, 16);

	private void OnEnable()
	{
		locations = serializedObject.FindProperty("locations");
		width = serializedObject.FindProperty("width");
		height = serializedObject.FindProperty("height");
	}

	
	public override void OnInspectorGUI()
	{
		Rect position = new Rect(0, 0, EditorGUIUtility.currentViewWidth, 0);

		for(int i = 0; i < K.layoutSpaces; ++i) EditorGUILayout.Space();

		EditorGUI.BeginChangeCheck();

		EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, 16), width, new GUIContent("Width"));
		position.y += 18;
		EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, 16), height, new GUIContent("Height"));
		position.y += 18;
		scale = EditorGUI.Slider(new Rect(position.x, position.y, position.width, 16), new GUIContent("Editor scale"), scale, 1, 5);
		position.y += 18;

		Vector2 buttonSize = new Vector2(scale * initialButtonSize.x, initialButtonSize.y);


		Rect scrollView = new Rect(0, 0, (buttonSize.x+5)*width.intValue, (buttonSize.y + 5) * height.intValue);
		Rect scrollRect = new Rect(0, 0, EditorGUIUtility.currentViewWidth, 600);

		scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, scrollView);

		for (int i = 0; i < locations.arraySize; ++i)
		{
			int u = i % width.intValue;
			int v = i / width.intValue;
			
			EditorGUI.PropertyField(new Rect(position.x + u * (buttonSize.x + 5), position.y + v * (buttonSize.y + 5), buttonSize.x, buttonSize.y), locations.GetArrayElementAtIndex(i),GUIContent.none);
		}

		if (EditorGUI.EndChangeCheck())
		{
			locations.arraySize = width.intValue * height.intValue;
			serializedObject.ApplyModifiedProperties();
		}

		GUI.EndScrollView();

	}
}
