using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace Hanzzz.MeshSlicerFree
{

public class MeshSlicerTest : MonoBehaviour
{
    [Header("Right click on the script and to run the available tests.\n")]

    public Transform plane;
    public GameObject target;
    public Material intersectionMaterial;

    private MeshSlicer meshSlicer = new MeshSlicer();
    private SkinnedMeshSlicer skinnedMeshSlicer = new SkinnedMeshSlicer();
    private (GameObject, GameObject) result;

    public Text logText;
    private Stopwatch timer;


    private (Vector3,Vector3,Vector3) Get3PointsOnPlane(Plane p)
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
        return (-p.distance*p.normal, -p.distance*p.normal+xAxis, -p.distance*p.normal+yAxis);
    }

    private void PreSliceOperation()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        timer = Stopwatch.StartNew();
    }
    private void PostSliceOperation()
    {
        timer.Stop();
        string log = $"Slice Time: {timer.ElapsedMilliseconds}ms.";
        logText.text = log;
        UnityEngine.Debug.Log(log);
        result.Item1.transform.SetParent(transform,false);
        result.Item2.transform.SetParent(transform,false);
        target.SetActive(false);
    }

    [ContextMenu("Slice")]
    public void Slice()
    {
        PreSliceOperation();
        result = meshSlicer.Slice(target, Get3PointsOnPlane(new Plane(plane.up, plane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    [ContextMenu("Slice Async")]
    public async void SliceAsync()
    {
        PreSliceOperation();
        result = await meshSlicer.SliceAsync(target,Get3PointsOnPlane(new Plane(plane.up, plane.position)),intersectionMaterial);
        PostSliceOperation();
    }
    [ContextMenu("Slice Skinned")]
    public void SliceSkinned()
    {
        PreSliceOperation();
        result = skinnedMeshSlicer.Slice(target, 0, 1, Get3PointsOnPlane(new Plane(plane.up, plane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    [ContextMenu("Slice Skinned Async")]
    public async void SliceSkinnedAsync()
    {
        PreSliceOperation();
        result = await skinnedMeshSlicer.SliceAsync(target, 0, 1, Get3PointsOnPlane(new Plane(plane.up, plane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    
    [ContextMenu("Clear")]
    public void Clear()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        meshSlicer = new MeshSlicer();
        skinnedMeshSlicer = new SkinnedMeshSlicer();
        target.SetActive(true);
    }
}

}
