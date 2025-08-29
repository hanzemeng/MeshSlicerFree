using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public class SkinnedMeshSlicer
{
    private Slicer m_slicer;
    private ConstrainedDelaunayTriangulation m_CDT;

    private MeshVertexDataMapper m_tMVDM;
    private MeshVertexDataMapper m_bMVDM;

    private List<Vector3> m_targetVertices;
    private List<int> m_listIntTemp;
    private List<int> m_targetTriangles;
    private Dictionary<int,int> m_targetVerticesSubmeshIndexMap;
    private int m_targetSubmeshCount;
    private List<BoneWeight> m_targetBoneWeights;
    private List<Matrix4x4>  m_targetLocalToWorldMatrixes;
    private List<Matrix4x4>  m_targetBindPoses;

    private List<int>[] m_topTriangles;
    private List<int>[] m_bottomTriangles;

    private List<Point2D> m_intersectionVerticesPlane;
    private List<(Vector3, BoneWeight)> m_intersectionDatas;
    private List<int> m_intersectionTriangles;

    private List<Material> m_targetMaterials;

    public const int MAX_SUBMESH_COUNT = 16; // must be strictly less than the number of submeshes in the target game object

    public SkinnedMeshSlicer()
    {
        m_slicer = new();
        m_CDT = new();

        m_tMVDM = new();
        m_bMVDM = new();

        m_targetVertices = new();
        m_listIntTemp = new();
        m_targetTriangles = new();
        m_targetVerticesSubmeshIndexMap = new();
        m_targetBoneWeights = new();
        m_targetLocalToWorldMatrixes = new();
        m_targetBindPoses = new();

        m_topTriangles = Enumerable.Range(0,MAX_SUBMESH_COUNT).Select(i=>new List<int>()).ToArray();
        m_bottomTriangles = Enumerable.Range(0,MAX_SUBMESH_COUNT).Select(i=>new List<int>()).ToArray();

        m_intersectionVerticesPlane = new();
        m_intersectionDatas = new();
        m_intersectionTriangles = new();

        m_targetMaterials = new();
    }

    public (GameObject, GameObject) Slice(GameObject targetGameObject, int skinnedMeshRendererIndex, int rootIndex, (Vector3,Vector3,Vector3) slicePlane, Material intersectionMaterial)
    {
        Transform skinnedMeshRendererTransform = targetGameObject.transform.GetChild(skinnedMeshRendererIndex);
        SkinnedMeshRenderer targetSkinnedMeshRenderer = skinnedMeshRendererTransform.GetComponent<SkinnedMeshRenderer>();

        (Mesh, Mesh) slicedMesh = Slice(slicePlane, targetSkinnedMeshRenderer.sharedMesh, targetSkinnedMeshRenderer.bones, null != intersectionMaterial);
        if(null == slicedMesh.Item1)
        {
            return (null, null);
        }

        return
        (
            CreateSlicedGameObject(slicedMesh.Item1, targetGameObject, targetSkinnedMeshRenderer, skinnedMeshRendererIndex, rootIndex, intersectionMaterial),
            CreateSlicedGameObject(slicedMesh.Item2, targetGameObject, targetSkinnedMeshRenderer, skinnedMeshRendererIndex, rootIndex, intersectionMaterial)
        );
    }
    public async Task<(GameObject, GameObject)> SliceAsync(GameObject targetGameObject, int skinnedMeshRendererIndex, int rootIndex, (Vector3,Vector3,Vector3) slicePlane, Material intersectionMaterial)
    {
        Transform skinnedMeshRendererTransform = targetGameObject.transform.GetChild(skinnedMeshRendererIndex);
        SkinnedMeshRenderer targetSkinnedMeshRenderer = skinnedMeshRendererTransform.GetComponent<SkinnedMeshRenderer>();

        (Mesh, Mesh) slicedMesh = await SliceAsync(slicePlane, targetSkinnedMeshRenderer.sharedMesh, targetSkinnedMeshRenderer.bones, null != intersectionMaterial);
        if(null == slicedMesh.Item1)
        {
            return (null, null);
        }

        return
        (
            CreateSlicedGameObject(slicedMesh.Item1, targetGameObject, targetSkinnedMeshRenderer, skinnedMeshRendererIndex, rootIndex, intersectionMaterial),
            CreateSlicedGameObject(slicedMesh.Item2, targetGameObject, targetSkinnedMeshRenderer, skinnedMeshRendererIndex, rootIndex, intersectionMaterial)
        );
    }

    public (Mesh, Mesh) Slice((Vector3,Vector3,Vector3) slicePlane, Mesh targetMesh, Transform[] targetBones, bool createSubmeshForIntersection)
    {
        CopyTargetData(targetMesh, targetBones);
        for(int i=0; i<m_targetVertices.Count; i++)
        {
            m_targetVertices[i] = GetLocalToWorldMatrix(m_targetBoneWeights[i]).MultiplyPoint(m_targetVertices[i]);
        }

        m_slicer.Slice(m_targetVertices, m_targetTriangles, slicePlane.Item1, slicePlane.Item2, slicePlane.Item3);
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
    public async Task<(Mesh, Mesh)> SliceAsync((Vector3,Vector3,Vector3) slicePlane, Mesh targetMesh, Transform[] targetBones, bool createSubmeshForIntersection)
    {
        CopyTargetData(targetMesh, targetBones);

        await Task.Run(()=>
        {
            for(int i=0; i<m_targetVertices.Count; i++)
            {
                m_targetVertices[i] = GetLocalToWorldMatrix(m_targetBoneWeights[i]).MultiplyPoint(m_targetVertices[i]);
            }
            m_slicer.Slice(m_targetVertices, m_targetTriangles, slicePlane.Item1, slicePlane.Item2, slicePlane.Item3);
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

    

    private GameObject CreateSlicedGameObject(Mesh slicedMesh, GameObject targetGameObject, SkinnedMeshRenderer targetSkinnedMeshRenderer, int skinnedMeshRendererIndex, int rootIndex, Material intersectionMaterial)
    {
        GameObject res = UnityEngine.Object.Instantiate(targetGameObject);
        slicedMesh.bindposes = targetSkinnedMeshRenderer.sharedMesh.bindposes;
        Transform[] targetBones = targetSkinnedMeshRenderer.bones; // hope .bones is not newing a Transform[]
        Transform[] resBones = new Transform[targetBones.Length];
        void AssignBones(Transform targetCur, Transform resCur)
        {
            int i;
            if(-1 != (i=Array.IndexOf(targetBones, targetCur))) // hope targetBones has no duplicates
            {
                resBones[i] = resCur;
            }
            for(i=0; i<targetCur.childCount; i++)
            {
                AssignBones(targetCur.GetChild(i), resCur.GetChild(i));
            }
        }
        AssignBones(targetGameObject.transform.GetChild(rootIndex), res.transform.GetChild(rootIndex));

        SkinnedMeshRenderer resSkinnedMeshRenderer = res.transform.GetChild(skinnedMeshRendererIndex).GetComponent<SkinnedMeshRenderer>();
        resSkinnedMeshRenderer.sharedMesh = slicedMesh;
        resSkinnedMeshRenderer.bones = resBones; // hope .bones is not newing a Transform[]

        if(null != intersectionMaterial)
        {
            targetSkinnedMeshRenderer.GetSharedMaterials(m_targetMaterials);
            m_targetMaterials.Add(intersectionMaterial);
            resSkinnedMeshRenderer.SetMaterials(m_targetMaterials);
        }
        return res;
    }

    

    private Matrix4x4 GetLocalToWorldMatrix(BoneWeight boneWeight)
    {
        Matrix4x4 m0 = m_targetLocalToWorldMatrixes[boneWeight.boneIndex0] * m_targetBindPoses[boneWeight.boneIndex0];
        Matrix4x4 m1 = m_targetLocalToWorldMatrixes[boneWeight.boneIndex1] * m_targetBindPoses[boneWeight.boneIndex1];
        Matrix4x4 m2 = m_targetLocalToWorldMatrixes[boneWeight.boneIndex2] * m_targetBindPoses[boneWeight.boneIndex2];
        Matrix4x4 m3 = m_targetLocalToWorldMatrixes[boneWeight.boneIndex3] * m_targetBindPoses[boneWeight.boneIndex3];

        Matrix4x4 toWorldMatrix = new Matrix4x4();
        for(int i=0; i<16; i++)
        {
            toWorldMatrix[i] = m0[i] * boneWeight.weight0 + m1[i] * boneWeight.weight1 + m2[i] * boneWeight.weight2 + m3[i] * boneWeight.weight3;
        }
        return toWorldMatrix;
    }


    private void CopyTargetData(Mesh targetMesh, Transform[] targetBones)
    {
        m_targetSubmeshCount = targetMesh.subMeshCount;
        m_targetLocalToWorldMatrixes.Clear();
        targetMesh.GetBoneWeights(m_targetBoneWeights);
        for(int i=0; i<targetBones.Length; i++)
        {
            m_targetLocalToWorldMatrixes.Add(targetBones[i].localToWorldMatrix);
        }
        targetMesh.GetBindposes(m_targetBindPoses);
        targetMesh.GetVertices(m_targetVertices);

        m_targetVerticesSubmeshIndexMap.Clear();
        m_targetTriangles.Clear();
        for(int i=0; i<m_targetSubmeshCount; i++)
        {
            targetMesh.GetTriangles(m_listIntTemp, i);
            m_listIntTemp.ForEach(x=>m_targetVerticesSubmeshIndexMap[x]=i);
            m_targetTriangles.AddRange(m_listIntTemp);
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
            m_listIntTemp.Clear();
            for(int j=0; j<3; j++)
            {
                if(srcTriangles[i+j]<0)
                {
                    int p1 = m_slicer.m_iMappings[-srcTriangles[i+j]].Item1;
                    int p2 = m_slicer.m_iMappings[-srcTriangles[i+j]].Item2;
                    double t = m_slicer.m_iMappings[-srcTriangles[i+j]].Item3;
                    m_listIntTemp.Add(MVDM.InterpolateVertexData(p1,p2,t));
                }
                else
                {
                    subMeshIndex = m_targetVerticesSubmeshIndexMap[srcTriangles[i+j]];
                    m_listIntTemp.Add(MVDM.CopyVertexData(srcTriangles[i+j]));
                }
            }
            resTriangles[subMeshIndex].Add(m_listIntTemp[0]);
            resTriangles[subMeshIndex].Add(m_listIntTemp[1]);
            resTriangles[subMeshIndex].Add(m_listIntTemp[2]);
        }
    }

    private void TriangulateIntersection()
    {
        m_intersectionVerticesPlane.Resize(m_slicer.m_iVertices.Count, new Point2D());
        m_intersectionDatas.Resize(m_slicer.m_iVertices.Count, (Vector3.zero, new BoneWeight()));

        foreach(var kvp in m_slicer.m_iVertices)
        {
            m_intersectionVerticesPlane[kvp.Value.Item2] = kvp.Key;
            int p0 = kvp.Value.Item1.Item2;
            int p1 = kvp.Value.Item1.Item3;
            float t = kvp.Value.Item1.Item4;

            Vector3 position = Vector3.Lerp(m_targetVertices[p0],m_targetVertices[p1],t);
            BoneWeight boneWeight = BoneWeightLerp.Lerp(m_targetBoneWeights[p0],m_targetBoneWeights[p1],t);
            position = GetLocalToWorldMatrix(boneWeight).inverse.MultiplyPoint(position);
            m_intersectionDatas[kvp.Value.Item2] = (position,boneWeight);
        }

        m_CDT.Triangulate(m_intersectionVerticesPlane, m_slicer.m_iEdges, m_intersectionTriangles);
    }

    private void CalculateIntersectionMeshData(bool isTop, MeshVertexDataMapper MVDM, List<int>[] resTriangles)
    {
        int n = MVDM.GetTargetVertexCount();
        MVDM.AddDefaultValues(m_intersectionDatas);
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

        // slicedMesh.RecalculateBounds(); called by SetTriangles
        slicedMesh.RecalculateNormals();
        slicedMesh.RecalculateTangents();
        slicedMesh.Optimize();
        return slicedMesh;
    }
}

}
