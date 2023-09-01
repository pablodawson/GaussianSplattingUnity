using UnityEngine;
using System.IO;


public class Test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string plyPath = "/Users/user/GaussianSplatting/Assets/point_cloud.ply";
        Gaussians gaussians = new Gaussians(File.ReadAllBytes(plyPath));
        Debug.Log("placeholder");
    }
}
