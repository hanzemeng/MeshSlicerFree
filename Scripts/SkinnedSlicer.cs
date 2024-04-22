//#define VERBOSE

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if VERBOSE
using System.Text;
#endif

namespace Hanzzz.MeshSlicerFree
{
    public class SkinnedSlicer
    {
        private const float EPSILON = 0.00001f;
        private class Vector3Comparator : IComparer<Vector3>
        {
            public int Compare(Vector3 a, Vector3 b)
            {
                if(a.x < b.x-EPSILON)
                {
                    return -1;
                }
                if(a.x > b.x+EPSILON)
                {
                    return 1;
                }
                if(a.y < b.y-EPSILON)
                {
                    return -1;
                }
                if(a.y > b.y+EPSILON)
                {
                    return 1;
                }
                if(a.z < b.z-EPSILON)
                {
                    return -1;
                }
                if(a.z > b.z+EPSILON)
                {
                    return 1;
                }
                return 0;
            }
        }

        private static List<VertexAttribute>  UV_ATTRIBTES = new List<VertexAttribute> {VertexAttribute.TexCoord0, VertexAttribute.TexCoord1, VertexAttribute.TexCoord2, VertexAttribute.TexCoord3, VertexAttribute.TexCoord4, VertexAttribute.TexCoord5, VertexAttribute.TexCoord6, VertexAttribute.TexCoord7 };

        private Plane slicePlane;

