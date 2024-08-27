using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public class MeshSlicer
{
    private Slicer m_slicer;
    private ConstrainedDelaunayTriangulation m_CDT;

    private MeshVertexDataMapper m_tMVDM;
    private MeshVertexDataMapper m_bMVDM;

    private List<Vector3> m_targetVertices;
    private List<int> m_targetTrianglesTemp;
    private List<int> m_targetTriangles;
    private Dictionary<int,int> m_targetVerticesSubmeshIndexMap;
    private int m_targetSubmeshCount;
    private Matrix4x4 m_targetLocalToWorldMatrix;

    private List<int>[] m_topTriangles;
    private List<int>[] m_bottomTriangles;

    private List<Vector2> m_intersectionVerticesPlane;
    private List<Vector3> m_intersectionVertices;
    private List<int> m_intersectionTriangles;

    private List<Material> m_targetMaterials;

    public int MAX_SUBMESH_COUNT = 16; // must be strictly less than the number of submeshes in the target game object

    public MeshSlicer()
    {
        m_slicer = new();
        m_CDT = new();

        m_tMVDM = new();
        m_bMVDM = new();

        m_targetVertices = new();
        m_targetTrianglesTemp = new();
        m_targetTriangles = new();
        m_targetVerticesSubmeshIndexMap = new();

        m_topTriangles = Enumerable.Range(0,MAX_SUBMESH_COUNT).Select(i=>new List<int>()).ToArray();
        m_bottomTriangles = Enumerable.Range(0,MAX_SUBMESH_COUNT).Select(i=>new List<int>()).ToArray();

        m_intersectionVerticesPlane = new();
        m_intersectionVertices = new();
        m_intersectionTriangles = new();

        m_targetMaterials = new();
    }

    public (GameObject, GameObject) Slice(GameObject targetGameObject, Plane slicePlane, Material intersectionMaterial)
    {
        Transform targetTransform = targetGameObject.transform;
        Mesh targetMesh = targetGameObject.GetComponent<MeshFilter>().sharedMesh;
        (Mesh, Mesh) slicedMesh = Slice(slicePlane, targetMesh, targetTransform, null != intersectionMaterial);
        if(null == slicedMesh.Item1)
        {
            return (null, null);
        }

        return
        (
            CreateSlicedGameObject(slicedMesh.Item1, targetGameObject, intersectionMaterial),
            CreateSlicedGameObject(slicedMesh.Item2, targetGameObject, intersectionMaterial)
        );
    }
    public async Task<(GameObject, GameObject)> SliceAsync(GameObject targetGameObject, Plane slicePlane, Material intersectionMaterial)
    {
        Transform targetTransform = targetGameObject.transform;
        Mesh targetMesh = targetGameObject.GetComponent<MeshFilter>().sharedMesh;
        (Mesh, Mesh) slicedMesh =  await SliceAsync(slicePlane, targetMesh, targetTransform, null != intersectionMaterial);
        if(null == slicedMesh.Item1)
        {
            return (null, null);
        }

        return
        (
            CreateSlicedGameObject(slicedMesh.Item1, targetGameObject, intersectionMaterial),
            CreateSlicedGameObject(slicedMesh.Item2, targetGameObject, intersectionMaterial)
        );
    }

    public (Mesh, Mesh) Slice(Plane slicePlane, Mesh targetMesh, Transform targetTransform, bool createSubmeshForIntersection)
    {
        CopyTargetData(targetMesh, targetTransform);

        for(int i=0; i<m_targetVertices.Count; i++)
        {
            m_targetVertices[i] = m_targetLocalToWorldMatrix.MultiplyPoint(m_targetVertices[i]);
        }
        m_slicer.Slice(m_targetVertices, m_targetTriangles, slicePlane);
        if(0 == m_slicer.m_tTriangles.Count || 0 == m_slicer.m_bTriangles.Count)
        {
            return (null,null);
        }

        CalculateSlicedMeshData(m_tMVDM, m_topTriangles, m_slicer.m_tTriangles);
        CalculateSlicedMeshData(m_bMVDM, m_bottomTriangles, m_slicer.m_bTriangles);

        TriangulateIntersection();

        CalculateIntersectionMeshData(true, m_tMVDM, m_topTriangles);
        CalculateIntersectionMeshData(false, m_bMVDM, m_bottomTriangles);

        return
        (
            CreateSlicedMesh(m_tMVDM, m_topTriangles, createSubmeshForIntersection),
            CreateSlicedMesh(m_bMVDM, m_bottomTriangles, createSubmeshForIntersection)
        );
    }
    public async Task<(Mesh, Mesh)> SliceAsync(Plane slicePlane, Mesh targetMesh, Transform targetTransform, bool createSubmeshForIntersection)
    {
        CopyTargetData(targetMesh, targetTransform);

        await Task.Run(()=>
        {
            for(int i=0; i<m_targetVertices.Count; i++)
            {
                m_targetVertices[i] = m_targetLocalToWorldMatrix.MultiplyPoint(m_targetVertices[i]);
            }
            m_slicer.Slice(m_targetVertices, m_targetTriangles, slicePlane);
        });
        if(0 == m_slicer.m_tTriangles.Count || 0 == m_slicer.m_bTriangles.Count)
        {
            return (null,null);
        }

        await Task.Run(()=>
        {
            CalculateSlicedMeshData(m_tMVDM, m_topTriangles, m_slicer.m_tTriangles);
            CalculateSlicedMeshData(m_bMVDM, m_bottomTriangles, m_slicer.m_bTriangles);

            TriangulateIntersection();

            CalculateIntersectionMeshData(true, m_tMVDM, m_topTriangles);
            CalculateIntersectionMeshData(false, m_bMVDM, m_bottomTriangles);
        });

        return
        (
            CreateSlicedMesh(m_tMVDM, m_topTriangles, createSubmeshForIntersection),
            CreateSlicedMesh(m_bMVDM, m_bottomTriangles, createSubmeshForIntersection)
        );
    }

    private GameObject CreateSlicedGameObject(Mesh slicedMesh, GameObject targetGameObject, Material intersectionMaterial)
    {
        GameObject res = UnityEngine.Object.Instantiate(targetGameObject);
        res.GetComponent<MeshFilter>().mesh = slicedMesh;
        if(null != intersectionMaterial)
        {
            res.GetComponent<MeshRenderer>().GetSharedMaterials(m_targetMaterials);
            m_targetMaterials.Add(intersectionMaterial);
            res.GetComponent<MeshRenderer>().SetMaterials(m_targetMaterials);
        }
        return res;
    }

    private void CopyTargetData(Mesh targetMesh, Transform targetTransform)
    {
        m_targetSubmeshCount = targetMesh.subMeshCount;
        m_targetLocalToWorldMatrix = targetTransform.localToWorldMatrix;

        targetMesh.GetVertices(m_targetVertices);

        m_targetVerticesSubmeshIndexMap.Clear();
        m_targetTriangles.Clear();
        for(int i=0; i<m_targetSubmeshCount; i++)
        {
            targetMesh.GetTriangles(m_targetTrianglesTemp, i);
            m_targetTrianglesTemp.ForEach(x=>m_targetVerticesSubmeshIndexMap[x]=i);
            m_targetTriangles.AddRange(m_targetTrianglesTemp);
        }

        m_tMVDM.AssignSourceMesh(targetMesh);
        m_bMVDM.AssignSourceMesh(m_tMVDM);
    }

    private void CalculateSlicedMeshData(MeshVertexDataMapper MVDM, List<int>[] resTriangles, List<int> srcTriangles)
    {
        for(int i=0; i<m_targetSubmeshCount; i++)
        {
            resTriangles[i].Clear();
        }

        for(int i=0; i<srcTriangles.Count; i+=3)
        {
            int subMeshIndex = -1;
            for(int j=0; j<3; j++)
            {
                if(srcTriangles[i+j]<0)
                {
                    int p1 = m_slicer.m_iMappings[-srcTriangles[i+j]].Item1;
                    int p2 = m_slicer.m_iMappings[-srcTriangles[i+j]].Item2;
                    double t = m_slicer.m_iMappings[-srcTriangles[i+j]].Item3;
                    MVDM.InterpolateVertexData(p1,p2,t);
                }
                else
                {
                    subMeshIndex = m_targetVerticesSubmeshIndexMap[srcTriangles[i+j]];
                    MVDM.CopyVertexData(srcTriangles[i+j]);
                }
            }
            resTriangles[subMeshIndex].Add(i+0);
            resTriangles[subMeshIndex].Add(i+1);
            resTriangles[subMeshIndex].Add(i+2);
        }
    }

    private void TriangulateIntersection()
    {
        m_intersectionVerticesPlane.Resize(m_slicer.m_iVertices.Count, Vector2.zero);
        m_intersectionVertices.Resize(m_slicer.m_iVertices.Count, Vector3.zero);

        foreach(var kvp in m_slicer.m_iVertices)
        {
            m_intersectionVerticesPlane[kvp.Value.Item2] = kvp.Key;
            m_intersectionVertices[kvp.Value.Item2] = kvp.Value.Item1.Item1;
        }
        Matrix4x4 worldToLocalMatrix = m_targetLocalToWorldMatrix.inverse;
        for(int i=0; i<m_intersectionVertices.Count; i++)
        {
            m_intersectionVertices[i] = worldToLocalMatrix.MultiplyPoint(m_intersectionVertices[i]);
        }

        m_CDT.Triangulate(m_intersectionVerticesPlane, m_slicer.m_iEdges, m_intersectionTriangles);
    }

    private void CalculateIntersectionMeshData(bool isTop, MeshVertexDataMapper MVDM, List<int>[] resTriangles)
    {
        int n = MVDM.GetTargetVertexCount();
        MVDM.AddDefaultValues(m_intersectionVertices);
        resTriangles[m_targetSubmeshCount].Clear();
        if(isTop)
        {
            for(int i=0; i<m_intersectionTriangles.Count; i+=3)
            {
                resTriangles[m_targetSubmeshCount].Add(n+m_intersectionTriangles[i+0]);
                resTriangles[m_targetSubmeshCount].Add(n+m_intersectionTriangles[i+1]);
                resTriangles[m_targetSubmeshCount].Add(n+m_intersectionTriangles[i+2]);
            }
        }
        else
        {
            for(int i=0; i<m_intersectionTriangles.Count; i+=3)
            {
                resTriangles[m_targetSubmeshCount].Add(n+m_intersectionTriangles[i+2]);
                resTriangles[m_targetSubmeshCount].Add(n+m_intersectionTriangles[i+1]);
                resTriangles[m_targetSubmeshCount].Add(n+m_intersectionTriangles[i+0]);
            }
        }
    }
    private Mesh CreateSlicedMesh(MeshVertexDataMapper MVDM, List<int>[] slicedTriangles, bool createSubmeshForIntersection)
    {
        Mesh slicedMesh = MVDM.MakeMesh();
        if(createSubmeshForIntersection)
        {
            slicedMesh.subMeshCount = m_targetSubmeshCount+1;
            slicedMesh.SetTriangles(slicedTriangles[m_targetSubmeshCount], m_targetSubmeshCount);
        }
        else
        {
            slicedMesh.subMeshCount = m_targetSubmeshCount;
            slicedTriangles[m_targetSubmeshCount-1].AddRange(slicedTriangles[m_targetSubmeshCount]);
        }
        for(int i=0; i<m_targetSubmeshCount; i++)
        {
            slicedMesh.SetTriangles(slicedTriangles[i], i);
        }

        //slicedMesh.RecalculateBounds(); called by SetTriangles
        slicedMesh.RecalculateNormals();
        slicedMesh.RecalculateTangents();
        slicedMesh.Optimize();

        return slicedMesh;
    }
}

}
