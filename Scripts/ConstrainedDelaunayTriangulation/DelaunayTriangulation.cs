using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    private void DelaunayTriangulation(List<Vector2> vertices)
    {
        AddVertex(new Point2D(-INPUT_VERTICES_RANGE,-INPUT_VERTICES_RANGE));
        AddVertex(new Point2D(INPUT_VERTICES_RANGE,-INPUT_VERTICES_RANGE));
        AddVertex(new Point2D(0f,INPUT_VERTICES_RANGE));
        AddTriangle(0,1,2);
        m_incidentTriangles[0] = 0;
        m_incidentTriangles[1] = 0;
        m_incidentTriangles[2] = 0;

        for(int i=0; i<vertices.Count; i++)
        {
            int v = AddVertex(new Point2D(vertices[i]));
            InsertVertex(v);
            //if(!VerifyDelaunay())
            //{
            //    Debug.Log("NOT Delaunay");
            //    throw new System.Exception();
            //}
        }
    }

    private void InsertVertex(int v)
    {
        {
            (int,int,int,int) walk = FindTriangleContainVertex(v);
            int t0 = walk.Item1;

            bool onEdge = false;
            if(0 == walk.Item2)
            {
                onEdge = true;
            }
            else if(0 == walk.Item3)
            {
                OrientTriangle(t0,m_triangles[3*t0+1]);
                onEdge = true;
            }
            else if(0 == walk.Item4)
            {
                OrientTriangle(t0,m_triangles[3*t0+2]);
                onEdge = true;
            }

            if(onEdge)
            {
                int p0 = m_triangles[3*t0+0];
                int p1 = m_triangles[3*t0+1];
                int p2 = m_triangles[3*t0+2];
                int t1 = m_neighbors[3*t0+0];
                int t0n1 = m_neighbors[3*t0+1];
                int t0n2 = m_neighbors[3*t0+2];
                OrientTriangle(t1, p0);
                int p3 = m_triangles[3*t1+1];
                int t1n0 = m_neighbors[3*t1+0];
                int t1n1 = m_neighbors[3*t1+1];

                m_triangles[3*t0+0] = v;
                m_triangles[3*t0+1] = p2;
                m_triangles[3*t0+2] = p0;
                m_triangles[3*t1+0] = v;
                m_triangles[3*t1+1] = p0;
                m_triangles[3*t1+2] = p3;
                int t2 = AddTriangle(v,p3,p1);
                int t3 = AddTriangle(v,p1,p2);

                m_incidentTriangles[v] = t0;
                m_incidentTriangles[p0] = t0;
                m_incidentTriangles[p2] = t0;
                m_incidentTriangles[p1] = t2;
                m_incidentTriangles[p3] = t2;

                m_neighbors[3*t0+0] = t3;
                m_neighbors[3*t0+1] = t0n2;
                if(-1 != t0n2)
                {
                    OrientTriangle(t0n2, p2, p0);
                    m_neighbors[3*t0n2+1] = t0;
                }
                m_neighbors[3*t0+2] = t1;

                m_neighbors[3*t1+0] = t0;
                m_neighbors[3*t1+1] = t1n0;
                if(-1 != t1n0)
                {
                    OrientTriangle(t1n0, p0, p3);
                    m_neighbors[3*t1n0+1] = t1;
                }
                m_neighbors[3*t1+2] = t2;

                m_neighbors[3*t2+0] = t1;
                m_neighbors[3*t2+1] = t1n1;
                if(-1 != t1n1)
                {
                    OrientTriangle(t1n1, p3, p1);
                    m_neighbors[3*t1n1+1] = t2;
                }
                m_neighbors[3*t2+2] = t3;

                m_neighbors[3*t3+0] = t2;
                m_neighbors[3*t3+1] = t0n1;
                if(-1 != t0n1)
                {
                    OrientTriangle(t0n1, p1, p2);
                    m_neighbors[3*t0n1+1] = t3;
                }
                m_neighbors[3*t3+2] = t0;

                m_flippedTriangles.Push(t0);
                m_flippedTriangles.Push(t1);
                m_flippedTriangles.Push(t2);
                m_flippedTriangles.Push(t3);
            }
            else
            {
                int p0 = m_triangles[3*t0+0];
                int p1 = m_triangles[3*t0+1];
                int p2 = m_triangles[3*t0+2];
                int n0 = m_neighbors[3*t0+0];
                int n1 = m_neighbors[3*t0+1];
                int n2 = m_neighbors[3*t0+2];

                m_triangles[3*t0+0] = v;
                m_triangles[3*t0+1] = p0;
                m_triangles[3*t0+2] = p1;
                int t1 = AddTriangle(v,p1,p2);
                int t2 = AddTriangle(v,p2,p0);

                m_incidentTriangles[v] = t0;
                m_incidentTriangles[p0] = t0;
                m_incidentTriangles[p1] = t0;
                m_incidentTriangles[p2] = t1;

                m_neighbors[3*t0+0] = t2;
                m_neighbors[3*t0+1] = n0;
                if(-1 != n0)
                {
                    OrientTriangle(n0, p0, p1);
                    m_neighbors[3*n0+1] = t0;
                }
                m_neighbors[3*t0+2] = t1;

                m_neighbors[3*t1+0] = t0;
                m_neighbors[3*t1+1] = n1;
                if(-1 != n1)
                {
                    OrientTriangle(n1, p1, p2);
                    m_neighbors[3*n1+1] = t1;
                }
                m_neighbors[3*t1+2] = t2;

                m_neighbors[3*t2+0] = t1;
                m_neighbors[3*t2+1] = n2;
                if(-1 != n2)
                {
                    OrientTriangle(n2, p2, p0);
                    m_neighbors[3*n2+1] = t2;
                }
                m_neighbors[3*t2+2] = t0;

                m_flippedTriangles.Push(t0);
                m_flippedTriangles.Push(t1);
                m_flippedTriangles.Push(t2);
            }
        }

        while(0 != m_flippedTriangles.Count)
        {
            int c = m_flippedTriangles.Pop();
            OrientTriangle(c, v);
            int n = m_neighbors[3*c+1];
            if(-1 == n)
            {
                continue;
            }

            int p0 = m_triangles[3*c+0];
            int p1 = m_triangles[3*c+1];
            int p2 = m_triangles[3*c+2];

            OrientTriangle(n, p2, p1);
            int p3 = m_triangles[3*n+0];
            if(-1 == p3)
            {
                continue;
            }
            if(1 != InCircle(p0,p1,p2,p3))
            {
                continue;
            }

            FlipDiagonal(c,n);

            //s.Remove(c);
            //s.Remove(n);
            m_flippedTriangles.Push(c);
            m_flippedTriangles.Push(n);
        }
    }

    private (int,int,int,int) FindTriangleContainVertex(int p)
    {
        int res = -1;
        int orient01 = -2;
        int orient12 = -2;
        int orient20 = -2;
        for(int i=0; i<m_triangles.Count; i+=3)
        {
            if(-1 != m_triangles[i])
            {
                res = i/3;
                break;
            }
        }
        int prev = res;
        bool found = false;
        while(!found)
        {
            int t0 = m_triangles[3*res+0];
            int t1 = m_triangles[3*res+1];
            int t2 = m_triangles[3*res+2];
            int n0 = m_neighbors[3*res+0];
            int n1 = m_neighbors[3*res+1];
            int n2 = m_neighbors[3*res+2];

            if(-1 == (orient01=Orient2D(t0,t1,p)) && -1 != n0 && prev != n0)
            {
                prev = res;
                res = n0;
            }
            else
            {
                if(-1 == (orient12=Orient2D(t1,t2,p)) && -1 != n1 && prev != n1)
                {
                    prev = res;
                    res = n1;
                }
                else
                {
                    if(-1 == (orient20=Orient2D(t2,t0,p)) && -1 != n2 && prev != n2)
                    {
                        prev = res;
                        res = n2;
                    }
                    else
                    {
                        found = true;
                    }
                }
            }
        }
        if(-2==orient01 || -2==orient12 || -2==orient20)
        {
            throw new System.Exception();
        }
        return (res, orient01, orient12, orient20);
    }
}

}
