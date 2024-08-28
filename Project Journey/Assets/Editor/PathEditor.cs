using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;

[CustomEditor(typeof(PathCreator))]
public class PathEditor : Editor
{
    private PathCreator creator;
    private Path path;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        if (GUILayout.Button("Create New"))
        {
            creator.CreatePath();
            path = creator.path;
            SceneView.RepaintAll();
        }
        
        if (GUILayout.Button("Toggle closed"))
        {
            path.ToggleClosed();
            SceneView.RepaintAll();
        }
    }

    void OnSceneGUI()
    {
        Input();
        Draw();
    }

    void Input()
    {
        Event guiEvent = Event.current;
        Vector3 mousePos = HandleUtility.GUIPointToWorldRay(guiEvent.mousePosition).origin;

        if (guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.shift)
        {
            Undo.RecordObject(creator, "Add segment");
            path.AddSegment(mousePos);
        }
    }
    
    void Draw()
    {
        for (int i = 0; i < path.NumSegments; i++)
        {
            Vector3[] points = path.GetPointsInSegment((i));
            Handles.color = Color.black;
            Handles.DrawLine(points[1], points[0]);
            Handles.DrawLine(points[2], points[3]);
            Handles.DrawBezier(points[0], points[3], points[1], points[2], Color.green, null, 2);
        }
        
        Handles.color = Color.red;
        
        for (int i = 0; i < path.NumPoints; i++)
        {
            Vector3 newPosition = Handles.FreeMoveHandle(path[i], 0.1f, Vector3.zero, Handles.CylinderHandleCap);
            if (path[i] != newPosition)
            {
                Undo.RecordObject(creator, "Move point");
                
                path.MovePoint(i, newPosition);
            }
        }
    }
    
    private void OnEnable()
    {
        creator = (PathCreator)target;
        if (creator.path == null)
        {
            creator.CreatePath();
        }

        path = creator.path;
    }
}
