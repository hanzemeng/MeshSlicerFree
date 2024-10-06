#define CHECK_VERTEX_ON_EDGE // considerably slows triangulation, uncomment if previously crashed or not terminating
using System.Collections.Generic;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    private void SegmentRecovery(List<int> edges)
    {
        for(int i=1; i<edges.Count; i+=2)
        {
            int e0;
            int e1;
            if(edges[i]<edges[i-1])
            {
                e0 = edges[i];
                e1 = edges[i-1];
            }
            else
            {
                e0 = edges[i-1];
                e1 = edges[i];
            }

            #if CHECK_VERTEX_ON_EDGE
            // add e0->e1 to constraints only if no vertecis lie on it
            for(int j=3; j <m_vertices.Count; j++)
            {
                if(e0 == j || e1 == j)
                {
                    continue;
                }
                if(0 == Orient2D(e0,j,e1))
                {
                    Point2D e0j = m_vertices[j]-m_vertices[e0];
                    Point2D je1 = m_vertices[e1]-m_vertices[j];
                    double d = Point2D.Dot(e0j,je1);
                    if(d > 0d && d<(m_vertices[e1]-m_vertices[e0]).SquaredMagnitude())
                    {
                        goto NEXT;
                    }
                }
            }
            #endif
            m_constraints.Add((e0,e1));
            #if CHECK_VERTEX_ON_EDGE
            NEXT:
            continue;
            #endif
        }

        foreach((int,int) constraint in m_constraints)
        {
            if(-1 != FindIncidentTriangles(constraint.Item1, constraint.Item2).Item1)
            {
                continue;
            }
            m_intersectEdges.Clear();
            m_newEdges.Clear();

            {
                bool found = false;
                int t = m_incidentTriangles[constraint.Item1];
                int p1 = -1;
                int p2 = -1;
                while(!found)
                {
                    OrientTriangle(t, constraint.Item1);
                    p1 = m_triangles[3*t+1];
                    p2 = m_triangles[3*t+2];
                    if(Intersect(p1,p2, constraint.Item1, constraint.Item2))
                    {
                        m_intersectEdges.Add((p1,p2));
                        found = true;
                    }
                    else
                    {
                        t = m_neighbors[3*t+0];
                        if(-1 == t)
                        {
                            break;
                        }
                    }
                }
                if(!found)
                {
                    t = m_incidentTriangles[constraint.Item1];
                    while(!found)
                    {
                        OrientTriangle(t, constraint.Item1);
                        p1 = m_triangles[3*t+1];
                        p2 = m_triangles[3*t+2];
                        if(Intersect(p1,p2, constraint.Item1, constraint.Item2))
                        {
                            m_intersectEdges.Add((p1,p2));
                            found = true;
                        }
                        else
                        {
                            t = m_neighbors[3*t+2];
                        }
                    }
                }

                t = m_neighbors[3*t+1];
                OrientTriangle(t,p1,p2);
                while(true)
                {
                    int p0 = m_triangles[3*t+0];
                    p1 = m_triangles[3*t+1];
                    p2 = m_triangles[3*t+2];
                    if(p0 == constraint.Item2)
                    {
                        break;
                    }
                    if(Intersect(p0,p1,constraint.Item1, constraint.Item2))
                    {
                        m_intersectEdges.Add((p0,p1));
                        t = m_neighbors[3*t+0];
                        OrientTriangle(t, p0, p1);
                    }
                    else if(Intersect(p0,p2,constraint.Item1, constraint.Item2))
                    {
                        m_intersectEdges.Add((p0,p2));
                        t = m_neighbors[3*t+2];
                        OrientTriangle(t, p0, p2);
                    }
                    else
                    {
                        throw new System.Exception();
                    }
                }
            }
            
            {
                while(0 != m_intersectEdges.Count)
                {
                    int p1 = m_intersectEdges[0].Item1;
                    int p2 = m_intersectEdges[0].Item2;
                    (int, int) ts = FindIncidentTriangles(p1,p2);
                    int t0 = ts.Item1;
                    int t1 = ts.Item2;

                    OrientTriangle(t0,p1,p2);
                    OrientTriangle(t1,p1,p2);
                    int p0 = m_triangles[3*t0+0];
                    p1 = m_triangles[3*t0+1];
                    p2 = m_triangles[3*t0+2];
                    int p3 = m_triangles[3*t1+0];

                    int o013 = Orient2D(p0,p1,p3);
                    int o023 = Orient2D(p0,p2,p3);
                    if(!((-1 == o013 && 1 == o023) || (1 == o013 && -1 == o023)))
                    {
                        m_intersectEdges.Add(m_intersectEdges[0]);
                        m_intersectEdges.RemoveAt(0);
                        continue;
                    }

                    m_intersectEdges.RemoveAt(0);
                    FlipDiagonal(t0,t1);

                    if(p0 == constraint.Item1 || p3 == constraint.Item2 || p3 == constraint.Item1 || p0 == constraint.Item2)
                    {
                        m_newEdges.Add((p0,p3));
                    }
                    else if(Intersect(p0,p3,constraint.Item1,constraint.Item2))
                    {
                        m_intersectEdges.Add((p0,p3));
                    }
                    else
                    {
                        m_newEdges.Add((p0, p3));
                    }
                }
            }

            {
                bool hasSwap;
                do
                {
                    hasSwap = false;
                    for(int i=0; i<m_newEdges.Count; i++)
                    {
                        int p1 = m_newEdges[i].Item1;
                        int p2 = m_newEdges[i].Item2;
                        (int, int) ts = FindIncidentTriangles(p1,p2);
                        int t0 = ts.Item1;
                        int t1 = ts.Item2;

                        if((p1 == constraint.Item1 && p2 == constraint.Item2) || (p2 == constraint.Item1 && p1 == constraint.Item2))
                        {
                            continue;
                        }

                        OrientTriangle(t0,p1,p2);
                        OrientTriangle(t1,p1,p2);
                        int p0 = m_triangles[3*t0+0];
                        p1 = m_triangles[3*t0+1];
                        p2 = m_triangles[3*t0+2];
                        int p3 = m_triangles[3*t1+0];

                        if(1 != InCircle(p0,p1,p2,p3))
                        {
                            continue;
                        }

                        hasSwap = true;
                        FlipDiagonal(t0,t1);
                        m_newEdges[i] = (p0, p3);
                    }
                }
                while(hasSwap);
            }
        }
    }
}

}
