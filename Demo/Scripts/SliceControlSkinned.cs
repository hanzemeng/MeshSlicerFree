using UnityEngine;
using TMPro;

namespace Hanzzz.MeshSlicerFree
{

    public class SliceControlSkinned : MonoBehaviour
    {
        [SerializeField] private GameObject originalGameObject;
        [SerializeField] private int skinnedMeshRendererIndex;
        [SerializeField] private int rootIndex;
        [SerializeField] private Transform slicePlane;
        [SerializeField] private Material intersectionMaterial;

        [SerializeField] private KeyCode cutKey;
        [SerializeField] private Vector3 topMoveDistance;
        [SerializeField] private Vector3 bottomMoveDistance;

        [SerializeField] private TMP_Text loggingText;

        private static SkinnedSlicer slicer;

        private void Awake()
        {
            if(null == slicer)
            {
                slicer = new SkinnedSlicer();
            }
        }

        private void Update()
        {
            if(!Input.GetKeyDown(cutKey) || null == originalGameObject)
            {
                return;
            }

            Plane plane = new Plane(slicePlane.up, slicePlane.position);
            SkinnedSlicer.SliceReturnValue sliceReturnValue;
            try
            {
                int triangleCount = originalGameObject.transform.GetChild(0).GetComponent<SkinnedMeshRenderer>().sharedMesh.triangles.Length;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                sliceReturnValue = slicer.Slice(originalGameObject, skinnedMeshRendererIndex, rootIndex, plane, intersectionMaterial);
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
