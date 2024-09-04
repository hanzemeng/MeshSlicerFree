using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public class ConstrainedDelaunayTriangulationTest : MonoBehaviour
{
    public Transform pointsParent;
    public GameObject pointPrefab;
    public TextAsset polygonFile;
    [Range(3,1000)] public int pointCount;
    [Range(0f,20f)] public float pointRange;
    [Range(0f,0.2f)]public float pointScale;
    public MeshFilter resultMesh;


    [ContextMenu("Triangulate")]
    public void Triangulate()
    {
        List<Point2D> positions = new List<Point2D>();
        foreach(Transform child in pointsParent)
        {
            if(false == child.gameObject.activeSelf)
            {
                continue;
            }
            positions.Add(new Point2D(child.position));
        }
        List<int> edges = new List<int>();
        for(int i=0; i<positions.Count; i++)
        {
            edges.Add(i);
            edges.Add((i+1)%positions.Count);
        }
        ConstrainedDelaunayTriangulation cdt = new ConstrainedDelaunayTriangulation();
        var res = cdt.Triangulate(positions,edges);

        Mesh m = new Mesh();
        m.vertices = positions.Select(x=>(Vector3)(x.ToVector2())).ToArray();
        m.triangles = res.ToArray();
        resultMesh.mesh = m;
    }

    [ContextMenu("Read File")]
    public void ReadFile()
    {
        int n=3;
        string[] data = polygonFile.text.Split(new char[]{',','\n'});
        HashSet<(int,int)> coor = new HashSet<(int, int)>();
        for(int i=2; i<data.Length; i+=3)
        {
            int x = int.Parse(data[i-2]);
            int y = int.Parse(data[i-1]);
            if(coor.Contains((x,y)))
            {
                continue;
            }
            coor.Add((x,y));
            Transform t = Instantiate(pointPrefab,pointsParent).transform;
            t.gameObject.name = $"{n++}";
            t.position = new Vector3((float)x/100f,(float)y/100f,0f);
        }
        OnValidate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        while(0 != pointsParent.childCount)
        {
            DestroyImmediate(pointsParent.GetChild(0).gameObject);
        }

        for(int i=0; i<pointCount; i++)
        {
            Transform t = Instantiate(pointPrefab,pointsParent).transform;
            t.position = pointRange * Random.insideUnitCircle;
            t.localScale = pointScale*Vector3.one;
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        while(0 != pointsParent.childCount)
        {
            DestroyImmediate(pointsParent.GetChild(0).gameObject);
        }
        resultMesh.mesh = null;
    }

    public void OnValidate()
    {
        foreach(Transform child in pointsParent)
        {
            child.localScale = pointScale*Vector3.one;
        }
    }
}

}
