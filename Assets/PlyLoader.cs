using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
//using UnityEngine;
using System.Numerics;

public class Gaussian
{
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    public Vector4 RotQuat { get; set; }
    public float Opacity { get; set; }
    public List<Vector3> ShCoeffs { get; set; }
    public Vector3? CameraSpacePos { get; set; }

    public Gaussian(
        Vector3 position,
        Vector3 scale,
        Vector4 rotQuat,
        float opacity,
        List<Vector3> shCoeffs,
        Vector3? cameraSpacePos = null)
    {
        Position = position;
        Scale = scale;
        RotQuat = rotQuat;
        Opacity = opacity;
        ShCoeffs = shCoeffs;
        CameraSpacePos = cameraSpacePos;
    }
}

public class Gaussians
{
    public int NumGaussians { get; private set; }
    public int SphericalHarmonicsDegree { get; private set; }
    public int n_SphericalCoeff { get; private set; }
    public List<Gaussian> GaussianList { get; private set; }

    public Gaussians(byte[] arrayBuffer)
    {
        // Get header only
        string endHeaderMarker = "end_header";
        string byteArrayAsString = System.Text.Encoding.ASCII.GetString(arrayBuffer);

        int endHeaderIndex = byteArrayAsString.IndexOf(endHeaderMarker);

        string headerText = "";

        if (endHeaderIndex >= 0)
        {
            headerText = byteArrayAsString.Substring(0, endHeaderIndex + endHeaderMarker.Length);
        }
        
        var lines = headerText.Split('\n');
        NumGaussians = 0;
        var propertyTypes = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("element vertex"))
            {
                NumGaussians = int.Parse(line.Split()[2]);
            }
            else if (line.StartsWith("property"))
            {
                var prop = line.Split();
                propertyTypes[prop[2]] = prop[1];
            }
        }

        Memory<byte> vertexData = new Memory<byte>(arrayBuffer, headerText.Length, arrayBuffer.Length - headerText.Length);
        
        var nCoeffsPerColor = propertyTypes.Count(prop => prop.Key.StartsWith("f_rest_")) / 3;
        SphericalHarmonicsDegree = (int)Math.Sqrt(nCoeffsPerColor + 1) - 1;
        n_SphericalCoeff = CalculateSphericalHarmonicsDegreeCoeff(SphericalHarmonicsDegree);
        var shFeatureOrder = new List<string>();
        for (var rgb = 0; rgb < 3; rgb++)
        {
            shFeatureOrder.Add($"f_dc_{rgb}");
        }
        
        for (var i = 0; i < nCoeffsPerColor; i++)
        {
            for (var rgb = 0; rgb < 3; rgb++)
            {
                shFeatureOrder.Add($"f_rest_{rgb * nCoeffsPerColor + i}");
            }
        }

        GaussianList = new List<Gaussian>();
        var offset = 0;
        for (var i = 0; i < NumGaussians; i++)
        {
            var result = ReadRawVertex(offset, vertexData, propertyTypes);
            offset = result.Item1;
            var rawVertex = result.Item2;
            var rotation = new Vector4(rawVertex["rot_0"], rawVertex["rot_1"], rawVertex["rot_2"], rawVertex["rot_3"]);
            var length = (float)Math.Sqrt(rotation.X * rotation.X + rotation.Y * rotation.Y + rotation.Z * rotation.Z + rotation.W * rotation.W);
            rotation /= length;

            //var shCoeffs = shFeatureOrder.Select(feature => rawVertex[feature]).ToList();

            var shCoeffs = new List<Vector3>();

            // Similar to reshape(3,-1)
            for (var j = 0 ; j < shFeatureOrder.Count; j += 3)
            {
                shCoeffs.Add(new Vector3(rawVertex[shFeatureOrder[j]], rawVertex[shFeatureOrder[j + 1]], rawVertex[shFeatureOrder[j + 2]]));
            }
            

            var gaussian = new Gaussian(
                new Vector3(rawVertex["x"], rawVertex["y"], rawVertex["z"]),
                new Vector3((float)Math.Exp(rawVertex["scale_0"]), (float)Math.Exp(rawVertex["scale_1"]), (float)Math.Exp(rawVertex["scale_2"])),
                rotation,
                Sigmoid(rawVertex["opacity"]),
                shCoeffs
            );
            GaussianList.Add(gaussian);
        }
    }

    private int CalculateSphericalHarmonicsDegreeCoeff(int n)
    {
        switch (n)
        {
            case 0:
                return 1;
            case 1:
                return 4;
            case 2:
                return 9;
            case 3:
                return 16;
            default:
                throw new ArgumentException($"Unsupported SH degree: {n}");
        }
    }

    private Tuple<int, Dictionary<string, float>> ReadRawVertex(int offset, ReadOnlyMemory<byte> vertexData, Dictionary<string, string> propertyTypes)
    {
        var vertex = new Dictionary<string, float>();
        foreach (var (prop, propType) in propertyTypes)
        {
            if (propType == "float")
            {
                vertex[prop] = BitConverter.ToSingle(vertexData.Span.Slice(offset, 4));
                offset += 4;
            }
            else if (propType == "uchar")
            {
                vertex[prop] = vertexData.Span[offset] / 255.0f;
                offset += 1;
            }
        }
        return new Tuple<int, Dictionary<string, float>>(offset, vertex);
    }

    private float Sigmoid(float x)
    {
        return 1.0f / (1.0f + (float)Math.Exp(-x));
    }
}