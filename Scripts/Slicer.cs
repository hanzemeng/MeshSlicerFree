using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Hanzzz.MeshSlicerFree.RobustGeometry.Predicates;

namespace Hanzzz.MeshSlicerFree
{

public class Slicer
{
    public Slicer()
    {
        m_vertices = new();
        //m_triangles = new(); do not uncomment
        m_tTriangles = new();
        m_bTriangles = new();
        m_iVertices = new SortedDictionary<Point2D, ((Vector3,int,int,float), int)>();
        m_iEdges = new();
        m_iMappings = new();
        m_iMappings.Add((-1,-1,-1)); // dummy
    }
    public void Reset()
    {
        m_vertices.Clear();
        //m_triangles.Clear(); do not uncomment
        m_tTriangles.Clear();
        m_bTriangles.Clear();
        m_iVertices.Clear();
        m_iEdges.Clear();
        m_iMappings.Clear();
        m_iMappings.Add((-1,-1,-1)); // dummy
    }

    public void Slice(IReadOnlyList<Vector3> vertices, List<int> triangles, Plane p)
    {
        Vector3 xAxis;
        if(0f != p.normal.x)
        {
            xAxis = new Vector3(-p.normal.y/p.normal.x, 1f, 0f);
        }
        else if(0f != p.normal.y)
        {
            xAxis = new Vector3(0f, -p.normal.z/p.normal.y, 1f);
        }
        else
        {
            xAxis = new Vector3(1f, 0f, -p.normal.x/p.normal.z);
        }
        Vector3 yAxis = Vector3.Cross(p.normal, xAxis);

        Slice(vertices, triangles, -p.distance*p.normal, -p.distance*p.normal+xAxis, -p.distance*p.normal+yAxis);
    }
    public void Slice(IReadOnlyList<Vector3> vertices, List<int> triangles, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Reset();
        for(int i=0; i<vertices.Count; i++)
        {
            m_vertices.Add(new Point3D(vertices[i]));
        }
        m_triangles = triangles;
        Point3D pp1 = new Point3D(p1);
        Point3D pp2 = new Point3D(p2);
        Point3D pp3 = new Point3D(p3);
        void CopyPointToDoubleArray(double[] arr, Point3D p)
        {
            arr[0] = p.x;
            arr[1] = p.y;
            arr[2] = p.z;
        }
        CopyPointToDoubleArray(m_pp1, pp1);
        CopyPointToDoubleArray(m_pp2, pp2);
        CopyPointToDoubleArray(m_pp3, pp3);

        m_pn = Point3D.Cross(pp3-pp2, pp2-pp1);
        m_pn.Normalize();
        m_px = m_pn.GetPerpendicular();
        m_px.Normalize();
        m_py = Point3D.Cross(m_pn, m_px);
        m_py.Normalize();

        for(int i=0; i<triangles.Count; i+=3)
        {
            int o0 = Orient3D(triangles[i+0]);
            int o1 = Orient3D(triangles[i+1]);
            int o2 = Orient3D(triangles[i+2]);
            
            //StringBuilder sb = new StringBuilder();
            //sb.Append(vertices[triangles[i+0]]);
            //sb.Append("\n");
            //sb.Append(vertices[triangles[i+1]]);
            //sb.Append("\n");
            //sb.Append(vertices[triangles[i+2]]);
            

            if(0 == o0 && 0 == o1 && 0 == o2)
            {
                // the triangle is part of the slice plane
                continue;
            }

            if((1 == o0 && 1 == o1 && 1 == o2) ||
               (0 == o0 && 1 == o1 && 1 == o2) ||
               (1 == o0 && 0 == o1 && 1 == o2) ||
               (1 == o0 && 1 == o1 && 0 == o2))
            {
                CopyTriangle(m_tTriangles, triangles, i);
                continue;
            }
            if((-1 == o0 && -1 == o1 && -1 == o2) ||
               (0 == o0 && -1 == o1 && -1 == o2) ||
               (-1 == o0 && 0 == o1 && -1 == o2) ||
               (-1 == o0 && -1 == o1 && 0 == o2))
            {
                CopyTriangle(m_bTriangles, triangles, i);
                continue;
            }

            // both on vertex
            if(0 == o0 && 0 == o1)
            {
                TwoVertexHelper(1 == o2, i,0,1);
            }
            else if(0 == o0 && 0 == o2)
            {
                TwoVertexHelper(1 == o1, i,2,0);
            }
            else if(0 == o1 && 0 == o2)
            {
                TwoVertexHelper(1 == o0, i,1,2);
            }
            // one on vertex, the other on edge
            else if(0 == o0)
            {
                OneVertexOneEdgeHelper(1 == o1, i, 0, 1, 2);
            }
            else if(0 == o1)
            {
                OneVertexOneEdgeHelper(1 == o2, i, 1, 2, 0);
            }
            else if(0 == o2)
            {
                OneVertexOneEdgeHelper(1 == o0, i, 2, 0, 1);
            }
            // both on edge
            else if(o0 != o1 && o0 != o2)
            {
                TwoEdgeHelper(1 == o0, i, 0, 1, 2);
            }
            else if(o1 != o0 && o1 != o2)
            {
                TwoEdgeHelper(1 == o1, i, 1, 2, 0);
            }
            else if(o2 != o0 && o2 != o1)
            {
                TwoEdgeHelper(1 == o2, i, 2, 0, 1);
            }
        }
    }

