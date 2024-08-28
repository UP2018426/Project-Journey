using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Path
{
    [SerializeField, HideInInspector] private List<Vector3> points;
    
    [SerializeField, HideInInspector] private bool isClosed;

    public Path(Vector3 center)
    {
        points = new List<Vector3>
        {
            center + Vector3.left,
            center + (Vector3.left + Vector3.up) * 0.5f,
            center + (Vector3.right + Vector3.down) * 0.5f,
            center + Vector3.right
        };
    }

    public Vector3 this[int i]
    {
        get
        {
            return points[i];
        }
    }

    public int NumPoints
    {
        get
        {
            return points.Count;
        }
    }

    public int NumSegments
    {
        get
        {
            return points.Count / 3;
        }
    }

    public void AddSegment(Vector3 anchorPos)
    {
        points.Add(points[points.Count-1] * 2 - points[points.Count - 2]);

        points.Add((points[points.Count - 1] + anchorPos) * 0.5f);
        
        points.Add(anchorPos);

    }

    public Vector3[] GetPointsInSegment(int i)
    {
        return new Vector3[] { points[i * 3], points[i * 3 + 1], points[i * 3 + 2], points[LoopIndex(i * 3 + 3)] };
    }

    public void MovePoint(int i, Vector3 pos)
    {
        Vector3 deltaMove = pos - points[i];
        points[i] = pos;

        if (i % 3 == 0)
        {
            if (i + 1 < points.Count || isClosed)
            {
                points[LoopIndex(i + 1)] += deltaMove;
            }
            if (i - 1 >= 0 || isClosed)
            {
                points[LoopIndex(i - 1)] += deltaMove;
            }
        }
        else
        {
            bool nextPointIsAnchor = (i + 1) % 3 == 0;
            int correspondingControlIndex = (nextPointIsAnchor) ? i + 2 : i - 2;
            int anchorIndex = (nextPointIsAnchor) ? i + 1 : i - 1;

            if (correspondingControlIndex >= 0 && correspondingControlIndex < points.Count || isClosed)
            {
                float distance = (points[LoopIndex(anchorIndex)] - points[LoopIndex(correspondingControlIndex)]).magnitude;
                Vector3 direction = (points[LoopIndex(anchorIndex)] - pos).normalized;
                points[LoopIndex(correspondingControlIndex)] = points[LoopIndex(anchorIndex)] + direction * distance;
            }
        }
    }

    public void ToggleClosed()
    {
        isClosed = !isClosed;

        if (isClosed)
        {
            points.Add(points[points.Count-1] * 2 - points[points.Count - 2]);
            points.Add(points[0] * 2 - points[1]);
        }
        else
        {
            points.RemoveRange(points.Count - 2, 2);
        }
    }

    int LoopIndex(int i)
    {
        return (i + points.Count) % points.Count;
    }
}
