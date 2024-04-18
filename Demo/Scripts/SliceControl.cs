using UnityEngine;
using TMPro;

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

    [SerializeField] private TMP_Text loggingText;

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
            int triangleCount = originalGameObject.GetComponent<MeshFilter>().sharedMesh.triangles.Length;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            sliceReturnValue = slicer.Slice(originalGameObject, plane, intersectionMaterial);
            loggingText.text = $"Triangle count: {triangleCount}; slice time: {watch.ElapsedMilliseconds} ms.";
        }
        catch
        {
            sliceReturnValue = null;
            loggingText.text = $"Slice failed.";
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
