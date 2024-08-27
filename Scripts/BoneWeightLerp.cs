using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public static class BoneWeightLerp
{
    private static SortedDictionary<int, float> m_lerpBoneWeight = new SortedDictionary<int, float>();
    private static List<(int, float)> m_sortBoneWeight = new List<(int, float)>();
    private static BoneWeightComparator m_boneWeightComparator = new BoneWeightComparator();
    private class BoneWeightComparator : Comparer<(int,float)>
    {
        public override int Compare((int,float) x, (int,float) y)
        {
            return y.Item2.CompareTo(x.Item2);
        }
    }

    public static BoneWeight Lerp(BoneWeight b0, BoneWeight b1, float t)
    {
        m_lerpBoneWeight.Clear();
        m_sortBoneWeight.Clear();

        m_lerpBoneWeight[b0.boneIndex0] = b0.weight0 * (1f-t);
        m_lerpBoneWeight[b0.boneIndex1] = b0.weight1 * (1f-t);
        m_lerpBoneWeight[b0.boneIndex2] = b0.weight2 * (1f-t);
        m_lerpBoneWeight[b0.boneIndex3] = b0.weight3 * (1f-t);
        void CheckB1(int index, float weight)
        {
            if(m_lerpBoneWeight.ContainsKey(index))
            {
                m_lerpBoneWeight[index] += weight * t;
            }
            else
            {
                m_lerpBoneWeight[index] = weight * t;
            }
        }
        CheckB1(b1.boneIndex0, b1.weight0);
        CheckB1(b1.boneIndex1, b1.weight1);
        CheckB1(b1.boneIndex2, b1.weight2);
        CheckB1(b1.boneIndex3, b1.weight3);

        foreach(var kvp in m_lerpBoneWeight)
        {
            m_sortBoneWeight.Add((kvp.Key,kvp.Value));
        }
        m_sortBoneWeight.Sort(m_boneWeightComparator);


        BoneWeight res = new BoneWeight();
        res.boneIndex0 = m_sortBoneWeight[0].Item1;
        res.weight0 = m_sortBoneWeight[0].Item2;
        if(m_sortBoneWeight.Count>1)
        {
            res.boneIndex1 = m_sortBoneWeight[1].Item1;
            res.weight1 = m_sortBoneWeight[1].Item2;
        }
        if(m_sortBoneWeight.Count>2)
        {
            res.boneIndex2 = m_sortBoneWeight[2].Item1;
            res.weight2 = m_sortBoneWeight[2].Item2;
        }
        if(m_sortBoneWeight.Count>3)
        {
            res.boneIndex3 = m_sortBoneWeight[3].Item1;
            res.weight3 = m_sortBoneWeight[3].Item2;
        }
        return res;
    }
}

}
