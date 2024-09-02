using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public class MeshSectioner : MonoBehaviour
{
    [Header("Right click on the script to run the test.\n")]
    public Transform slicePlane;
    public GameObject sectionTarget;
    public float degreeDelta;
    public float splitDelta;
    public float moveDelta;
    public Material intersectionMaterial;
    public Transform resultParent;

    [ContextMenu("Section")]
    public void Section()
    {

        MeshSlicer meshSlicer = new MeshSlicer();
        for(float i=0f, ii=0f; i<=90f; i+=degreeDelta, ii+=1f)
        {
            for(float j=0f, jj=0f; j<=90f; j+=degreeDelta, jj+=1f)
            {
                slicePlane.rotation = Quaternion.Euler(i,0f,j);
                Vector3 normal = slicePlane.up;
                (GameObject, GameObject) res = meshSlicer.Slice(sectionTarget,Get3PointsOnPlane(new Plane(normal,slicePlane.position)), intersectionMaterial);
                if(null == res.Item1)
                {
                    continue;
                }
                res.Item1.transform.SetParent(resultParent,false);
                res.Item2.transform.SetParent(resultParent,false);
                res.Item1.transform.position += splitDelta*normal;
                res.Item2.transform.position -= splitDelta*normal;
                res.Item1.transform.position += moveDelta * (new Vector3(ii,jj,0f));
                res.Item2.transform.position += moveDelta * (new Vector3(ii,jj,0f));
            }
        }
    }

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
}

}
