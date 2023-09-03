using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;


public class Gaussians
{
    public int NumGaussians { get; private set; }
    public int SphericalHarmonicsDegree { get; private set; }
    public int n_SphericalCoeff { get; private set; }
    public int shCoeffs_size { get; private set; }
    
    public Vector3[] Position { get; private set; }
    public Vector3[] Scale { get; private set; }
    public Vector4[] RotQuat { get; private set; }
    public float[] Opacity { get; private set; }
    public Vector3[] ShCoeffs { get; private set; }

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

        // Limit the number of gaussians
        int maxGaussians = 20000;
        NumGaussians = Math.Min(maxGaussians, NumGaussians);

        int vertexByteOffset = endHeaderIndex + "end_header".Length + 1;

        Memory<byte> vertexData = new Memory<byte>(arrayBuffer, vertexByteOffset, arrayBuffer.Length - vertexByteOffset);

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

        shCoeffs_size = shFeatureOrder.Count;

        Position = new Vector3[NumGaussians];
        Scale = new Vector3[NumGaussians];
        RotQuat = new Vector4[NumGaussians];
        Opacity = new float[NumGaussians];
        List<Vector3> ShCoeffsList = new List<Vector3>();
        
        var offset = 0;
        for (var i = 0; i < NumGaussians; i++)
        {
            var result = ReadRawVertex(offset, vertexData, propertyTypes);
            offset = result.Item1;
            var rawVertex = result.Item2;
            var rotation = new Vector4(rawVertex["rot_0"], rawVertex["rot_1"], rawVertex["rot_2"], rawVertex["rot_3"]);
            var length = (float)Math.Sqrt(rotation.X * rotation.X + rotation.Y * rotation.Y + rotation.Z * rotation.Z + rotation.W * rotation.W);
            rotation /= length;

            for (var j = 0 ; j < shFeatureOrder.Count; j += 3)
            {
                Vector3 sh = new Vector3(rawVertex[shFeatureOrder[j]], rawVertex[shFeatureOrder[j + 1]], rawVertex[shFeatureOrder[j + 2]]);
                ShCoeffsList.Add(sh);
            }

            Position[i] = new Vector3(rawVertex["x"], rawVertex["y"], rawVertex["z"]);
            Scale[i] = new Vector3((float)Math.Exp(rawVertex["scale_0"]), (float)Math.Exp(rawVertex["scale_1"]), (float)Math.Exp(rawVertex["scale_2"]));
            RotQuat[i] = rotation;
            Opacity[i] = Sigmoid(rawVertex["opacity"]);
        }
        ShCoeffs = ShCoeffsList.ToArray();
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
                vertex[prop] = BitConverter.ToSingle(vertexData.Span.Slice(offset));

                offset += sizeof(float);
            }
            else if (propType == "uchar")
            {
                vertex[prop] = vertexData.Span[offset] / 255.0f;
                offset += sizeof(byte);
            }
        }
        return new Tuple<int, Dictionary<string, float>>(offset, vertex);
    }


}