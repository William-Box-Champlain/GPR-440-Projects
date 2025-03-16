using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class GridConfig
{
    public Vector2Int mGridResolution = new Vector2Int();
    public NavMeshData mNavMesh = null;
    public List<Vector2> mSinkLocations = new List<Vector2>();
    public float mSinkRadius = 0.0f;
    public ComputeShader mComputeShader = null;
    public RenderTexture mGrid = null;
}

/// <summary>
/// This class is used to create a 2d graph using the navmesh as a starting point for the area to be quantized.
/// </summary>
public class Grid
{
    private Dictionary<NavMeshData, Mesh> mMeshDictionary;
    private Dictionary<NavMeshData, GridConfig> mConfigs;
    private Dictionary<Mesh, RenderTexture> mGrids;

    // Constructor initializes internal data.
    public Grid()
    {
        mMeshDictionary = new Dictionary<NavMeshData, Mesh>();
        mConfigs = new Dictionary<NavMeshData, GridConfig>();
        mGrids = new Dictionary<Mesh, RenderTexture>();
    }

    public RenderTexture GenerateTexture(NavMeshData navMeshData)
    {
        Mesh mesh;
        GridConfig config;
        if(mMeshDictionary.TryGetValue(navMeshData, out mesh) && mConfigs.TryGetValue(navMeshData,out config)) return GenerateTexture(mesh,config);
        else
        {
            FlattenNavMesh(navMeshData);

            return GenerateTexture(navMeshData);
        }
    }

    public RenderTexture GenerateTexture(Mesh mesh, GridConfig config)
    {
        if (mesh == null) return null;

        RenderTexture output = new RenderTexture
            (
                config.mGridResolution.x,
                config.mGridResolution.y,
                0
            )
            { enableRandomWrite = true };

        output.Create();

        Vector2[] trianglesData = new Vector2[mesh.triangles.Length];

        for (int i = 0; i < mesh.triangles.Length / 3; i++)
        {
            int triangle = i * 3;
            trianglesData[triangle] = mesh.vertices[mesh.triangles[triangle]];
            trianglesData[triangle + 1] = mesh.vertices[mesh.triangles[triangle + 1]];
            trianglesData[triangle + 2] = mesh.vertices[mesh.triangles[triangle + 2]];
        }

        using ComputeBuffer buffer = new ComputeBuffer(trianglesData.Length, sizeof(float) * 2);
        buffer.SetData(trianglesData);
        int kernel = config.mComputeShader.FindKernel("CSMain");

        config.mComputeShader.SetBuffer(kernel, "triangles", buffer);
        config.mComputeShader.SetInt("numTriangles", mesh.triangles.Length / 3);
        config.mComputeShader.SetVector("minBounds", mesh.bounds.min);
        config.mComputeShader.SetVector("maxBounds", mesh.bounds.max);
        config.mComputeShader.SetVector("textureSize", new Vector2(output.width, output.height));
        config.mComputeShader.SetTexture(kernel, "Result", output);
        using ComputeBuffer sinkBuffer = new ComputeBuffer(config.mSinkLocations.Count, sizeof(float) * 2);
        sinkBuffer.SetData(config.mSinkLocations);
        config.mComputeShader.SetBuffer(kernel, "sinks", sinkBuffer);
        config.mComputeShader.SetInt("numSinks", config.mSinkLocations.Count);
        config.mComputeShader.SetFloat("sinkRadius", config.mSinkRadius);

        config.mComputeShader.Dispatch
            (
                kernel,
                Mathf.CeilToInt(config.mGridResolution.x / 8.0f),
                Mathf.CeilToInt(config.mGridResolution.y / 8.0f),
                1
            );
        bool added = mGrids.TryAdd(mesh, output);
        if (!added)
        {
            mGrids[mesh] = output;
        }
        return output;
    }

    public bool TryGetGridTexture(NavMeshData data, out RenderTexture texture)
    {
        Mesh temp;
        texture = null;
        if (mMeshDictionary.TryGetValue(data, out temp))
        {
            return mGrids.TryGetValue(temp, out texture);
        }
        return false;
    }

    public void ConfigureGrid(GridConfig gridConfig, NavMeshData gridData)
    {
        mConfigs.Add(gridData, gridConfig);
    }

    public void UpdateSinks(List<GameObject> sinks, NavMeshData data)
    {
        if (mConfigs.TryGetValue(data, out var config))
        {
            config.mSinkLocations.Clear();
            foreach (var sink in sinks)
            {
                config.mSinkLocations.Add(sink.transform.position);
            }
        }
    }

    private void FlattenNavMesh(NavMeshData navMeshData)
    {
        NavMesh.RemoveAllNavMeshData();
        NavMesh.AddNavMeshData(navMeshData);
        NavMeshTriangulation temp = NavMesh.CalculateTriangulation();
        Mesh output = new Mesh();

        List<Vector3> tempVert = new List<Vector3>();
        foreach (var vert in temp.vertices)
        {
            tempVert.Add(new Vector3(vert.x,vert.y, 0));
        }
        output.vertices = tempVert.ToArray();
        output.triangles = temp.indices;
        output.RecalculateNormals();
        output.RecalculateBounds();

        bool added = mMeshDictionary.TryAdd(navMeshData, output);
        if (!added)
        {
            mMeshDictionary[navMeshData] = output;
        }
    }
}