    // p1i, p2i are the offset of the two vertices on the slice plane
    private void TwoVertexHelper(bool isTop, int i, int p1i, int p2i)
    {
        if(isTop)
        {
            CopyTriangle(m_tTriangles, m_triangles, i);
            m_iEdges.Add(AddIntersectionVertex(m_vertices[m_triangles[i+p2i]],m_triangles[i+p2i],0,0d));
            m_iEdges.Add(AddIntersectionVertex(m_vertices[m_triangles[i+p1i]],m_triangles[i+p1i],0,0d));
        }
        else
        {
            CopyTriangle(m_bTriangles, m_triangles, i);
            m_iEdges.Add(AddIntersectionVertex(m_vertices[m_triangles[i+p1i]],m_triangles[i+p1i],0,0d));
            m_iEdges.Add(AddIntersectionVertex(m_vertices[m_triangles[i+p2i]],m_triangles[i+p2i],0,0d));
        }
    }
    // p0i is the offset of the vertex on the slice plane. p1i and p2i are in order of the triangle
    // isTop is true if p1i is above the slice plane
    private void OneVertexOneEdgeHelper(bool isTop, int i, int p0i, int p1i, int p2i)
    {
        (Point3D, double) inter = CalculateIntersection(m_triangles[i+p1i],m_triangles[i+p2i]);
        int pi = m_iMappings.Count;
        m_iMappings.Add((m_triangles[i+p1i],m_triangles[i+p2i],inter.Item2));

        if(isTop)
        {
            m_tTriangles.Add(m_triangles[i+p0i]);
            m_tTriangles.Add(m_triangles[i+p1i]);
            m_tTriangles.Add(-pi);
            m_bTriangles.Add(m_triangles[i+p0i]);
            m_bTriangles.Add(-pi);
            m_bTriangles.Add(m_triangles[i+p2i]);
            m_iEdges.Add(AddIntersectionVertex(m_vertices[m_triangles[i+p0i]],m_triangles[i+p0i],0,0d));
            m_iEdges.Add(AddIntersectionVertex(inter.Item1,m_triangles[i+p1i],m_triangles[i+p2i],inter.Item2));
        }
        else
        {
            m_bTriangles.Add(m_triangles[i+p0i]);
            m_bTriangles.Add(m_triangles[i+p1i]);
            m_bTriangles.Add(-pi);
            m_tTriangles.Add(m_triangles[i+p0i]);
            m_tTriangles.Add(-pi);
            m_tTriangles.Add(m_triangles[i+p2i]);
            m_iEdges.Add(AddIntersectionVertex(inter.Item1,m_triangles[i+p1i],m_triangles[i+p2i],inter.Item2));
            m_iEdges.Add(AddIntersectionVertex(m_vertices[m_triangles[i+p0i]],m_triangles[i+p0i],0,0d));
        }
    }
    // p0i is the offset of the vertex on the different side of p1i and p2i. p1i and p2i are in order of the triangle
    // isTop is true if p0i is above the slice plane
    private void TwoEdgeHelper(bool isTop, int i, int p0i, int p1i, int p2i)
    {
        (Point3D, double) inter1 = CalculateIntersection(m_triangles[i+p0i],m_triangles[i+p1i]);
        (Point3D, double) inter2 = CalculateIntersection(m_triangles[i+p0i],m_triangles[i+p2i]);
        int pi1 = m_iMappings.Count;
        m_iMappings.Add((m_triangles[i+p0i],m_triangles[i+p1i],inter1.Item2));
        int pi2 = m_iMappings.Count;
        m_iMappings.Add((m_triangles[i+p0i],m_triangles[i+p2i],inter2.Item2));

        if(isTop)
        {
            m_tTriangles.Add(m_triangles[i+p0i]);
            m_tTriangles.Add(-pi1);
            m_tTriangles.Add(-pi2);
            m_bTriangles.Add(-pi1);
            m_bTriangles.Add(m_triangles[i+p1i]);
            m_bTriangles.Add(m_triangles[i+p2i]);
            m_bTriangles.Add(-pi1);
            m_bTriangles.Add(m_triangles[i+p2i]);
            m_bTriangles.Add(-pi2);
            m_iEdges.Add(AddIntersectionVertex(inter2.Item1,m_triangles[i+p0i],m_triangles[i+p2i],inter2.Item2));
            m_iEdges.Add(AddIntersectionVertex(inter1.Item1,m_triangles[i+p0i],m_triangles[i+p1i],inter1.Item2));
        }
        else
        {
            m_bTriangles.Add(m_triangles[i+p0i]);
            m_bTriangles.Add(-pi1);
            m_bTriangles.Add(-pi2);
            m_tTriangles.Add(-pi1);
            m_tTriangles.Add(m_triangles[i+p1i]);
            m_tTriangles.Add(m_triangles[i+p2i]);
            m_tTriangles.Add(-pi1);
            m_tTriangles.Add(m_triangles[i+p2i]);
            m_tTriangles.Add(-pi2);
            m_iEdges.Add(AddIntersectionVertex(inter1.Item1,m_triangles[i+p0i],m_triangles[i+p1i],inter1.Item2));
            m_iEdges.Add(AddIntersectionVertex(inter2.Item1,m_triangles[i+p0i],m_triangles[i+p2i],inter2.Item2));
        }
    }

