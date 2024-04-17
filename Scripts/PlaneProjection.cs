using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public static class PlaneProjection
{
    public static IEnumerable<Vector2> Get2DProjection(IEnumerable<Vector3> points, Vector3 planeNormal)
    {
        (Vector3, Vector3, Vector3) plane = GetPlane(points, planeNormal);

        foreach(var point in Get2DProjection(points, planeNormal, plane.Item1, plane.Item2, plane.Item3))
        {
            yield return point;
        }
    }
    public static IEnumerable<Vector2> Get2DProjection(IEnumerable<Vector3> points, Vector3 planeNormal, Vector3 origin, Vector3 xAxis, Vector3 yAxis)
    {
        foreach(Vector3 point in points)
        {
            yield return new Vector2(Vector3.Dot(xAxis, point-origin), Vector3.Dot(yAxis, point-origin));
        }
    }

    public static (Vector3, Vector3, Vector3) GetPlane(IEnumerable<Vector3> points, Vector3 planeNormal)
    {
        Vector3 xAxis;
        if(0f != planeNormal.x)
        {
            xAxis = new Vector3(-planeNormal.y/planeNormal.x, 1f, 0f);
        }
        else if(0f != planeNormal.y)
        {
            xAxis = new Vector3(0f, -planeNormal.z/planeNormal.y, 1f);
        }
        else
        {
            xAxis = new Vector3(1f, 0f, -planeNormal.x/planeNormal.z);
        }
        xAxis.Normalize();
        Vector3 yAxis = Vector3.Cross(planeNormal, xAxis);
        yAxis.Normalize();
        Vector3 origin = points.Aggregate(Vector3.zero, (sum, next)=>sum+next) / points.Count();

        return (Vector3.zero, xAxis, yAxis);
    }
}

}
