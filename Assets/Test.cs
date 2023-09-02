using UnityEngine;
using System.IO;


public class Test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string plyPath = "C:/Users/pablo_7xoop1s/Downloads/point_cloud.ply";
        GaussiansStruct gaussians = new GaussiansStruct(File.ReadAllBytes(plyPath));
        Debug.Log("placeholder");
    }
}
