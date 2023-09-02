using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

public struct GaussianStruct
{
    public Vector3 Position;
    public Vector3 Scale;
    public Vector4 RotQuat;
    public float Opacity;

    // Nasty, again
    public Vector3 ShCoeffs1;
    public Vector3 ShCoeffs2;
    public Vector3 ShCoeffs3;
    public Vector3 ShCoeffs4;
    public Vector3 ShCoeffs5;
    public Vector3 ShCoeffs6;
    public Vector3 ShCoeffs7;
    public Vector3 ShCoeffs8;
    public Vector3 ShCoeffs9;
    public Vector3 ShCoeffs10;
    public Vector3 ShCoeffs11;
    public Vector3 ShCoeffs12;
    public Vector3 ShCoeffs13;
    public Vector3 ShCoeffs14;
    public Vector3 ShCoeffs15;
    public Vector3 ShCoeffs16;
}

public class GaussiansStruct
{
    public int NumGaussians { get; private set; }
    public int SphericalHarmonicsDegree { get; private set; }
    public int n_SphericalCoeff { get; private set; }
    public int shCoeffs_size { get; private set; }
    public GaussianStruct[] gaussianArray { get; private set; }

    public GaussiansStruct(byte[] arrayBuffer)
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

        // Limit the number of gaussians
        int maxGaussians = 50;
        NumGaussians = Math.Min(maxGaussians, NumGaussians);

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

        gaussianArray = new GaussianStruct[NumGaussians];
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

            shCoeffs_size = shFeatureOrder.Count / 3;
            var shCoeffs = new Vector3[shFeatureOrder.Count / 3];

            // Similar to reshape(3,-1)
            for (var j = 0; j < shFeatureOrder.Count; j += 3)
            {
                shCoeffs[j / 3] = new Vector3(rawVertex[shFeatureOrder[j]], rawVertex[shFeatureOrder[j + 1]], rawVertex[shFeatureOrder[j + 2]]);
            }

            gaussianArray[i].Position = new Vector3(rawVertex["x"], rawVertex["y"], rawVertex["z"]);
            gaussianArray[i].Scale = new Vector3((float)Math.Exp(rawVertex["scale_0"]), (float)Math.Exp(rawVertex["scale_1"]), (float)Math.Exp(rawVertex["scale_2"]));
            gaussianArray[i].RotQuat = rotation;
            gaussianArray[i].Opacity = Sigmoid(rawVertex["opacity"]);

            gaussianArray[i].ShCoeffs1 = shCoeffs[0];
            gaussianArray[i].ShCoeffs2 = shCoeffs[1];
            gaussianArray[i].ShCoeffs3 = shCoeffs[2];
            gaussianArray[i].ShCoeffs4 = shCoeffs[3];
            gaussianArray[i].ShCoeffs5 = shCoeffs[4];
            gaussianArray[i].ShCoeffs6 = shCoeffs[5];
            gaussianArray[i].ShCoeffs7 = shCoeffs[6];
            gaussianArray[i].ShCoeffs8 = shCoeffs[7];
            gaussianArray[i].ShCoeffs9 = shCoeffs[8];
            gaussianArray[i].ShCoeffs10 = shCoeffs[9];
            gaussianArray[i].ShCoeffs11 = shCoeffs[10];
            gaussianArray[i].ShCoeffs12 = shCoeffs[11];
            gaussianArray[i].ShCoeffs13 = shCoeffs[12];
            gaussianArray[i].ShCoeffs14 = shCoeffs[13];
            gaussianArray[i].ShCoeffs15 = shCoeffs[14];
            gaussianArray[i].ShCoeffs16 = shCoeffs[15];
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

    private float Sigmoid(float x)
    {
        return 1.0f / (1.0f + (float)Math.Exp(-x));
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


}