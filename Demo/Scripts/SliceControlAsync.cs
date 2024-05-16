using UnityEngine;
using TMPro;

namespace Hanzzz.MeshSlicerFree
{
    public class SliceControlAsync : MonoBehaviour
    {
        [SerializeField] private GameObject originalGameObject;
        [SerializeField] private Transform slicePlane;
        [SerializeField] private Material intersectionMaterial;

        [SerializeField] private KeyCode cutKey;
        [SerializeField] private Vector3 topMoveDistance;
        [SerializeField] private Vector3 bottomMoveDistance;

        [SerializeField] private TMP_Text loggingText;

        private static Slicer slicer;
        private bool isSlicing;

        private void Awake()
        {
            if(null == slicer)
            {
                slicer = new Slicer();
            }
            isSlicing = false;
        }

        private void Update()
        {
            if(!Input.GetKeyDown(cutKey))
            {
                return;
            }

            Slice();
        }

        private async void Slice()
        {
            if(isSlicing)
            {
                return;
            }
            isSlicing = true;
            Plane plane = new Plane(slicePlane.up, slicePlane.position);

            int triangleCount = originalGameObject.GetComponent<MeshFilter>().sharedMesh.triangles.Length;
            int startFrame =  Time.frameCount;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Slicer.SliceReturnValue sliceReturnValue = await slicer.SliceAsync(originalGameObject, plane, intersectionMaterial);
            loggingText.text = $"Triangle count: {triangleCount}; slice frame: {Time.frameCount-startFrame}; slice time: {watch.ElapsedMilliseconds} ms.";

            sliceReturnValue.topGameObject.transform.SetParent(originalGameObject.transform.parent, false);
            sliceReturnValue.bottomGameObject.transform.SetParent(originalGameObject.transform.parent, false);
            sliceReturnValue.topGameObject.transform.position += topMoveDistance;
            sliceReturnValue.bottomGameObject.transform.position += bottomMoveDistance;

            Destroy(originalGameObject);
            originalGameObject = null;
        }
    }
}
