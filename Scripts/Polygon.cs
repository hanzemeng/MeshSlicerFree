using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public static class Polygon
{
    private const float EPSILON = 0.0001f;

    public static bool InsidePolygon(List<Vector2> points, Vector2 point)
    {
        int n = points.Count;

        float x = point.x;
        float y = point.y;
        bool res = false;

        Vector2 p1 = points[0];
        Vector2 p2;
 
        for(int i=1; i<=n; i++)
        {
            p2 = points[i % n];
            if(y > Mathf.Min(p1.y, p2.y))
            {
                if(y <= Mathf.Max(p1.y, p2.y))
                {
                    if (x <= Mathf.Max(p1.x, p2.x))
                    {
                        float x_intersection = (y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y) + p1.x;
 
                        if (Mathf.Abs(p1.x-p2.x)<=EPSILON || x <= x_intersection)
                        {
                            res ^= true;
                        }
                    }
                }
            }
            p1 = p2;
        }
 
        return res;
    }

    public enum IsStraightReturn
    {
        NOT_STRAIGHT = 0,
        STRAIGHT,
        REVERSE_STRAIGHT
    }
    public static IsStraightReturn IsStraight(List<Vector3> points)
    {
        if(points.Count <=2)
        {
            return IsStraightReturn.STRAIGHT;
        }

        bool isReverse = false;
        Vector3 lastEdge = points[1]-points[0];
        lastEdge.Normalize();
        Vector3 edge;
        float direction;
        for(int i=2; i<points.Count; i++)
        {
            edge = points[i]-points[i-1];
            edge.Normalize();
            direction = Vector3.Dot(lastEdge,edge);
            //cross = Vector3.Cross(edge, lastEdge);
            //if((cross-Vector3.zero).sqrMagnitude > EPSILON)
            //{
            //    return IsStraightReturn.NOT_STRAIGHT;
            //}
            if(direction<-(1f-EPSILON))
            {
                isReverse = true;
            }
            else if(direction<1f-EPSILON)
            {
                return IsStraightReturn.NOT_STRAIGHT;
            }
            lastEdge = edge;
        }

        if(isReverse)
        {
            return IsStraightReturn.REVERSE_STRAIGHT;
        }
        return IsStraightReturn.STRAIGHT;
    }
}

public class ContourTree
{
    public const float EPSILON = 0.00001f;

    public List<Vector2> contour;
    public List<int> contourId;
    public List<ContourTree> children;

    public ContourTree()
    {
        contour = new List<Vector2>();
        contourId = new List<int>();
        children = new List<ContourTree>();

        //contour.Add(Vector2.negativeInfinity);
        //contour.Add(new Vector2(float.PositiveInfinity, float.NegativeInfinity));
        //contour.Add(Vector2.positiveInfinity);
        //contour.Add(new Vector2(float.NegativeInfinity, float.PositiveInfinity));
        contour.Add(new Vector2(-1/EPSILON, -1/EPSILON));
        contour.Add(new Vector2(1/EPSILON, -1/EPSILON));
        contour.Add(new Vector2(1/EPSILON, 1/EPSILON));
        contour.Add(new Vector2(-1/EPSILON, 1/EPSILON));
        for(int i=0; i<4; i++)
        {
            contourId.Add(i);
        }
    }
    public ContourTree(List<Vector2> contour, List<int> contourId)
    {
        this.contour = contour;
        this.contourId = contourId;
        children = new List<ContourTree>();
    }

    public void AddContour(List<Vector2> newContour, List<int> contourId)
    {
        if(!Polygon.InsidePolygon(contour, newContour[0]))
        {
            return;
        }

        List<ContourTree> enclosedContour = new List<ContourTree>();
        foreach(ContourTree child in children)
        {
            if(Polygon.InsidePolygon(child.contour, newContour[0]))
            {
                child.AddContour(newContour, contourId);
                return;
            }
            if(Polygon.InsidePolygon(newContour, child.contour[0]))
            {
                enclosedContour.Add(child);
            }
        }

        ContourTree newContourTree = new ContourTree(newContour, contourId);
        foreach(ContourTree contourTree in enclosedContour)
        {
            children.Remove(contourTree);
            newContourTree.children.Add(contourTree);
        }
        children.Add(newContourTree);
    }
}

}
