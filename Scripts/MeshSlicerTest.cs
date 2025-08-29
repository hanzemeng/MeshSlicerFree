using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace Hanzzz.MeshSlicerFree
{

public class MeshSlicerTest : MonoBehaviour
{
    [Header("Right click on the title of the script to see the available tests.\n")]

    public Transform slicePlane;
    public GameObject sliceTarget;
    public Material intersectionMaterial;
    public float splitDistance;

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
        if(null == result.Item1)
        {
            UnityEngine.Debug.Log("Slice plane does not intersect slice target.");
            return;
        }
        result.Item1.transform.SetParent(transform,false);
        result.Item2.transform.SetParent(transform,false);
        result.Item1.transform.position += splitDistance * slicePlane.up;
        result.Item2.transform.position -= splitDistance * slicePlane.up;
        sliceTarget.SetActive(false);
    }

    [ContextMenu("Slice")]
    public void Slice()
    {
        PreSliceOperation();
        result = meshSlicer.Slice(sliceTarget, Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    [ContextMenu("Slice Async")]
    public async void SliceAsync()
    {
        PreSliceOperation();
        result = await meshSlicer.SliceAsync(sliceTarget,Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)),intersectionMaterial);
        PostSliceOperation();
    }
    [ContextMenu("Slice Skinned")]
    public void SliceSkinned()
    {
        PreSliceOperation();
        result = skinnedMeshSlicer.Slice(sliceTarget, 0, 1, Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    [ContextMenu("Slice Skinned Async")]
    public async void SliceSkinnedAsync()
    {
        PreSliceOperation();
        result = await skinnedMeshSlicer.SliceAsync(sliceTarget, 0, 1, Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)), intersectionMaterial);
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
        sliceTarget.SetActive(true);
    }
}

}