    private List<Point3D> m_vertices;
    private List<int> m_triangles; // reference to the input triangles
    public List<int> m_tTriangles;
    public List<int> m_bTriangles;
    public List<(int,int,double)> m_iMappings; // for the top and bottom triangles
    public SortedDictionary<Point2D, ((Vector3,int,int,float), int)> m_iVertices; // for the intersection face
    public List<int> m_iEdges;

    private void CopyTriangle(List<int> desTriangles, List<int> srcTriangles, int startIndex)
    {
        desTriangles.Add(srcTriangles[startIndex+0]);
        desTriangles.Add(srcTriangles[startIndex+1]);
        desTriangles.Add(srcTriangles[startIndex+2]);
    }

    private int AddIntersectionVertex(Point3D vertex, int p0, int p1, double t)
    {
        Point2D planeVertex = new Point2D
        (
            FloatingPointConverter.KeepFloatDecimals(Point3D.Dot(m_px,vertex)),
            FloatingPointConverter.KeepFloatDecimals(Point3D.Dot(m_py,vertex))
        );
        if(!m_iVertices.ContainsKey(planeVertex))
        {
            m_iVertices[planeVertex] = ((vertex.ToVector3(),p0,p1,(float)t), m_iVertices.Count);
        }
        return m_iVertices[planeVertex].Item2;
    }

    private (Point3D, double) CalculateIntersection(int p0, int p1)
    {
        Point3D seg = m_vertices[p1]-m_vertices[p0];
        double dir = Point3D.Dot(seg,m_pn);

        Point3D diff = m_vertices[p0];
        diff.x = diff.x - m_pp1[0];
        diff.y = diff.y - m_pp1[1];
        diff.z = diff.z - m_pp1[2];

        double t = -1d * Point3D.Dot(m_pn,diff) / dir;
        Point3D res = m_vertices[p0]+(seg*t);
        return (res,t);
    }

    private Point3D m_pn;
    private Point3D m_px;
    private Point3D m_py;
    private double[] m_pp1=new double[3]{0d,0d,0d};
    private double[] m_pp2=new double[3]{0d,0d,0d};
    private double[] m_pp3=new double[3]{0d,0d,0d};
    private double[] m_vp=new double[3]{0d,0d,0d};

    // Use left hand rule.
    // Positive if d is below plane abc, negative if d is above, zero if on.
    private int Orient3D(int v)
    {
        m_vp[0] = m_vertices[v].x;
        m_vp[1] = m_vertices[v].y;
        m_vp[2] = m_vertices[v].z;

        return DoubleToInt(GeometricPredicates.Orient3D(m_pp1,m_pp2,m_pp3,m_vp));
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
}

}