        private List<Transform> originalBones = new List<Transform>();
        private List<Matrix4x4> originalBonesToWorldMatrix = new List<Matrix4x4>();
        private List<Matrix4x4> originalBindPoses = new List<Matrix4x4>();
        private Mesh originalMesh;
        private HashSet<VertexAttribute> originalVertexAttributes = new HashSet<VertexAttribute>();
        private List<Vector3> originalVertices = new List<Vector3>();
        private List<BoneWeight> originalBoneWeights = new List<BoneWeight>();
        private List<int> originalTriangles = new List<int>();
        private List<List<Vector2>> originalUVs = new List<List<Vector2>> {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
        private List<Color> originalColors = new List<Color>();

        private int subMeshCount = 0;
        private int currentSubMeshIndex = 0;

        private List<Vector3> topVertices = new List<Vector3>();
        private List<BoneWeight> topBoneWeights = new List<BoneWeight>();
        private List<int> topSubMeshIndex = new List<int>();
        private List<int> topTriangles = new List<int>();
        private Dictionary<int, int> topIndexMapping = new Dictionary<int, int>();
        private SortedDictionary<Vector3, int> topIntersectionIndexMapping = new SortedDictionary<Vector3, int>(new Vector3Comparator());
        private int topIntersectionCount = 0;
        private List<List<Vector3>> topIntersectionConnection = new List<List<Vector3>>();
        private List<List<Vector2>> topUVs = new List<List<Vector2>> {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
        private List<Color> topColors = new List<Color>();

        private List<Vector3> bottomVertices = new List<Vector3>();
        private List<BoneWeight> bottomBoneWeights = new List<BoneWeight>();
        private List<int> bottomSubMeshIndex = new List<int>();
        private List<int> bottomTriangles = new List<int>();
        private Dictionary<int, int> bottomIndexMapping = new Dictionary<int, int>();
        private SortedDictionary<Vector3, int> bottomIntersectionIndexMapping = new SortedDictionary<Vector3, int>(new Vector3Comparator());
        private int bottomIntersectionCount = 0;
        private List<List<Vector3>> bottomIntersectionConnection = new List<List<Vector3>>();
        private List<List<Vector2>> bottomUVs = new List<List<Vector2>> {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
        private List<Color> bottomColors = new List<Color>();


        public class SliceReturnValue
        {
            public GameObject topGameObject;
            public GameObject bottomGameObject;
        }

        public async Task<SliceReturnValue> SliceAsync(GameObject originalGameObject, int skinnedMeshRendererIndex, int rootIndex, Plane slicePlane,Material intersectionMaterial)
        {
            CopyOriginalData(originalGameObject, skinnedMeshRendererIndex, rootIndex, slicePlane);

            for(currentSubMeshIndex=0; currentSubMeshIndex<subMeshCount; currentSubMeshIndex++)
            {
                originalMesh.GetTriangles(originalTriangles,currentSubMeshIndex);
                await Task.Run(() => LoopThroughTriangles());
            }

            await Task.Run(() => FillIntersection(topIntersectionConnection,true));
            await Task.Run(() => FillIntersection(bottomIntersectionConnection,false));

            return CreateNewGameObjects(originalGameObject, skinnedMeshRendererIndex, rootIndex, intersectionMaterial);
        }
        public SliceReturnValue Slice(GameObject originalGameObject, int skinnedMeshRendererIndex, int rootIndex, Plane slicePlane,Material intersectionMaterial)
        {
            CopyOriginalData(originalGameObject, skinnedMeshRendererIndex, rootIndex, slicePlane);

            for(currentSubMeshIndex=0; currentSubMeshIndex<subMeshCount; currentSubMeshIndex++)
            {
                originalMesh.GetTriangles(originalTriangles,currentSubMeshIndex);
                LoopThroughTriangles();
            }

            FillIntersection(topIntersectionConnection,true);
            FillIntersection(bottomIntersectionConnection,false);

            return CreateNewGameObjects(originalGameObject, skinnedMeshRendererIndex, rootIndex, intersectionMaterial);
        }

        private void CopyOriginalData(GameObject originalGameObject, int skinnedMeshRendererIndex, int rootIndex, Plane slicePlane)
        {
            SkinnedMeshRenderer originalSkinnedMeshRenderer = originalGameObject.transform.GetChild(skinnedMeshRendererIndex).GetComponent<SkinnedMeshRenderer>();

            this.slicePlane = slicePlane;

            originalMesh = originalSkinnedMeshRenderer.sharedMesh;
            originalBones = originalSkinnedMeshRenderer.bones.ToList();
            originalBonesToWorldMatrix.Clear();
            foreach(Transform bone in originalBones)
            {
                originalBonesToWorldMatrix.Add(bone.localToWorldMatrix);
            }
            originalMesh.GetBindposes(originalBindPoses);

            originalMesh.GetVertices(originalVertices);
            originalMesh.GetBoneWeights(originalBoneWeights);
            originalVertexAttributes.Clear();
            for(int i=0; i<8; i++)
            {
                if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
                {
                    break;
                }
                originalVertexAttributes.Add(UV_ATTRIBTES[i]);
                originalMesh.GetUVs(i, originalUVs[i]);
            }
            if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
            {
                originalVertexAttributes.Add(VertexAttribute.Color);
                originalMesh.GetColors(originalColors);
            }
            subMeshCount = originalMesh.subMeshCount;

            topVertices.Clear();
            topBoneWeights.Clear();
            topSubMeshIndex.Clear();
            topTriangles.Clear();
            topIndexMapping.Clear();
            topIntersectionIndexMapping.Clear();
            topIntersectionCount = 0;
            topIntersectionConnection.Clear();
            topUVs.ForEach(x=>x.Clear());
            topColors.Clear();

            bottomVertices.Clear();
            bottomBoneWeights.Clear();
            bottomSubMeshIndex.Clear();
            bottomTriangles.Clear();
            bottomIndexMapping.Clear();
            bottomIntersectionIndexMapping.Clear();
            bottomIntersectionCount = 0;
            bottomIntersectionConnection.Clear();
            bottomUVs.ForEach(x=>x.Clear());
            bottomColors.Clear();
        }

        private void LoopThroughTriangles()
        {
            bool vertexOneSide, vertexTwoSide, vertexThreeSide;
            for(int i=0; i<originalTriangles.Count; i+=3)
            {
                vertexOneSide = slicePlane.GetSide(ToWorldPosition(originalVertices[originalTriangles[i]], originalBoneWeights[originalTriangles[i]]));
                vertexTwoSide = slicePlane.GetSide(ToWorldPosition(originalVertices[originalTriangles[i+1]], originalBoneWeights[originalTriangles[i+1]]));
                vertexThreeSide = slicePlane.GetSide(ToWorldPosition(originalVertices[originalTriangles[i+2]], originalBoneWeights[originalTriangles[i+2]]));


                if(vertexOneSide && vertexTwoSide && vertexThreeSide)
                {
                    for(int j=i; j<i+3; j++)
                    {
                        topTriangles.Add(TopGetMapping(originalTriangles[j]));
                    }
                }
                else if(!vertexOneSide && !vertexTwoSide && !vertexThreeSide)
                {
                    for(int j=i; j<i+3; j++)
                    {
                        bottomTriangles.Add(BottomGetMapping(originalTriangles[j]));
                    }
                }
                else
                {
                    if(vertexTwoSide == vertexThreeSide)
                    {
                        AddIntersections(originalTriangles[i], originalTriangles[i+1], originalTriangles[i+2], vertexOneSide);
                    }
                    else if(vertexOneSide == vertexThreeSide)
                    {
                        AddIntersections(originalTriangles[i+1], originalTriangles[i+2], originalTriangles[i], vertexTwoSide);
                    }
                    else
                    {
                        AddIntersections(originalTriangles[i+2], originalTriangles[i], originalTriangles[i+1], vertexThreeSide);
                    }
                }
            }
        }

        private void FillIntersection(List<List<Vector3>> intersection, bool isTop)
        {
            (Vector3, Vector3, Vector3) plane = (Vector3.zero,Vector3.zero,Vector3.zero);
            ContourTree contourTree = new ContourTree();
            for(int i=0; i<intersection.Count; i++)
            {
                List<int> mapping;
                List<Vector3> worldPosition;
                if(isTop)
                {
                    mapping = intersection[i].Select(x=>topIntersectionIndexMapping[x]).ToList();
                    worldPosition = mapping.Select((x,j)=>ToWorldPosition(intersection[i][j], topBoneWeights[topIndexMapping[x]])).ToList();
                }
                else
                {
                    mapping = intersection[i].Select(x=>bottomIntersectionIndexMapping[x]).ToList();
                    worldPosition = mapping.Select((x,j)=>ToWorldPosition(intersection[i][j], bottomBoneWeights[bottomIndexMapping[x]])).ToList();
                }
                if(0 == i)
                {
                    plane = PlaneProjection.GetPlane(worldPosition,slicePlane.normal);
                }
                List<Vector2> contourIntersection = PlaneProjection.Get2DProjection(worldPosition,slicePlane.normal,plane.Item1,plane.Item2,plane.Item3).ToList();

                float area = 0f;
                int last = contourIntersection.Count-1;
                for(int current=0; current<contourIntersection.Count; current++)
                {
                    area += (contourIntersection[last].x + contourIntersection[current].x) * (contourIntersection[last].y - contourIntersection[current].y);
                    last = current;
                }
            
                if(area > EPSILON)
                {
                    contourIntersection.Reverse();
                    mapping.Reverse();
                }

                #if VERBOSE
                Debug.Log(area);
                StringBuilder str = new StringBuilder();
                for(int j = 0; j<intersection[i].Count; j++)
                {
                    str.Append($"{contourIntersection[j].x.ToString("F7")}, {contourIntersection[j].y.ToString("F7")}\n");
                }
                Debug.Log(str.ToString());
                #endif
                contourTree.AddContour(contourIntersection, mapping);
            }

            TraverseContourTree(contourTree, true, isTop);
        }

        private SliceReturnValue CreateNewGameObjects(GameObject originalGameObject, int skinnedMeshRendererIndex, int rootIndex,  Material intersectionMaterial)
        {
            Transform originalRoot = originalGameObject.transform.GetChild(rootIndex);
            GameObject topGameObject = UnityEngine.Object.Instantiate(originalGameObject);
            SkinnedMeshRenderer topSkinnedMeshRenderer = topGameObject.transform.GetChild(skinnedMeshRendererIndex).GetComponent<SkinnedMeshRenderer>();
            Transform topRoot = topGameObject.transform.GetChild(rootIndex);
            GameObject bottomGameObject = UnityEngine.Object.Instantiate(topGameObject);
            SkinnedMeshRenderer bottomSkinnedMeshRenderer = bottomGameObject.transform.GetChild(skinnedMeshRendererIndex).GetComponent<SkinnedMeshRenderer>();
            Transform bottomRoot = bottomGameObject.transform.GetChild(rootIndex);

            List<Transform> originalTransforms = new List<Transform>();
            List<Transform> topTransforms = new List<Transform>();
            List<Transform> bottomTransforms = new List<Transform>();

            Queue<Transform> q = new Queue<Transform>();
            q.Enqueue(originalRoot);
            while(0 != q.Count)
            {
                Transform t = q.Dequeue();
                originalTransforms.Add(t);
                for(int i=0; i<t.childCount; i++)
                {
                    q.Enqueue(t.GetChild(i));
                }
            }
            q.Enqueue(topRoot);
            while(0 != q.Count)
            {
                Transform t = q.Dequeue();
                topTransforms.Add(t);
                for(int i=0; i<t.childCount; i++)
                {
                    q.Enqueue(t.GetChild(i));
                }
            }
            q.Enqueue(bottomRoot);
            while(0 != q.Count)
            {
                Transform t = q.Dequeue();
                bottomTransforms.Add(t);
                for(int i=0; i<t.childCount; i++)
                {
                    q.Enqueue(t.GetChild(i));
                }
            }
            List<int> bonesMapping = originalBones.Select(x=>originalTransforms.FindIndex(y=>x==y)).ToList();
            

            topSkinnedMeshRenderer.materials = topSkinnedMeshRenderer.materials.Concat(new Material[]{intersectionMaterial}).ToArray();
            topSkinnedMeshRenderer.bones = bonesMapping.Select(x=>topTransforms[x]).ToArray();
            Mesh topMesh = new Mesh();
            topMesh.bindposes = originalBindPoses.ToArray();
            topMesh.vertices = topVertices.ToArray();
            topMesh.boneWeights = topBoneWeights.ToArray();
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                topMesh.SetUVs(i, topUVs[i]);
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                topMesh.SetColors(topColors);
            }
            topMesh.subMeshCount = subMeshCount+1;
            for(int i = 0; i<=subMeshCount; i++)
            {
                topMesh.SetTriangles(topTriangles.Where((x) => i==topSubMeshIndex[x]).ToArray(),i);
            }
            topMesh.RecalculateNormals();
            topMesh.RecalculateTangents();
            topMesh.Optimize();
            topSkinnedMeshRenderer.sharedMesh = topMesh;

            bottomSkinnedMeshRenderer.materials = bottomSkinnedMeshRenderer.materials.Concat(new Material[] { intersectionMaterial }).ToArray();
            bottomSkinnedMeshRenderer.bones = bonesMapping.Select(x=>bottomTransforms[x]).ToArray();
            Mesh bottomMesh = new Mesh();
            bottomMesh.bindposes = originalBindPoses.ToArray();
            bottomMesh.vertices = bottomVertices.ToArray();
            bottomMesh.boneWeights = bottomBoneWeights.ToArray();
            for(int i = 0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                bottomMesh.SetUVs(i,bottomUVs[i]);
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                bottomMesh.SetColors(bottomColors);
            }
            bottomMesh.subMeshCount = subMeshCount+1;
            for(int i = 0; i<=subMeshCount; i++)
            {
                bottomMesh.SetTriangles(bottomTriangles.Where((x) => i==bottomSubMeshIndex[x]).ToArray(),i);
            }
            bottomMesh.RecalculateNormals();
            bottomMesh.RecalculateTangents();
            bottomMesh.Optimize();
            bottomSkinnedMeshRenderer.sharedMesh = bottomMesh;

            return new SliceReturnValue{topGameObject=topGameObject, bottomGameObject=bottomGameObject};
        }

        private void TraverseContourTree(ContourTree current, bool shouldSkip, bool isTop)
        {
            if(shouldSkip)
            {
                goto TraverseContourTree_NEXT;
            }

            List<List<Vector2>> triangulationPoint = new List<List<Vector2>>();
            List<List<int>> triangulationMapping = new List<List<int>>();

            triangulationPoint.Add(current.contour);
            triangulationMapping.Add(current.contourId);

            foreach(ContourTree contourTree in current.children)
            {
                contourTree.contour.Reverse();
                contourTree.contourId.Reverse();
                triangulationPoint.Add(contourTree.contour);
                triangulationMapping.Add(contourTree.contourId);
            }

            (int, int[,]) triangulationRes = Triangulation.Triangulate(triangulationPoint);
            if(isTop)
            {
                for(int i=0; i<triangulationRes.Item1; i++)
                {
                    for(int j=2; j>=0; j--)
                    {
                        int pointIndex = triangulationRes.Item2[i,j]-1;
                        for(int k=0; k<triangulationMapping.Count; k++)
                        {
                            if(pointIndex>=triangulationMapping[k].Count)
                            {
                                pointIndex -= triangulationMapping[k].Count;
                            }
                            else
                            {
                                topTriangles.Add(topIndexMapping[triangulationMapping[k][pointIndex]]);
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                for(int i=0; i<triangulationRes.Item1; i++)
                {
                    for(int j=0; j<3; j++)
                    {
                        int pointIndex = triangulationRes.Item2[i,j]-1;
                        for(int k=0; k<triangulationMapping.Count; k++)
                        {
                            if(pointIndex>=triangulationMapping[k].Count)
                            {
                                pointIndex -= triangulationMapping[k].Count;
                            }
                            else
                            {
                                bottomTriangles.Add(bottomIndexMapping[triangulationMapping[k][pointIndex]]);
                                break;
                            }
                        }
                    }
                }
            }

            TraverseContourTree_NEXT:
            foreach(ContourTree child in current.children)
            {
                TraverseContourTree(child, !shouldSkip, isTop);
            }
        }

        private void AddIntersections(int originalIndexPivot, int originalIndexOne, int originalIndexTwo, bool isTop)
        {
            int vertexOne, vertexTwo, vertexThree;
            int intersectionVertexOne, intersectionVertexTwo;
            (Vector3, float, BoneWeight) intersectionOne, intersectionTwo;

            intersectionOne = GetIntersection(originalIndexPivot, originalIndexOne);
            intersectionTwo = GetIntersection(originalIndexPivot, originalIndexTwo);

            TopGetIntersectionMapping(intersectionOne.Item1, intersectionOne.Item3);
            TopGetIntersectionMapping(intersectionTwo.Item1, intersectionTwo.Item3);
            ConnectIntersection(intersectionOne.Item1, intersectionTwo.Item1, topIntersectionConnection);
            BottomGetIntersectionMapping(intersectionOne.Item1, intersectionOne.Item3);
            BottomGetIntersectionMapping(intersectionTwo.Item1, intersectionTwo.Item3);
            ConnectIntersection(intersectionOne.Item1, intersectionTwo.Item1, bottomIntersectionConnection);

            if(isTop)
            {
                vertexOne = TopGetMapping(originalIndexPivot);
                intersectionVertexOne = TopGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2, intersectionOne.Item3);
                intersectionVertexTwo = TopGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2, intersectionTwo.Item3);

                topTriangles.Add(vertexOne);
                topTriangles.Add(intersectionVertexOne);
                topTriangles.Add(intersectionVertexTwo);

                vertexTwo = BottomGetMapping(originalIndexOne);
                vertexThree = BottomGetMapping(originalIndexTwo);

                intersectionVertexOne = BottomGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2, intersectionOne.Item3);
                intersectionVertexTwo = BottomGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2, intersectionTwo.Item3);

                bottomTriangles.Add(intersectionVertexOne);
                bottomTriangles.Add(vertexTwo);
                bottomTriangles.Add(vertexThree);
                bottomTriangles.Add(intersectionVertexOne);
                bottomTriangles.Add(vertexThree);
                bottomTriangles.Add(intersectionVertexTwo);
            }
            else
            {
                vertexOne = BottomGetMapping(originalIndexPivot);
                intersectionVertexOne = BottomGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2, intersectionOne.Item3);
                intersectionVertexTwo = BottomGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2, intersectionTwo.Item3);

                bottomTriangles.Add(vertexOne);
                bottomTriangles.Add(intersectionVertexOne);
                bottomTriangles.Add(intersectionVertexTwo);


                vertexTwo = TopGetMapping(originalIndexOne);
                vertexThree = TopGetMapping(originalIndexTwo);

                intersectionVertexOne = TopGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2, intersectionOne.Item3);
                intersectionVertexTwo = TopGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2, intersectionTwo.Item3);

