using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hanzzz.MeshSlicerFree
{

public class MeshVertexDataMapper
{
    private VertexAttributeDescriptor[] m_sourceVertexAttributeDescriptors;

    private List<Vector3> m_sourcePositions;
    private List<Color32> m_sourceColors;
    private List<Vector4>[] m_sourceUVs;
    private List<BoneWeight> m_sourceBoneWeights;
    private bool m_hasColor;
    private bool[] m_hasUV;
    private bool m_hasBoneWeight;

    private List<Vector3> m_targetPositions;
    private List<Color32> m_targetColors;
    private List<List<Vector4>> m_targetUVs;
    private List<BoneWeight> m_targetBoneWeights;

    private Dictionary<int,int> m_verticesMapping;
    
    public MeshVertexDataMapper()
    {
        m_sourcePositions = new List<Vector3>();
        m_sourceColors = new List<Color32>();
        m_sourceUVs = new List<Vector4>[8]{new(), new(), new(), new(), new(), new(), new(), new()};
        m_sourceBoneWeights = new List<BoneWeight>();
        m_hasColor = false;
        m_hasUV = new bool[8];
        m_hasBoneWeight = false;

        m_targetPositions = new List<Vector3>();
        m_targetColors = new List<Color32>();
        m_targetUVs = Enumerable.Range(0,8).Select(i=>new List<Vector4>()).ToList();
        m_targetBoneWeights = new List<BoneWeight>();

        m_verticesMapping = new Dictionary<int, int>();
    }

    public void AssignSourceMesh(Mesh sourceMesh)
    {
        m_sourceVertexAttributeDescriptors = sourceMesh.GetVertexAttributes();

        sourceMesh.GetVertices(m_sourcePositions);
        m_targetPositions.Clear();

        if(m_hasColor = sourceMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            sourceMesh.GetColors(m_sourceColors);
            m_targetColors.Clear();
        }

        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i] = sourceMesh.HasVertexAttribute(VertexAttribute.TexCoord0+i))
            {
                sourceMesh.GetUVs(i, m_sourceUVs[i]);
                m_targetUVs[i].Clear();
            }
        }

        if(m_hasBoneWeight = sourceMesh.HasVertexAttribute(VertexAttribute.BlendWeight))
        {
            sourceMesh.GetBoneWeights(m_sourceBoneWeights);
            m_targetBoneWeights.Clear();
        }

        m_verticesMapping.Clear();
    }
    public void AssignSourceMesh(MeshVertexDataMapper other)
    {
        m_sourceVertexAttributeDescriptors = other.m_sourceVertexAttributeDescriptors;

        m_sourcePositions = other.m_sourcePositions;
        m_targetPositions.Clear();

        if(m_hasColor = other.m_hasColor)
        {
            m_sourceColors = other.m_sourceColors;
            m_targetColors.Clear();
        }

        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i] = other.m_hasUV[i])
            {
                m_sourceUVs[i] = other.m_sourceUVs[i];
                m_targetUVs[i].Clear();
            }
        }

        if(m_hasBoneWeight = other.m_hasBoneWeight)
        {
            m_sourceBoneWeights = other.m_sourceBoneWeights;
            m_targetBoneWeights.Clear();
        }

        m_verticesMapping.Clear();
    }

    public int GetTargetVertexCount()
    {
        return m_targetPositions.Count;
    }

    public int CopyVertexData(int s)
    {
        int res;
        if(m_verticesMapping.TryGetValue(s, out res))
        {
            return res;
        }

        res = m_targetPositions.Count;
        m_verticesMapping[s] = res;

        m_targetPositions.Add(m_sourcePositions[s]);
        if(m_hasColor)
        {
            m_targetColors.Add(m_sourceColors[s]);
        }
        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i])
            {
                m_targetUVs[i].Add(m_sourceUVs[i][s]);
            }
        }
        if(m_hasBoneWeight)
        {
            m_targetBoneWeights.Add(m_sourceBoneWeights[s]);
        }
        return res;
    }

    public int InterpolateVertexData(int s0, int s1, double t)
    {
        return InterpolateVertexData(s0,s1,(float)t);
    }
    public int InterpolateVertexData(int s0, int s1, float t)
    {
        int res = m_targetPositions.Count;
        m_targetPositions.Add(Vector4.Lerp(m_sourcePositions[s0],m_sourcePositions[s1],t));
        if(m_hasColor)
        {
            m_targetColors.Add(Color32.Lerp(m_sourceColors[s0],m_sourceColors[s1],t));
        }
        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i])
            {
                m_targetUVs[i].Add(Vector4.Lerp(m_sourceUVs[i][s0],m_sourceUVs[i][s1],t));
            }
        }
        if(m_hasBoneWeight)
        {
            m_targetBoneWeights.Add(BoneWeightLerp.Lerp(m_sourceBoneWeights[s0], m_sourceBoneWeights[s1],t));
        }
        return res;
    }

    public void AddDefaultValues(IReadOnlyList<Vector3> positions)
    {
        for(int i=0; i<positions.Count; i++)
        {
            AddDefaultValue(positions[i]);
        }
    }
    public void AddDefaultValue(Vector3 position)
    {
        m_targetPositions.Add(position);
        if(m_hasColor)
        {
            m_targetColors.Add(Color.clear);
        }
        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i])
            {
                m_targetUVs[i].Add(Vector4.zero);
            }
        }
        if(m_hasBoneWeight)
        {
            m_targetBoneWeights.Add(new BoneWeight());
        }
    }
    public void AddDefaultValues(IReadOnlyList<(Vector3,BoneWeight)> datas)
    {
        for(int i=0; i<datas.Count; i++)
        {
            AddDefaultValue(datas[i]);
        }
    }
    public void AddDefaultValue((Vector3,BoneWeight) data)
    {
        m_targetPositions.Add(data.Item1);
        m_targetBoneWeights.Add(data.Item2);
        if(m_hasColor)
        {
            m_targetColors.Add(Color.clear);
        }
        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i])
            {
                m_targetUVs[i].Add(Vector4.zero);
            }
        }
    }

    public Mesh MakeMesh()
    {
        Mesh res = new Mesh();
        res.SetVertices(m_targetPositions);
        if(m_hasColor)
        {
            res.SetColors(m_targetColors);
        }
        for(int i=0; i<8; i++)
        {
            if(m_hasUV[i])
            {
                res.SetUVs(i,m_targetUVs[i]);
            }
        }
        if(m_hasBoneWeight)
        {
            res.boneWeights = m_targetBoneWeights.ToArray();
        }
        res.SetVertexBufferParams(m_targetPositions.Count, m_sourceVertexAttributeDescriptors);
        return res;
    }
}

}
