using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    public ConstrainedDelaunayTriangulation()
    {
        //m_vertices = new();
        m_constraints = new();
        m_verticesCenter = Point2D.zero;
        m_p0 = m_p1 = m_p2 = -1;
        m_verticesProcessOrder = new ();
        m_verticesProcessOrderComparer = Comparer<int>.Create((a,b)=>
        {
            bool ab = m_p0==a || m_p1==a || m_p2==a;
            bool bb = m_p0==b || m_p1==b || m_p2==b;
            if(ab&&bb)
            {
                return a.CompareTo(b);
            }
            else if(ab)
            {
                return -1;
            }
            else if(bb)
            {
                return 1;
            }
            Point2D ad = m_vertices[a] - m_verticesCenter;
            Point2D bd = m_vertices[b] - m_verticesCenter;
            return ad.SquaredMagnitude().CompareTo(bd.SquaredMagnitude());
        });

        m_triangles = new();
        m_convexHull = new(Comparer<int>.Create((a,b)=>
        {
            Point2D ad = m_vertices[a] - m_verticesCenter;
            Point2D bd = m_vertices[b] - m_verticesCenter;
            return ad.Radian().CompareTo(bd.Radian());
        }));
        m_delunarlizeStack = new ();
        m_flippedTriangles = new();
        m_verticesIncidentTriangles = new();
        m_edgesIncidentTriangles = new();
        m_findToVisit = new();
        m_findVisited = new();
        m_intersectEdges = new();
        m_newEdges = new();
        m_inDomain = new();
    }
    public void Reset()
    {
        //m_vertices.Clear();
        m_constraints.Clear();
        m_verticesCenter = Point2D.zero;
        m_p0 = m_p1 = m_p2 = -1;
        //m_verticesProcessOrder.Clear();
        m_triangles.Clear();
        m_convexHull.Clear();
        m_delunarlizeStack.Clear();
        m_flippedTriangles.Clear();
        //m_verticesIncidentTriangles.Clear();
        m_edgesIncidentTriangles.Clear();
        m_findToVisit.Clear();
        m_findVisited.Clear();
        m_intersectEdges.Clear();
        m_newEdges.Clear();
        m_inDomain.Clear();
    }

    public void Triangulate(List<Point2D> vertices, List<int> edges, List<int> resTriangles)
    {
        Reset();
        //string s="";
        //foreach(Point2D v in vertices)
        //{
        //    s += v.ToString();
        //    s += "\n";
        //}
        //Debug.Log(s);
        DelaunayTriangulation(vertices);
        if(null != edges)
        {
            SegmentRecovery(edges);
        }
        DomainCalculation();

        resTriangles.Clear();
        for(int i=0; i<m_triangles.Count; i+=3)
        {
            if(-1 == m_inDomain[i/3])
            {
                continue;
            }
            resTriangles.Add(m_triangles[i]);
            resTriangles.Add(m_triangles[i+1]);
            resTriangles.Add(m_triangles[i+2]);
        }
    }
    public List<int> Triangulate(List<Point2D> vertices, List<int> edges)
    {
        List<int> resTriangles = new List<int>();
        Triangulate(vertices, edges, resTriangles);
        return resTriangles;
    }

    // does not consider the constraints when verifying
    public bool VerifyDelaunay()
    {
        for(int i=0; i<m_triangles.Count; i+=3)
        {
            if(-1 == m_triangles[i])
            {
                continue;
            }

            for(int j=0; j<m_vertices.Count; j++)
            {
                if(j==m_triangles[i] || j==m_triangles[i+1] || j==m_triangles[i+2])
                {
                    continue;
                }
                if(1 == InCircle(m_triangles[i],m_triangles[i+1],m_triangles[i+2],j))
                {
                    Debug.Log($"{m_triangles[i]}_{m_triangles[i+1]}_{m_triangles[i+2]}->{j}");
                    return false;
                }
            }
        }
        return true;
    }
}

}
