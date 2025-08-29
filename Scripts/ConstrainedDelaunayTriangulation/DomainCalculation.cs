#define USE_WINDING_NUMBER // use winding number to determine if a triangle is part of the input domain.
// Only use this if intersection contains intersecting edges or non-closed polygons.
using System;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    #if USE_WINDING_NUMBER
    private void DomainCalculation()
    {
        const double WIND_THRESHOLD = 0.5d; // The smaller the number, the more triangles will be included. Should be a value in [0, 1].
        m_inDomain.Resize(m_triangles.Count/3, 0);
        for(int i=0; i<m_triangles.Count; i+=3)
        {
            int t0 = m_triangles[i+0];
            int t1 = m_triangles[i+1];
            int t2 = m_triangles[i+2];
            Point2D center = (m_vertices[t0] + m_vertices[t1] + m_vertices[t2])/3d;

            double wind = 0d;
            foreach(var (e0,e1) in m_constraints)
            {
                Point2D d0 = m_vertices[e0] - center;
                Point2D d1 = m_vertices[e1] - center;
                double theta = Math.Atan2(Point2D.Cross(d0,d1), Point2D.Dot(d0,d1));
                wind += theta;
            }
            wind /= 2d*Math.PI;
            if(wind > WIND_THRESHOLD)
            {
                m_inDomain[i/3] = 1;
            }
            else
            {
                m_inDomain[i/3] = -1;
            }
        }
    }
    #else
    private void DomainCalculation()
    {
        {
            var it0 = m_convexHull.GetMinNode();
            var it1 = m_convexHull.GetNextNode(it0);
            int p0 = it0.value;
            int p1 = it1.value;
            int t0 = GetEdgeIncident(p0,p1).Item1;

            m_findToVisit.Clear();
            m_findVisited.Clear();
            m_findVisited.Add(-1);
            m_inDomain.Resize(m_triangles.Count/3, 0);
            m_findToVisit.Enqueue(t0);
            if(m_constraints.Contains((p0,p1)) || m_constraints.Contains((p1,p0)))
            {
                m_inDomain[t0] = 1;
            }
            else
            {
                m_inDomain[t0] = -1;
            }
        }

        while(0 != m_findToVisit.Count)
        {
            int t = m_findToVisit.Dequeue();
            if(m_findVisited.Contains(t))
            {
                continue;
            }
            m_findVisited.Add(t);

            for(int i=0; i<3; i++)
            {
                int n = GetNeighbor(m_triangles[3*t+i],m_triangles[3*t+(i+1)%3], t);
                if(-1 == n || 0 != m_inDomain[n])
                {
                    continue;
                }

                int p0 = m_triangles[3*t+i];
                int p1 = m_triangles[3*t+(i+1)%3];
                if(m_constraints.Contains((p0,p1)) || m_constraints.Contains((p1,p0)))
                {
                    m_inDomain[n] = -1*m_inDomain[t];
                }
                else
                {
                    m_inDomain[n] = m_inDomain[t];
                }
                m_findToVisit.Enqueue(n);
            }
        }
    }
    #endif
}

}
