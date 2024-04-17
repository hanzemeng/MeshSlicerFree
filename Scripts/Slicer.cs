//#define VERBOSE

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if VERBOSE
using System.Text;
#endif

namespace Hanzzz.MeshSlicerFree
{

public class Slicer
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

    private static List<Type> RETAIN_TYPES = new List<Type>{typeof(GameObject), typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer)};
    private static List<VertexAttribute>  UV_ATTRIBTES = new List<VertexAttribute> {VertexAttribute.TexCoord0, VertexAttribute.TexCoord1, VertexAttribute.TexCoord2, VertexAttribute.TexCoord3, VertexAttribute.TexCoord4, VertexAttribute.TexCoord5, VertexAttribute.TexCoord6, VertexAttribute.TexCoord7 };

    private Plane slicePlane;

    private GameObject originalGameObject;
    private Mesh originalMesh;
    private List<Vector3> originalVertices = new List<Vector3>();
    private List<int> originalTriangles = new List<int>();
    private List<List<Vector2>> originalUVs = new List<List<Vector2>> {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
    private List<Color> originalColors = new List<Color>();

    private int subMeshCount = 0;
    private int currentSubMeshIndex = 0;

    private List<Vector3> topVertices = new List<Vector3>();
    private List<int> topSubMeshIndex = new List<int>();
    private List<int> topTriangles = new List<int>();
    private Dictionary<int, int> topIndexMapping = new Dictionary<int, int>();
    private SortedDictionary<Vector3, int> topIntersectionIndexMapping = new SortedDictionary<Vector3, int>(new Vector3Comparator());
    private int topIntersectionCount = 0;
    private List<List<Vector3>> topIntersectionConnection = new List<List<Vector3>>();
    private List<List<Vector2>> topUVs = new List<List<Vector2>> {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
    private List<Color> topColors = new List<Color>();

    private List<Vector3> bottomVertices = new List<Vector3>();
    private List<int> bottomSubMeshIndex = new List<int>();
    private List<int> bottomTriangles = new List<int>();
    private Dictionary<int, int> bottomIndexMapping = new Dictionary<int, int>();
    private SortedDictionary<Vector3, int> bottomIntersectionIndexMapping = new SortedDictionary<Vector3, int>(new Vector3Comparator());
    private int bottomIntersectionCount = 0;
    private List<List<Vector3>> bottomIntersectionConnection = new List<List<Vector3>>();
    private List<List<Vector2>> bottomUVs = new List<List<Vector2>> {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
    private List<Color> bottomColors = new List<Color>();

    private void Reset()
    {
        topVertices.Clear();
        topSubMeshIndex.Clear();
        topTriangles.Clear();
        topIndexMapping.Clear();
        topIntersectionIndexMapping.Clear();
        topIntersectionCount = 0;
        topIntersectionConnection.Clear();
        topUVs.ForEach(x=>x.Clear());
        topColors.Clear();

        bottomVertices.Clear();
        bottomSubMeshIndex.Clear();
        bottomTriangles.Clear();
        bottomIndexMapping.Clear();
        bottomIntersectionIndexMapping.Clear();
        bottomIntersectionCount = 0;
        bottomIntersectionConnection.Clear();
        bottomUVs.ForEach(x=>x.Clear());
        bottomColors.Clear();
    }

    public class SliceReturnValue
    {
        public GameObject topGameObject;
        public GameObject bottomGameObject;
    }
    public SliceReturnValue Slice(GameObject originalGameObject, Plane slicePlane, Material intersectionMaterial)
    {
        this.originalGameObject = originalGameObject;
        this.slicePlane = slicePlane;

        originalMesh = originalGameObject.GetComponent<MeshFilter>().sharedMesh;
        originalMesh.GetVertices(originalVertices);
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            originalMesh.GetUVs(i, originalUVs[i]);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            originalMesh.GetColors(originalColors);
        }
        subMeshCount = originalMesh.subMeshCount;

        Reset();

        bool vertexOneSide, vertexTwoSide, vertexThreeSide;
        for(currentSubMeshIndex=0; currentSubMeshIndex<subMeshCount; currentSubMeshIndex++)
        {
            originalMesh.GetTriangles(originalTriangles, currentSubMeshIndex);
            for(int i=0; i<originalTriangles.Count; i+=3)
            {
                vertexOneSide = slicePlane.GetSide(originalGameObject.transform.TransformPoint(originalVertices[originalTriangles[i]]));
                vertexTwoSide = slicePlane.GetSide(originalGameObject.transform.TransformPoint(originalVertices[originalTriangles[i+1]]));
                vertexThreeSide = slicePlane.GetSide(originalGameObject.transform.TransformPoint(originalVertices[originalTriangles[i+2]]));

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
        

        FillIntersection(topIntersectionConnection, true);
        FillIntersection(bottomIntersectionConnection, false);


        GameObject topGameObject = UnityEngine.Object.Instantiate(originalGameObject);
        Component[] components = topGameObject.GetComponents<Component>();
        foreach(Component component in components)
        {
            if(RETAIN_TYPES.Contains(component.GetType()))
            {
                continue;
            }
            UnityEngine.Object.DestroyImmediate(component);
        }
        GameObject bottomGameObject = UnityEngine.Object.Instantiate(topGameObject);

        topGameObject.GetComponent<MeshRenderer>().materials = topGameObject.GetComponent<MeshRenderer>().materials.Concat(new Material[]{intersectionMaterial}).ToArray();
        Mesh topMesh = new Mesh();
        topMesh.vertices=topVertices.ToArray();
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                continue;
            }
            topMesh.SetUVs(i, topUVs[i]);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
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
        topGameObject.GetComponent<MeshFilter>().mesh = topMesh;

        bottomGameObject.GetComponent<MeshRenderer>().materials = bottomGameObject.GetComponent<MeshRenderer>().materials.Concat(new Material[]{intersectionMaterial}).ToArray();
        Mesh bottomMesh = new Mesh();
        bottomMesh.vertices=bottomVertices.ToArray();
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                continue;
            }
            bottomMesh.SetUVs(i, bottomUVs[i]);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
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
        bottomGameObject.GetComponent<MeshFilter>().mesh = bottomMesh;

        return new SliceReturnValue{topGameObject=topGameObject, bottomGameObject=bottomGameObject};
    }

    private void FillIntersection(List<List<Vector3>> intersection, bool isTop)
    {
        List<List<Vector3>> intersectionWorld = intersection.Select(x=>x.Select(y=>originalGameObject.transform.TransformPoint(y)).ToList()).ToList();
        (Vector3, Vector3, Vector3) plane = PlaneProjection.GetPlane(intersectionWorld[0],slicePlane.normal);
        ContourTree contourTree = new ContourTree();
        for(int i = 0; i<intersection.Count; i++)
        {
            List<Vector2> contourIntersection = PlaneProjection.Get2DProjection(intersectionWorld[i],slicePlane.normal,plane.Item1,plane.Item2,plane.Item3).ToList();
            List<int> mapping;
            if(isTop)
            {
                mapping = intersection[i].Select(x=>topIntersectionIndexMapping[x]).ToList();
            }
            else
            {
                mapping = intersection[i].Select(x=>bottomIntersectionIndexMapping[x]).ToList();
            }

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
        (Vector3, float) intersectionOne, intersectionTwo;

        intersectionOne = GetIntersection(originalIndexPivot, originalIndexOne);
        intersectionTwo = GetIntersection(originalIndexPivot, originalIndexTwo);

        TopGetIntersectionMapping(intersectionOne.Item1);
        TopGetIntersectionMapping(intersectionTwo.Item1);
        ConnectIntersection(intersectionOne.Item1, intersectionTwo.Item1, topIntersectionConnection);
        BottomGetIntersectionMapping(intersectionOne.Item1);
        BottomGetIntersectionMapping(intersectionTwo.Item1);
        ConnectIntersection(intersectionOne.Item1, intersectionTwo.Item1, bottomIntersectionConnection);

        if(isTop)
        {
            vertexOne = TopGetMapping(originalIndexPivot);
            intersectionVertexOne = TopGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2);
            intersectionVertexTwo = TopGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2);

            topTriangles.Add(vertexOne);
            topTriangles.Add(intersectionVertexOne);
            topTriangles.Add(intersectionVertexTwo);

            vertexTwo = BottomGetMapping(originalIndexOne);
            vertexThree = BottomGetMapping(originalIndexTwo);

            intersectionVertexOne = BottomGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2);
            intersectionVertexTwo = BottomGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2);

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
            intersectionVertexOne = BottomGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2);
            intersectionVertexTwo = BottomGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2);

            bottomTriangles.Add(vertexOne);
            bottomTriangles.Add(intersectionVertexOne);
            bottomTriangles.Add(intersectionVertexTwo);


            vertexTwo = TopGetMapping(originalIndexOne);
            vertexThree = TopGetMapping(originalIndexTwo);

            intersectionVertexOne = TopGetMapping(originalIndexPivot, originalIndexOne, intersectionOne.Item1, intersectionOne.Item2);
            intersectionVertexTwo = TopGetMapping(originalIndexPivot, originalIndexTwo, intersectionTwo.Item1, intersectionTwo.Item2);

            topTriangles.Add(intersectionVertexOne);
            topTriangles.Add(vertexTwo);
            topTriangles.Add(vertexThree);
            topTriangles.Add(intersectionVertexOne);
            topTriangles.Add(vertexThree);
            topTriangles.Add(intersectionVertexTwo);
        }
    }

    private (Vector3, float) GetIntersection(int originVertexIndex, int targetVertexIndex)
    {
        Vector3 origin = originalGameObject.transform.TransformPoint(originalVertices[originVertexIndex]);
        Vector3 traget = originalGameObject.transform.TransformPoint(originalVertices[targetVertexIndex]);
        Vector3 distance = traget-origin;
        Ray ray = new Ray(origin, distance.normalized);

        float rayDistance;
        slicePlane.Raycast(ray,out rayDistance);
        return (originalGameObject.transform.InverseTransformPoint(ray.GetPoint(rayDistance)), rayDistance/distance.magnitude);
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
        topSubMeshIndex.Add(currentSubMeshIndex);
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            topUVs[i].Add(originalUVs[i][originalVertexIndex]);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            topColors.Add(originalColors[originalVertexIndex]);
        }
    }
    private int TopGetMapping(int originalVertexIndex, int targetVertexIndex, Vector3 topIntersection, float interpolationAmount)
    {
        int res = topVertices.Count;

        topVertices.Add(topIntersection);
        topSubMeshIndex.Add(currentSubMeshIndex);
        TopInterpolateVertex(originalVertexIndex, targetVertexIndex, interpolationAmount);

        return res;
    }
    private void TopInterpolateVertex(int originalVertexIndex, int targetVertexIndex, float interpolationAmount)
    {
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            topUVs[i].Add(Vector2.Lerp(originalUVs[i][originalVertexIndex], originalUVs[i][targetVertexIndex], interpolationAmount));
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            topColors.Add(Color.Lerp(originalColors[originalVertexIndex], originalColors[targetVertexIndex], interpolationAmount));
        }
    }
    private int TopGetIntersectionMapping(Vector3 topIntersection)
    {
        if(!topIntersectionIndexMapping.ContainsKey(topIntersection))
        {
            topIntersectionCount++;
            topIntersectionIndexMapping[topIntersection] = -topIntersectionCount;
            topIndexMapping[topIntersectionIndexMapping[topIntersection]] = topVertices.Count;

            topVertices.Add(topIntersection);
            topSubMeshIndex.Add(subMeshCount);
            TopIntersectionInterpolateVertex();
        }

        return topIndexMapping[topIntersectionIndexMapping[topIntersection]];
    }
    private void TopIntersectionInterpolateVertex() // temp
    {
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            topUVs[i].Add(Vector2.zero);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
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
        bottomSubMeshIndex.Add(currentSubMeshIndex);
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            bottomUVs[i].Add(originalUVs[i][originalVertexIndex]);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            bottomColors.Add(originalColors[originalVertexIndex]);
        }
    }
    private int BottomGetMapping(int originalVertexIndex, int targetVertexIndex, Vector3 bottomIntersection, float interpolationAmount)
    {
        int res = bottomVertices.Count;

        bottomVertices.Add(bottomIntersection);
        bottomSubMeshIndex.Add(currentSubMeshIndex);
        BottomInterpolateVertex(originalVertexIndex, targetVertexIndex, interpolationAmount);

        return res;
    }
    private void BottomInterpolateVertex(int originalVertexIndex, int targetVertexIndex, float interpolationAmount)
    {
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            bottomUVs[i].Add(Vector2.Lerp(originalUVs[i][originalVertexIndex], originalUVs[i][targetVertexIndex], interpolationAmount));
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            bottomColors.Add(Color.Lerp(originalColors[originalVertexIndex], originalColors[targetVertexIndex], interpolationAmount));
        }
    }
    private int BottomGetIntersectionMapping(Vector3 bottomIntersection)
    {
        if(!bottomIntersectionIndexMapping.ContainsKey(bottomIntersection))
        {
            bottomIntersectionCount++;
            bottomIntersectionIndexMapping[bottomIntersection] = -bottomIntersectionCount;
            bottomIndexMapping[bottomIntersectionIndexMapping[bottomIntersection]] = bottomVertices.Count;

            bottomVertices.Add(bottomIntersection);
            bottomSubMeshIndex.Add(subMeshCount);
            BottomIntersectionInterpolateVertex();
        }

        return bottomIndexMapping[bottomIntersectionIndexMapping[bottomIntersection]];
    }
    private void BottomIntersectionInterpolateVertex() // temp
    {
        for(int i=0; i<8; i++)
        {
            if(!originalMesh.HasVertexAttribute(UV_ATTRIBTES[i]))
            {
                break;
            }
            bottomUVs[i].Add(Vector2.zero);
        }
        if(originalMesh.HasVertexAttribute(VertexAttribute.Color))
        {
            bottomColors.Add(Color.white);
        }
    }
}

}
