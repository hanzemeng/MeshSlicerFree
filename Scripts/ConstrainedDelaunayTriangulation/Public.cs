using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public partial class ConstrainedDelaunayTriangulation
{
    public ConstrainedDelaunayTriangulation()
    {
        m_vertices = new();
        m_constraints = new();
        m_triangles = new();
        m_neighbors = new();
        m_flippedTriangles = new();
        m_incidentTriangles = new();
        m_findToVisit = new();
        m_findVisited = new();
        m_intersectEdges = new();
        m_newEdges = new();
        m_inDomain = new();
    }
    public void Reset()
    {
        m_vertices.Clear();
        m_constraints.Clear();
        m_triangles.Clear();
        m_neighbors.Clear();
        m_flippedTriangles.Clear();
        m_incidentTriangles.Clear();
        m_findToVisit.Clear();
        m_findVisited.Clear();
        m_intersectEdges.Clear();
        m_newEdges.Clear();
        m_inDomain.Clear();
    }

    public const float INPUT_VERTICES_RANGE = 10000f;
    public void Triangulate(List<Vector2> vertices, List<int> edges, List<int> resTriangles)
    {
        Reset();
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
            resTriangles.Add(m_triangles[i]-3);
            resTriangles.Add(m_triangles[i+1]-3);
            resTriangles.Add(m_triangles[i+2]-3);
        }
    }
    public List<int> Triangulate(List<Vector2> vertices, List<int> edges)
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
