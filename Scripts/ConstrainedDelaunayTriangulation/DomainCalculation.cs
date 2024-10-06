using System.Collections.Generic;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    private void DomainCalculation()
    {
        {
            var it0 = m_convexHull.GetMinNode();
            var it1 = m_convexHull.GetNextNode(it0);
            int p0 = it0.value;
            int p1 = it1.value;
            int t0 = FindIncidentTriangles(p0,p1).Item1;

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
                int n = m_neighbors[3*t+i];
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
}

}
