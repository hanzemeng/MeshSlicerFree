using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public class SliceControl : MonoBehaviour
{
    [SerializeField] private GameObject originalGameObject;
    [SerializeField] private Transform slicePlane;
    [SerializeField] private Material intersectionMaterial;

    [SerializeField] private KeyCode cutKey;
    [SerializeField] private Vector3 topMoveDistance;
    [SerializeField] private Vector3 bottomMoveDistance;

    private static Slicer slicer;

    private void Awake()
    {
        if(null == slicer)
        {
            slicer = new Slicer();
        }
    }

    private void Update()
    {
        if(!Input.GetKeyDown(cutKey) || null == originalGameObject)
        {
            return;
        }

        Plane plane = new Plane(slicePlane.up, slicePlane.position);
        Slicer.SliceReturnValue sliceReturnValue;
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            sliceReturnValue = slicer.Slice(originalGameObject, plane, intersectionMaterial);
            Debug.Log($"Slice time: {watch.ElapsedMilliseconds} ms.");
        }
        catch
        {
            sliceReturnValue = null;
            Debug.Log($"Slice failed.");
        }

        if(null == sliceReturnValue)
        {
            return;
        }
        sliceReturnValue.topGameObject.transform.SetParent(originalGameObject.transform.parent, false);
        sliceReturnValue.bottomGameObject.transform.SetParent(originalGameObject.transform.parent, false);
        sliceReturnValue.topGameObject.transform.position += topMoveDistance;
        sliceReturnValue.bottomGameObject.transform.position += bottomMoveDistance;

        Destroy(originalGameObject);
        originalGameObject = null;
    }
}

}