                topTriangles.Add(intersectionVertexOne);
                topTriangles.Add(vertexTwo);
                topTriangles.Add(vertexThree);
                topTriangles.Add(intersectionVertexOne);
                topTriangles.Add(vertexThree);
                topTriangles.Add(intersectionVertexTwo);
            }
        }

        private (Vector3, float, BoneWeight) GetIntersection(int originVertexIndex, int targetVertexIndex)
        {
            Vector3 origin = ToWorldPosition(originalVertices[originVertexIndex], originalBoneWeights[originVertexIndex]);
            Vector3 traget = ToWorldPosition(originalVertices[targetVertexIndex], originalBoneWeights[targetVertexIndex]);

            Vector3 distance = traget-origin;
            Ray ray = new Ray(origin, distance.normalized);
            float rayDistance;
            slicePlane.Raycast(ray,out rayDistance);

            BoneWeight intersectionBoneWeight = InterpolateBoneWeight(originalBoneWeights[originVertexIndex], originalBoneWeights[targetVertexIndex], rayDistance/distance.magnitude);
            return (ToLocalPosition(ray.GetPoint(rayDistance), intersectionBoneWeight), rayDistance/distance.magnitude, intersectionBoneWeight);
        }

        private void ConnectIntersection(Vector3 intersectionOne, Vector3 intersectionTwo, List<List<Vector3>> intersectionConnection)
        {
            Vector3Comparator vector3Comparator = new Vector3Comparator();
            (int, int) intersectionOneIndex = (-1,-1);
            (int, int) intersectionTwoIndex = (-1,-1);
            for(int i=0; i<intersectionConnection.Count; i++)
            {
                if(0 == vector3Comparator.Compare(intersectionOne, intersectionConnection[i][0]))
                {
                    intersectionOneIndex = (i,0);
                    if((-1,-1) != intersectionTwoIndex)
                    {
                        break;
                    }
                }
                else if(0 == vector3Comparator.Compare(intersectionOne, intersectionConnection[i][^1]))
                {
                    intersectionOneIndex = (i,intersectionConnection[i].Count-1);
                    if((-1,-1) != intersectionTwoIndex)
                    {
                        break;
                    }
                }

                if(0 == vector3Comparator.Compare(intersectionTwo, intersectionConnection[i][0]))
                {
                    intersectionTwoIndex = (i,0);
                    if((-1,-1) != intersectionOneIndex)
                    {
                        break;
                    }
                }
                else if(0 == vector3Comparator.Compare(intersectionTwo, intersectionConnection[i][^1]))
                {
                    intersectionTwoIndex = (i,intersectionConnection[i].Count-1);
                    if((-1,-1) != intersectionOneIndex)
                    {
                        break;
                    }
                }
            }

            if((-1,-1) == intersectionOneIndex && (-1,-1) == intersectionTwoIndex)
            {
                intersectionConnection.Add(new List<Vector3>{intersectionOne, intersectionTwo});
            }
            else if((-1,-1) != intersectionOneIndex && (-1,-1) == intersectionTwoIndex)
            {
                if(Polygon.IsStraightReturn.STRAIGHT == Polygon.IsStraight(intersectionConnection[intersectionOneIndex.Item1]))
                {
                    Vector3 newEdge, oldEdge, cross;

                    if(0 == intersectionOneIndex.Item2)
                    {
                        newEdge = intersectionOne - intersectionTwo;
                        oldEdge = intersectionConnection[intersectionOneIndex.Item1][1] - intersectionOne;
                        cross = Vector3.Cross(newEdge, oldEdge);
                    }
                    else
                    {
                        newEdge = intersectionTwo - intersectionOne;
                        oldEdge = intersectionOne-intersectionConnection[intersectionOneIndex.Item1][intersectionOneIndex.Item2-1];
                        cross = Vector3.Cross(oldEdge, newEdge);
                    }
                    float direction = Vector3.Dot(cross, slicePlane.normal);
                    if(direction<-EPSILON)
                    {
                        intersectionConnection[intersectionOneIndex.Item1].Reverse();
                        if(0 != intersectionOneIndex.Item2)
                        {
                            intersectionConnection[intersectionOneIndex.Item1].Insert(0,intersectionTwo);
                        }
                        else
                        {
                            intersectionConnection[intersectionOneIndex.Item1].Add(intersectionTwo);
                        }
                    }
                    else
                    {
                        if(0 != intersectionOneIndex.Item2)
                        {
                            intersectionConnection[intersectionOneIndex.Item1].Add(intersectionTwo);
                        }
                        else
                        {
                            intersectionConnection[intersectionOneIndex.Item1].Insert(0,intersectionTwo);
                        }
                    }
                }
                else
                {
                    if(0 != intersectionOneIndex.Item2)
                    {
                        intersectionConnection[intersectionOneIndex.Item1].Add(intersectionTwo);
                    }
                    else
                    {
                        intersectionConnection[intersectionOneIndex.Item1].Insert(0,intersectionTwo);
                    }
                }
            }
            else if((-1,-1) == intersectionOneIndex && (-1,-1) != intersectionTwoIndex)
            {
                if(Polygon.IsStraightReturn.STRAIGHT == Polygon.IsStraight(intersectionConnection[intersectionTwoIndex.Item1]))
                {
                    Vector3 newEdge, oldEdge, cross;

                    if(0 == intersectionTwoIndex.Item2)
                    {
                        newEdge = intersectionTwo - intersectionOne;
                        oldEdge = intersectionConnection[intersectionTwoIndex.Item1][1] - intersectionTwo;
                        cross = Vector3.Cross(newEdge, oldEdge);
                    }
                    else
                    {
                        newEdge = intersectionOne - intersectionTwo;
                        oldEdge = intersectionTwo-intersectionConnection[intersectionTwoIndex.Item1][intersectionTwoIndex.Item2-1];
                        cross = Vector3.Cross(oldEdge, newEdge);
                    }
                    float direction = Vector3.Dot(cross, slicePlane.normal);
                    if(direction<-EPSILON)
                    {
                        intersectionConnection[intersectionTwoIndex.Item1].Reverse();
                        if(0 != intersectionTwoIndex.Item2)
                        {
                            intersectionConnection[intersectionTwoIndex.Item1].Insert(0,intersectionOne);
                        }
                        else
                        {
                            intersectionConnection[intersectionTwoIndex.Item1].Add(intersectionOne);
                        }
                    }
                    else
                    {
                        if(0 != intersectionTwoIndex.Item2)
                        {
                            intersectionConnection[intersectionTwoIndex.Item1].Add(intersectionOne);
                        }
                        else
                        {
                            intersectionConnection[intersectionTwoIndex.Item1].Insert(0,intersectionOne);
                        }
                    }
                }
                else
                {
                    if(0 != intersectionTwoIndex.Item2)
                    {
                        intersectionConnection[intersectionTwoIndex.Item1].Add(intersectionOne);
                    }
                    else
                    {
                        intersectionConnection[intersectionTwoIndex.Item1].Insert(0,intersectionOne);
                    }
                }
            }
            else
            {
                if(intersectionOneIndex.Item1 == intersectionTwoIndex.Item1)
                {
                    return;
                }

                if(0 == intersectionOneIndex.Item2)
                {
                    if(0 == intersectionTwoIndex.Item2)
                    {
                        intersectionConnection[intersectionOneIndex.Item1].Reverse();
                        intersectionConnection[intersectionOneIndex.Item1].AddRange(intersectionConnection[intersectionTwoIndex.Item1]);
                    }
                    else
                    {
                        intersectionConnection[intersectionOneIndex.Item1].InsertRange(0,intersectionConnection[intersectionTwoIndex.Item1]);
                    }
                }
                else
                {
                    if(0 == intersectionTwoIndex.Item2)
                    {
                        intersectionConnection[intersectionOneIndex.Item1].AddRange(intersectionConnection[intersectionTwoIndex.Item1]);
                    }
                    else
                    {
                        intersectionConnection[intersectionTwoIndex.Item1].Reverse();
                        intersectionConnection[intersectionOneIndex.Item1].AddRange(intersectionConnection[intersectionTwoIndex.Item1]);
                    }
                }
                intersectionConnection.RemoveAt(intersectionTwoIndex.Item1);
            }
        }

        private int TopGetMapping(int originalVertexIndex)
        {
            if(!topIndexMapping.ContainsKey(originalVertexIndex))
            {
                topIndexMapping[originalVertexIndex] = topVertices.Count;

                TopCopyVertex(originalVertexIndex);
            }
            return topIndexMapping[originalVertexIndex];
        }
        private void TopCopyVertex(int originalVertexIndex)
        {
            topVertices.Add(originalVertices[originalVertexIndex]);
            topBoneWeights.Add(originalBoneWeights[originalVertexIndex]);
            topSubMeshIndex.Add(currentSubMeshIndex);
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                topUVs[i].Add(originalUVs[i][originalVertexIndex]);
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                topColors.Add(originalColors[originalVertexIndex]);
            }
        }
        private int TopGetMapping(int originalVertexIndex, int targetVertexIndex, Vector3 topIntersection, float interpolationAmount, BoneWeight topBoneWeight)
        {
            int res = topVertices.Count;

            topVertices.Add(topIntersection);
            topBoneWeights.Add(topBoneWeight);
            topSubMeshIndex.Add(currentSubMeshIndex);
            TopInterpolateVertex(originalVertexIndex, targetVertexIndex, interpolationAmount);

            return res;
        }
        private void TopInterpolateVertex(int originalVertexIndex, int targetVertexIndex, float interpolationAmount)
        {
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                topUVs[i].Add(Vector2.Lerp(originalUVs[i][originalVertexIndex], originalUVs[i][targetVertexIndex], interpolationAmount));
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                topColors.Add(Color.Lerp(originalColors[originalVertexIndex], originalColors[targetVertexIndex], interpolationAmount));
            }
        }
        private int TopGetIntersectionMapping(Vector3 topIntersection, BoneWeight topBoneWeight)
        {
            if(!topIntersectionIndexMapping.ContainsKey(topIntersection))
            {
                topIntersectionCount++;
                topIntersectionIndexMapping[topIntersection] = -topIntersectionCount;
                topIndexMapping[topIntersectionIndexMapping[topIntersection]] = topVertices.Count;

                topVertices.Add(topIntersection);
                topBoneWeights.Add(topBoneWeight);
                topSubMeshIndex.Add(subMeshCount);
                TopIntersectionInterpolateVertex();
            }

            return topIndexMapping[topIntersectionIndexMapping[topIntersection]];
        }
        private void TopIntersectionInterpolateVertex() // temp
        {
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                topUVs[i].Add(Vector2.zero);
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                topColors.Add(Color.white);
            }
        }

        private int BottomGetMapping(int originalVertexIndex)
        {
            if(!bottomIndexMapping.ContainsKey(originalVertexIndex))
            {
                bottomIndexMapping[originalVertexIndex] = bottomVertices.Count;

                BottomCopyVertex(originalVertexIndex);
            }
            return bottomIndexMapping[originalVertexIndex];
        }
        private void BottomCopyVertex(int originalVertexIndex)
        {
            bottomVertices.Add(originalVertices[originalVertexIndex]);
            bottomBoneWeights.Add(originalBoneWeights[originalVertexIndex]);
            bottomSubMeshIndex.Add(currentSubMeshIndex);
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                bottomUVs[i].Add(originalUVs[i][originalVertexIndex]);
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                bottomColors.Add(originalColors[originalVertexIndex]);
            }
        }
        private int BottomGetMapping(int originalVertexIndex, int targetVertexIndex, Vector3 bottomIntersection, float interpolationAmount, BoneWeight bottomBoneWeight)
        {
            int res = bottomVertices.Count;

            bottomVertices.Add(bottomIntersection);
            bottomBoneWeights.Add(bottomBoneWeight);
            bottomSubMeshIndex.Add(currentSubMeshIndex);
            BottomInterpolateVertex(originalVertexIndex, targetVertexIndex, interpolationAmount);

            return res;
        }
        private void BottomInterpolateVertex(int originalVertexIndex, int targetVertexIndex, float interpolationAmount)
        {
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                bottomUVs[i].Add(Vector2.Lerp(originalUVs[i][originalVertexIndex], originalUVs[i][targetVertexIndex], interpolationAmount));
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                bottomColors.Add(Color.Lerp(originalColors[originalVertexIndex], originalColors[targetVertexIndex], interpolationAmount));
            }
        }
        private int BottomGetIntersectionMapping(Vector3 bottomIntersection, BoneWeight bottomBoneWeight)
        {
            if(!bottomIntersectionIndexMapping.ContainsKey(bottomIntersection))
            {
                bottomIntersectionCount++;
                bottomIntersectionIndexMapping[bottomIntersection] = -bottomIntersectionCount;
                bottomIndexMapping[bottomIntersectionIndexMapping[bottomIntersection]] = bottomVertices.Count;

                bottomVertices.Add(bottomIntersection);
                bottomBoneWeights.Add(bottomBoneWeight);
                bottomSubMeshIndex.Add(subMeshCount);
                BottomIntersectionInterpolateVertex();
            }

            return bottomIndexMapping[bottomIntersectionIndexMapping[bottomIntersection]];
        }
        private void BottomIntersectionInterpolateVertex() // temp
        {
            for(int i=0; i<8; i++)
            {
                if(!originalVertexAttributes.Contains(UV_ATTRIBTES[i]))
                {
                    break;
                }
                bottomUVs[i].Add(Vector2.zero);
            }
            if(originalVertexAttributes.Contains(VertexAttribute.Color))
            {
                bottomColors.Add(Color.white);
            }
        }

        private Matrix4x4 GetToWorldMatrix(BoneWeight boneWeight)
        {
            Matrix4x4 m0 = originalBonesToWorldMatrix[boneWeight.boneIndex0] * originalBindPoses[boneWeight.boneIndex0];
            Matrix4x4 m1 = originalBonesToWorldMatrix[boneWeight.boneIndex1] * originalBindPoses[boneWeight.boneIndex1];
            Matrix4x4 m2 = originalBonesToWorldMatrix[boneWeight.boneIndex2] * originalBindPoses[boneWeight.boneIndex2];
            Matrix4x4 m3 = originalBonesToWorldMatrix[boneWeight.boneIndex3] * originalBindPoses[boneWeight.boneIndex3];

            Matrix4x4 toWorldMatrix = new Matrix4x4();
            for(int i=0; i<16; i++)
            {
                toWorldMatrix[i] = m0[i] * boneWeight.weight0 + m1[i] * boneWeight.weight1 + m2[i] * boneWeight.weight2 + m3[i] * boneWeight.weight3;
            }
            return toWorldMatrix;
        }
        private Vector3 ToWorldPosition(Vector3 localPosition, BoneWeight boneWeight)
        {
            return GetToWorldMatrix(boneWeight).MultiplyPoint3x4(localPosition);
        }
        private Vector3 ToLocalPosition(Vector3 worldPosition, BoneWeight boneWeight)
        {
            return GetToWorldMatrix(boneWeight).inverse.MultiplyPoint3x4(worldPosition);
        }

        private BoneWeight InterpolateBoneWeight(BoneWeight a, BoneWeight b, float interpolationAmount)
        {
            Dictionary<int, List<float>> boneWeights = new Dictionary<int, List<float>>();
            AddToDictionary(boneWeights, a.boneIndex0, a.weight0, true);
            AddToDictionary(boneWeights, a.boneIndex1, a.weight1, true);
            AddToDictionary(boneWeights, a.boneIndex2, a.weight2, true);
            AddToDictionary(boneWeights, a.boneIndex3, a.weight3, true);
            AddToDictionary(boneWeights, b.boneIndex0, b.weight0, false);
            AddToDictionary(boneWeights, b.boneIndex1, b.weight1, false);
            AddToDictionary(boneWeights, b.boneIndex2, b.weight2, false);
            AddToDictionary(boneWeights, b.boneIndex3, b.weight3, false);

            (int, float)[] resArray = boneWeights
                                                .Where(x=>x.Key>=0)
                                                .Select(x=>(x.Key, Mathf.Lerp(x.Value[0], x.Value[1], interpolationAmount)))
                                                .OrderByDescending(x=>x.Item2)
                                                .Take(4)
                                                .ToArray();
            float magnitude = resArray.Aggregate(0f, (sum, x)=>sum+x.Item2);
            BoneWeight res = new BoneWeight();

            if(resArray.Length>=1)
            {
                res.boneIndex0 = resArray[0].Item1;
                res.weight0 = resArray[0].Item2 / magnitude;;
            }
            if(resArray.Length>=2)
            {
                res.boneIndex1 = resArray[1].Item1;
                res.weight1 = resArray[1].Item2 / magnitude;
            }
            if(resArray.Length>=3)
            {
                res.boneIndex2 = resArray[2].Item1;
                res.weight2 = resArray[2].Item2 / magnitude;
            }
            if(resArray.Length>=4)
            {
                res.boneIndex3 = resArray[3].Item1;
                res.weight3 = resArray[3].Item2 / magnitude;
            }
            return res;
        }
        private void AddToDictionary(Dictionary<int, List<float>> boneWeights, int index, float weight, bool isFirst)
        {
            if(!boneWeights.ContainsKey(index))
            {
                boneWeights[index] = new List<float>{0f,0f};
            }
            if(isFirst)
            {
                boneWeights[index][0] = weight;
            }
            else
            {
                boneWeights[index][1] = weight;
            }
        }
    }
}
