using UnityEngine;

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


    [ContextMenu("Slice")]
    public void Slice()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        result = meshSlicer.Slice(target, new Plane(plane.up, plane.position), intersectionMaterial);
        result.Item1.transform.SetParent(transform,false);
        result.Item2.transform.SetParent(transform,false);
        target.SetActive(false);
    }
    [ContextMenu("Slice Async")]
    public async void SliceAsync()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        result = await meshSlicer.SliceAsync(target,new Plane(plane.up,plane.position),intersectionMaterial);
        result.Item1.transform.SetParent(transform,false);
        result.Item2.transform.SetParent(transform,false);
        target.SetActive(false);
    }
    [ContextMenu("Slice Skinned")]
    public void SliceSkinned()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        result = skinnedMeshSlicer.Slice(target, 0, 1, new Plane(plane.up, plane.position), intersectionMaterial);
        result.Item1.transform.SetParent(transform,false);
        result.Item2.transform.SetParent(transform,false);
        target.SetActive(false);
    }
    [ContextMenu("Slice Skinned Async")]
    public async void SliceSkinnedAsync()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        result = await skinnedMeshSlicer.SliceAsync(target, 0, 1, new Plane(plane.up, plane.position), intersectionMaterial);
        result.Item1.transform.SetParent(transform,false);
        result.Item2.transform.SetParent(transform,false);
        target.SetActive(false);
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
