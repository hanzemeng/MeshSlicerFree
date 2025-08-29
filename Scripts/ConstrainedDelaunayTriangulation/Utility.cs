using System.Collections.Generic;
using UnityEngine;
using Hanzzz.MeshSlicerFree.RobustGeometry.Predicates;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    private double[] pp1=new double[2]{0d,0d};
    private double[] pp2=new double[2]{0d,0d};
    private double[] pp3=new double[2]{0d,0d};
    private double[] pp4=new double[2]{0d,0d};

    // positive if the points p1,p2,p3 in counterclockwise order
    // negative if they occur in clockwise order
    // zero if collinear. 
    public int Orient2D(int p1, int p2, int p3)
    {
        pp1[0] = m_vertices[p1].x;
        pp1[1] = m_vertices[p1].y;
        pp2[0] = m_vertices[p2].x;
        pp2[1] = m_vertices[p2].y;
        pp3[0] = m_vertices[p3].x;
        pp3[1] = m_vertices[p3].y;
        return DoubleToInt(GeometricPredicates.Orient2D(pp1,pp2,pp3));
    }
    // Orient2D(p1,p2,p3) must be positive
    // positive if p4 is inside the circle of p1,p2,p3
    // negative if p4 is outside the circle
    // zero if p4 is on the circle
    public int InCircle(int p1, int p2, int p3, int p4)
    {
        pp1[0] = m_vertices[p1].x;
        pp1[1] = m_vertices[p1].y;
        pp2[0] = m_vertices[p2].x;
        pp2[1] = m_vertices[p2].y;
        pp3[0] = m_vertices[p3].x;
        pp3[1] = m_vertices[p3].y;
        pp4[0] = m_vertices[p4].x;
        pp4[1] = m_vertices[p4].y;
        return DoubleToInt(GeometricPredicates.InCircle(pp1,pp2,pp3,pp4));
    }
    private int DoubleToInt(double val)
    {
        if(val < 0d)
        {
            return -1;
        }
        if(val > 0d)
        {
            return 1;
        }
        return 0;
    }
    // true if p0->p1 intersects p2->p3 in interior at a single point
    public bool Intersect(int p0, int p1, int p2, int p3)
    {
        return Orient2D(p0,p1,p2) != Orient2D(p0,p1,p3) && Orient2D(p2,p3,p0) != Orient2D(p2,p3,p1);
    }


    private List<Point2D> m_vertices;
    private HashSet<(int,int)> m_constraints;

    private Point2D m_verticesCenter;
    private int m_p0,m_p1,m_p2;
    private List<int> m_verticesProcessOrder;
    private Comparer<int> m_verticesProcessOrderComparer;
    private Tree<int> m_convexHull;
    private Stack<int> m_delunarlizeStack;

    private List<int> m_triangles;
    private Stack<int> m_flippedTriangles;

    private List<int> m_verticesIncidentTriangles;
    private Dictionary<(int,int),(int,int)> m_edgesIncidentTriangles;
    
    private List<(int,int)> m_intersectEdges;
    private List<(int,int)> m_newEdges;

    private Queue<int> m_findToVisit;
    private HashSet<int> m_findVisited;
    private List<int> m_inDomain;


    // c and n are eachother's 1st (not 0th) neighbor
    private void FlipDiagonal(int c, int n)
    {
        int p0 = m_triangles[3*c+0];
        int p1 = m_triangles[3*c+1];
        int p2 = m_triangles[3*c+2];
        int p3 = m_triangles[3*n+0];

        //m_triangles[3*c+0] = p0;
        //m_triangles[3*c+1] = p1;
        m_triangles[3*c+2] = p3;
        m_triangles[3*n+0] = p0;
        m_triangles[3*n+1] = p3;
        m_triangles[3*n+2] = p2;

        //m_verticesIncidentTriangles[p0] = c;
        m_verticesIncidentTriangles[p1] = c;
        m_verticesIncidentTriangles[p2] = n;
        //m_verticesIncidentTriangles[p3] = c;

        RemoveEdgeIncident(p1,p2,c);
        RemoveEdgeIncident(p1,p2,n);
        AddEdgeIncident(p0,p3,c);
        AddEdgeIncident(p0,p3,n);

        //RemoveEdgeIncident(p0,p1,c);
        RemoveEdgeIncident(p1,p3,n);
        //RemoveEdgeIncident(p3,p2,n);
        RemoveEdgeIncident(p2,p0,c);
        //AddEdgeIncident(p0,p1,c);
        AddEdgeIncident(p1,p3,c);
        //AddEdgeIncident(p3,p2,n);
        AddEdgeIncident(p2,p0,n);
    }

    private int GetNeighbor(int e0, int e1, int t)
    {
        SortTwoInts(ref e0, ref e1);
        (int,int) old = m_edgesIncidentTriangles[(e0,e1)];
        if(old.Item1 == t)
        {
            return old.Item2;
        }
        return old.Item1;
    }
    private (int,int) GetEdgeIncident(int e0, int e1)
    {
        SortTwoInts(ref e0, ref e1);
        if(!m_edgesIncidentTriangles.ContainsKey((e0,e1)))
        {
            return (-1,-1);
        }
        return m_edgesIncidentTriangles[(e0,e1)];
    }
    private void AddEdgeIncident(int e0, int e1, int t)
    {
        SortTwoInts(ref e0, ref e1);
        if(!m_edgesIncidentTriangles.ContainsKey((e0,e1)))
        {
            m_edgesIncidentTriangles[(e0,e1)] = (t,-1);
        }
        else
        {
            (int,int) old = m_edgesIncidentTriangles[(e0,e1)];
            m_edgesIncidentTriangles[(e0,e1)] = (old.Item1, t);
        }
    }
    private void RemoveEdgeIncident(int e0, int e1, int t)
    {
        SortTwoInts(ref e0, ref e1);
        (int,int) old = m_edgesIncidentTriangles[(e0,e1)];
        (int,int) res;
        if(t == old.Item1)
        {
            res = (old.Item2, -1);
        }
        else
        {
            res = (old.Item1, -1);
        }
        if(-1 == res.Item1 && -1 == res.Item2)
        {
            m_edgesIncidentTriangles.Remove((e0,e1));
        }
        else
        {
            m_edgesIncidentTriangles[(e0,e1)] = res;
        }
    }
    private void SortTwoInts(ref int a, ref int b)
    {
        if(a>b)
        {
            int temp = a;
            a = b;
            b = temp;
        }
    }

    //private int AddVertex(Point2D p)
    //{
    //    m_vertices.Add(p);
    //    m_verticesIncidentTriangles.Add(-1);
    //    return m_vertices.Count-1;
    //}

    private int AddTriangle(int p1, int p2, int p3)
    {
        m_triangles.Add(p1);
        m_triangles.Add(p2);
        m_triangles.Add(p3);
        return m_triangles.Count/3-1;
    }

    // put p at t's first position
    private void OrientTriangle(int t, int p)
    {
        for(int i=0; i<3; i++)
        {
            if(m_triangles[3*t+i] == p)
            {
                SwapTrianglePivot(t, i);
                return;
            }
        }    
        throw new System.Exception();
    }

    // put the point other than p0 and p1 at t's first position
    private void OrientTriangle(int t, int p0, int p1)
    {
        for(int i=0; i<3; i++)
        {
            if(m_triangles[3*t+i] != p0 && m_triangles[3*t+i] != p1)
            {
                SwapTrianglePivot(t, i);
                return;
            }
        }    
        throw new System.Exception();
    }

    // put the i's element to 0th
    private void SwapTrianglePivot(int t, int i)
    {
        if(0 == i)
        {
            return;
        }
        else if(1 == i)
        {
            int temp = m_triangles[3*t+0];
            m_triangles[3*t+0] = m_triangles[3*t+1];
            m_triangles[3*t+1] = m_triangles[3*t+2];
            m_triangles[3*t+2] = temp;
        }
        else if(2 == i)
        {
            int temp = m_triangles[3*t+0];
            m_triangles[3*t+0] = m_triangles[3*t+2];
            m_triangles[3*t+2] = m_triangles[3*t+1];
            m_triangles[3*t+1] = temp;
        }
        else
        {
            throw new System.Exception();
        }
    }

    private bool ContainsVertex(int t, int p)
    {
        return p == m_triangles[3*t+0] || p == m_triangles[3*t+1] || p == m_triangles[3*t+2];
    }
}

}
