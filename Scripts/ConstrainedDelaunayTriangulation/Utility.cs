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
    private List<int> m_neighbors;
    private Stack<int> m_flippedTriangles;

    private List<int> m_incidentTriangles;
    private Queue<int> m_findToVisit;
    private HashSet<int> m_findVisited;

    private List<(int,int)> m_intersectEdges;
    private List<(int,int)> m_newEdges;

    private List<int> m_inDomain;


    // c and n are eachother's 1st (not 0th) neighbor
    private void FlipDiagonal(int c, int n)
    {
        int p0 = m_triangles[3*c+0];
        int p1 = m_triangles[3*c+1];
        int p2 = m_triangles[3*c+2];
        int p3 = m_triangles[3*n+0];
        int cn0 = m_neighbors[3*c+0];
        int cn2 = m_neighbors[3*c+2];
        int nn0 = m_neighbors[3*n+0];
        int nn2 = m_neighbors[3*n+2];

        m_triangles[3*c+0] = p0;
        m_triangles[3*c+1] = p1;
        m_triangles[3*c+2] = p3;
        m_triangles[3*n+0] = p0;
        m_triangles[3*n+1] = p3;
        m_triangles[3*n+2] = p2;

        m_incidentTriangles[p0] = c;
        m_incidentTriangles[p1] = c;
        m_incidentTriangles[p3] = c;
        m_incidentTriangles[p2] = n;

        m_neighbors[3*c+0] = cn0;
        if(-1 != cn0)
        {
            OrientTriangle(cn0, p0, p1);
            m_neighbors[3*cn0+1] = c;
        }
        m_neighbors[3*c+1] = nn2;
        if(-1 != nn2)
        {
            OrientTriangle(nn2, p3, p1);
            m_neighbors[3*nn2+1] = c;
        }
        m_neighbors[3*c+2] = n;

        m_neighbors[3*n+0] = c;
        m_neighbors[3*n+1] = nn0;
        if(-1 != nn0)
        {
            OrientTriangle(nn0, p2, p3);
            m_neighbors[3*nn0+1] = n;
        }
        m_neighbors[3*n+2] = cn2;
        if(-1 != cn2)
        {
            OrientTriangle(cn2, p2, p0);
            m_neighbors[3*cn2+1] = n;
        }
    }

    // set n as t's neighbor
    void AssignNeighbor(int t, int p0, int p1, int n)
    {
        if(-1 == t)
        {
            return;
        }
        int pos = 0;
        for(int i=0; i<3; i++)
        {
            if(p0 == m_triangles[3*t+i] || p1 == m_triangles[3*t+i])
            {
                pos+=i;
            }
        }
        if(1 == pos)
        {
            m_neighbors[3*t+0] = n;
        }
        else if(3 == pos)
        {
            m_neighbors[3*t+1] = n;
        }
        else if(2 == pos)
        {
            m_neighbors[3*t+2] = n;
        }
        else
        {
            throw new System.Exception();
        }
    }

    private int AddVertex(Point2D p)
    {
        m_vertices.Add(p);
        m_incidentTriangles.Add(-1);
        return m_vertices.Count-1;
    }

    private int AddTriangle(int p1, int p2, int p3)
    {
        m_triangles.Add(p1);
        m_triangles.Add(p2);
        m_triangles.Add(p3);
        m_neighbors.Add(-1);
        m_neighbors.Add(-1);
        m_neighbors.Add(-1);
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
            temp = m_neighbors[3*t+0];
            m_neighbors[3*t+0] = m_neighbors[3*t+1];
            m_neighbors[3*t+1] = m_neighbors[3*t+2];
            m_neighbors[3*t+2] = temp;
        }
        else if(2 == i)
        {
            int temp = m_triangles[3*t+0];
            m_triangles[3*t+0] = m_triangles[3*t+2];
            m_triangles[3*t+2] = m_triangles[3*t+1];
            m_triangles[3*t+1] = temp;
            temp = m_neighbors[3*t+0];
            m_neighbors[3*t+0] = m_neighbors[3*t+2];
            m_neighbors[3*t+2] = m_neighbors[3*t+1];
            m_neighbors[3*t+1] = temp;
        }
        else
        {
            throw new System.Exception();
        }
    }

    private (int,int) FindIncidentTriangles(int p0, int p1)
    {
        m_findToVisit.Clear();
        m_findVisited.Clear();
        m_findToVisit.Enqueue(m_incidentTriangles[p0]);
        m_findVisited.Add(-1);

        int res0 = -1;
        int res1 = -1;

        while(0 != m_findToVisit.Count)
        {
            int t = m_findToVisit.Dequeue();
            if(m_findVisited.Contains(t))
            {
                continue;
            }
            m_findVisited.Add(t);

            bool b0 = ContainsVertex(t,p0);
            bool b1 = ContainsVertex(t,p1);

            if(b0 && b1)
            {
                if(-1 == res0)
                {
                    res0 = t;
                }
                else
                {
                    res1 = t;
                    break;
                }
                m_findToVisit.Enqueue(m_neighbors[3*t+0]);
                m_findToVisit.Enqueue(m_neighbors[3*t+1]);
                m_findToVisit.Enqueue(m_neighbors[3*t+2]);
            }
            else if(b0 || b1)
            {
                m_findToVisit.Enqueue(m_neighbors[3*t+0]);
                m_findToVisit.Enqueue(m_neighbors[3*t+1]);
                m_findToVisit.Enqueue(m_neighbors[3*t+2]);
            }
        }

        return (res0,res1);
    }

    private bool ContainsVertex(int t, int p)
    {
        return p == m_triangles[3*t+0] || p == m_triangles[3*t+1] || p == m_triangles[3*t+2];
    }
    private bool ContainsGhostVertex(int t)
    {
        return m_triangles[3*t+0]<3 || m_triangles[3*t+1]<3 || m_triangles[3*t+2]<3;
    }
}

}
