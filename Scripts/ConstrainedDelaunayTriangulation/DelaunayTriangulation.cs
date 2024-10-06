using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    private void DelaunayTriangulation(List<Point2D> vertices)
    {
        for(int i=0; i<vertices.Count; i++)
        {
            AddVertex(vertices[i]);
            m_verticesCenter += vertices[i];
        }

        m_verticesCenter /= (double)vertices.Count;

        double dis = (m_vertices[0] - m_verticesCenter).SquaredMagnitude();
        m_p0 = 0;
        for(int i=1; i<vertices.Count; i++)
        {
            double curDis = (m_vertices[i] - m_verticesCenter).SquaredMagnitude();
            if(curDis < dis)
            {
                dis = curDis;
                m_p0 = i;
            }
        }

        dis = double.MaxValue;
        m_p1 = -1;
        for(int i=0; i<vertices.Count; i++)
        {
            if(m_p0 == i)
            {
                continue;
            }
            double curDis = (m_vertices[i] - m_vertices[m_p0]).SquaredMagnitude();
            if(curDis < dis)
            {
                dis = curDis;
                m_p1 = i;
            }
        }

        dis = double.MaxValue;
        m_p2 = -1;
        double m_p0Mag = m_vertices[m_p0].SquaredMagnitude();
        double m_p1Mag = m_vertices[m_p1].SquaredMagnitude();
        for(int i=0; i<vertices.Count; i++)
        {
            if(m_p0 == i || m_p1 == i || 0 == Orient2D(m_p0,m_p1,i))
            {
                continue;
            }
            double iMag = m_vertices[i].SquaredMagnitude();
            double sx = m_p0Mag*(m_vertices[m_p1].y-m_vertices[i].y) - m_vertices[m_p0].y*(m_p1Mag-iMag) + m_p1Mag*m_vertices[i].y-m_vertices[m_p1].y*iMag;
            double sy = m_vertices[m_p0].x*(m_p1Mag-iMag) - m_p0Mag*(m_vertices[m_p1].x-m_vertices[i].x) + m_vertices[m_p1].x*iMag-m_p1Mag*m_vertices[i].x;
            sx/=2;
            sy/=2;
            double a = m_vertices[m_p0].x*(m_vertices[m_p1].y-m_vertices[i].y) - m_vertices[m_p0].y*(m_vertices[m_p1].x-m_vertices[i].x) + m_vertices[m_p1].x*m_vertices[i].y-m_vertices[m_p1].y*m_vertices[i].x;
            double b = m_vertices[m_p0].x*(m_vertices[m_p1].y*iMag-m_p1Mag*m_vertices[i].y) - m_vertices[m_p0].y*(m_vertices[m_p1].x*iMag-m_p1Mag*m_vertices[i].x) + m_p0Mag*(m_vertices[m_p1].x*m_vertices[i].y-m_vertices[m_p1].y*m_vertices[i].x);

            double cur_dis = b/a + (sx*sx+sy*sy)/(a*a);
            if(cur_dis < dis)
            {
                dis = cur_dis;
                //m_verticesCenter = new Point2D(sx/a,sy/a);
                m_p2 = i;
                m_verticesCenter = (m_vertices[m_p0] + m_vertices[m_p1] + m_vertices[m_p2])/3d;
            }
        }

        if(-1 == m_p0 || -1 == m_p1 || -1 == m_p2)
        {
            throw new System.Exception();
        }
        if(-1 == Orient2D(m_p0,m_p1,m_p2))
        {
            int temp = m_p1;
            m_p1 = m_p2;
            m_p2 = temp;
        }

        AddTriangle(m_p0, m_p1, m_p2);
        m_incidentTriangles[m_p0] = 0;
        m_incidentTriangles[m_p1] = 0;
        m_incidentTriangles[m_p2] = 0;
        m_convexHull.Insert(m_p0);
        m_convexHull.Insert(m_p1);
        m_convexHull.Insert(m_p2);

        m_verticesProcessOrder.Resize(vertices.Count);
        for(int i=0; i<m_verticesProcessOrder.Count; i++)
        {
            m_verticesProcessOrder[i] = i;
        }
        m_verticesProcessOrder.Sort(m_verticesProcessOrderComparer);


        Node<int> GetNextNode(Node<int> cur)
        {
            Node<int> res = m_convexHull.GetNextNode(cur);
            if(Node<int>.Null == res)
            {
                return m_convexHull.GetMinNode();
            }
            return res;
        }
        Node<int> GetPreviousNode (Node<int> cur)
        {
            Node<int> res = m_convexHull.GetPreviousNode(cur);
            if(Node<int>.Null == res)
            {
                return m_convexHull.GetMaxNode();
            }
            return res;
        }

        for(int i=3; i<m_verticesProcessOrder.Count; i++)
        {
            int p0 = m_verticesProcessOrder[i];
            //Debug.Log(p0);
            var it2 = m_convexHull.GetNodeLowerBound(p0);
            if(Node<int>.Null == it2)
            {
                it2 = m_convexHull.GetMinNode();
            }
            var it1 = GetPreviousNode(it2);
            int p1 = it1.value;
            int p2 = it2.value;
            int ori = Orient2D(p1,p0,p2);

            if(1 == ori)
            {
                int t = AddTriangle(p0,p2,p1);
                int n = FindIncidentTriangles(p1,p2).Item1;
                m_neighbors[3*t+1] = n;
                AssignNeighbor(n,p1,p2,t);
                m_incidentTriangles[p0] = t;
                Delunarlize(t, p0);

                var it3 = GetPreviousNode(it1);                
                int p3 = it3.value;
                while(1 == Orient2D(p3,p0,p1))
                {
                    t = AddTriangle(p0,p1,p3);
                    int n0 = FindIncidentTriangles(p0,p1).Item1;
                    int n1 = FindIncidentTriangles(p1,p3).Item1;
                    m_neighbors[3*t+0] = n0;
                    AssignNeighbor(n0,p0,p1,t);
                    m_neighbors[3*t+1] = n1;
                    AssignNeighbor(n1,p1,p3,t);
                    Delunarlize(t, p0);

                    m_convexHull.Delete(it1.value);
                    //m_convexHull.Delete(it1);
                    it1 = m_convexHull.GetNode(p3);
                    it3 = it1;
                    it3 = GetPreviousNode(it3);
                    
                    p3 = it3.value;
                    p1 = it1.value;
                }
           
                it3 = GetNextNode(m_convexHull.GetNode(p2)); // RBTree's delete ruins every iterator
                p3 = it3.value;
                while(1 == Orient2D(p2,p0,p3))
                {
                    t = AddTriangle(p0,p3,p2);
                    int n1 = FindIncidentTriangles(p3,p2).Item1;
                    int n2 = FindIncidentTriangles(p2,p0).Item1;
                    m_neighbors[3*t+1] = n1;
                    AssignNeighbor(n1,p3,p2,t);
                    m_neighbors[3*t+2] = n2;
                    AssignNeighbor(n2,p2,p0,t);
                    Delunarlize(t, p0);

                    m_convexHull.Delete(it2.value);
                    //m_convexHull.Delete(it2);
                    it2 = m_convexHull.GetNode(p3);
                    it3 = it2;
                    it3 = GetNextNode(it3);

                    p3 = it3.value;
                    p2 = it2.value;
                }
            }
            else if(0 == ori)
            {
                int t0 = FindIncidentTriangles(p1,p2).Item1;
                OrientTriangle(t0,p1,p2);
                int p3 = m_triangles[3*t0+0];
                int n0 = m_neighbors[3*t0+0];
                int n2 = m_neighbors[3*t0+2];
                m_triangles[3*t0+0] = p0;
                m_triangles[3*t0+1] = p3;
                m_triangles[3*t0+2] = p1;
                int t1 = AddTriangle(p0,p2,p3);

                m_neighbors[3*t0+0] = t1;
                m_neighbors[3*t0+1] = n0;
                AssignNeighbor(n0,p3,p1,t0);
                m_neighbors[3*t0+2] = -1;
                m_neighbors[3*t1+0] = -1;
                m_neighbors[3*t1+1] = n2;
                AssignNeighbor(n2,p2,p3,t1);
                m_neighbors[3*t1+2] = t0;

                m_incidentTriangles[p0] = t0;
                m_incidentTriangles[p3] = t0;
                m_incidentTriangles[p1] = t0;
                m_incidentTriangles[p2] = t1;

                Delunarlize(t0, p0);
                Delunarlize(t1, p0);
            }
            else
            {
                throw new System.Exception();
            }
            m_convexHull.Insert(p0);
        }
            
    }

    private void Delunarlize(int t, int p)
    {
        m_delunarlizeStack.Push(t);
        while(0 != m_delunarlizeStack.Count)
        {
            t = m_delunarlizeStack.Pop();

            OrientTriangle(t, p);
            int p1 = m_triangles[3*t+1];
            int p2 = m_triangles[3*t+2];
            int n = m_neighbors[3*t+1];
            if(-1 == n)
            {
                continue;
            }
            OrientTriangle(n, p1, p2);
            int p3 = m_triangles[3*n+0];
            if(1 != InCircle(p,p1,p2,p3))
            {
                continue;
            }
            FlipDiagonal(t,n);
            m_delunarlizeStack.Push(t);
            m_delunarlizeStack.Push(n);
        }
    }
}
    
}
