using UnityEngine;
using System.IO;


public class Test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string plyPath = "C:/Users/pablo_7xoop1s/Downloads/point_cloud.ply";
        Gaussians gaussians = new Gaussians(File.ReadAllBytes(plyPath));
        Debug.Log("placeholder");
    }
}
