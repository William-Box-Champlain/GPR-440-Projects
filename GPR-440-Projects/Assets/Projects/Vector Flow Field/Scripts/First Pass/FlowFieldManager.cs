using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public enum eAgentSize
{
    small,
    medium,
    large,
}

public class FlowFieldManager : MonoBehaviour
{
    [Header("FlowField Configuration")]
    [SerializeField] private Vector2Int mGridResolution = new Vector2Int(256, 256);
    [SerializeField] private ComputeShader mGridComputeShader = null;
    [SerializeField] private List<NavMeshSurface> mMeshSurfaces = new List<NavMeshSurface>();
    [SerializeField] private List<NavMeshData> mNavMeshes = new List<NavMeshData>();
    [SerializeField] private float mSinkRadius = 0.05f;
    [SerializeField] private List<GameObject> mSinkObjects = new List<GameObject>();

    [Header("Grid Mesh")]
    [SerializeField] private Mesh mGridMesh;
    [SerializeField] private Vector2 mWorldMin = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 mWorldMax = new Vector2(10f, 10f);

    [Header("NavierStokes Configuration")]
    [SerializeField] private ComputeShader mNavierStokesComputeShader = null;
    [SerializeField] private int mPressureIterations = 20;

    [Header("Update Mode")]
    [SerializeField] private bool mUseFixedUpdate = false;

    [Header("Cached Textures")]
    private Grid mGridGenerator;
    private VectorFlowFieldCalculator mNavierStokesManager;

    [Header("Flow Field Texture Settings")]
    private RenderTexture mFlowFieldRenderTexture;
    private Dictionary<NavMeshData, RenderTexture> mFlowFields = new Dictionary<NavMeshData, RenderTexture>();
    private Dictionary<eAgentSize, NavMeshData> mAgentData = new Dictionary<eAgentSize, NavMeshData>();
    [SerializeField] List<RenderTexture> mLevelMasks = new List<RenderTexture>();

    void Start()
    {
        // Instantiate subsystem classes.
        mGridGenerator = new Grid();
        mNavierStokesManager = new VectorFlowFieldCalculator();

        //Get navmeshdata

        foreach(var surface in mMeshSurfaces)
        {
            mNavMeshes.Add(surface.navMeshData);
        }

        // Configure GridGenerator.
        foreach(var data in mNavMeshes)
        {
            GridConfig gridConfig = new GridConfig();
            gridConfig.mGridResolution = mGridResolution;
            gridConfig.mSinkRadius = mSinkRadius;
            gridConfig.mComputeShader = mGridComputeShader;

            foreach(var loc in mSinkObjects)
            {
                gridConfig.mSinkLocations.Add(loc.transform.position);
            }
            gridConfig.mNavMesh = data;

            mGridGenerator.ConfigureGrid(gridConfig, data);
        }

        foreach(NavMeshData data in mNavMeshes)
        {
            mGridGenerator.GenerateTexture(data);
        }

        // Configure NavierStokesManager.
        mNavierStokesManager.NavierStokes = mNavierStokesComputeShader;
        mNavierStokesManager.ConfigureNavierStokes(mPressureIterations);
        mNavierStokesManager.Initialize(mGridResolution);

        // Transfer the grid texture to the NavierStokes system at startup.
        if (mGridGenerator != null)
        {
            foreach (var data in mNavMeshes)
            {
                mGridGenerator.UpdateSinks(mSinkObjects, data);
                TransferGridToNavierStokes(mGridGenerator.GenerateTexture(data));
            }
        }

        SimLoop(0.0f);
    }

    void FixedUpdate()
    {
        if (mUseFixedUpdate && mNavierStokesManager != null)
        {
            // Regenerate the grid each update if gridMesh is provided.
            if(mGridGenerator != null)
            {
                SimLoop(Time.deltaTime);
            }
        }
    }

    void Update()
    {
        if (!mUseFixedUpdate && mNavierStokesManager != null)
        {
            // Regenerate the grid each update if gridMesh is provided.
            if (mGridGenerator != null)
            {
                SimLoop(Time.deltaTime);
            }
        }
    }

    void SimLoop(float dt)
    {
        mLevelMasks.Clear();
        foreach (var data in mNavMeshes)
        {
            RenderTexture tempText = mGridGenerator.GenerateTexture(data);
            mGridGenerator.UpdateSinks(mSinkObjects, data);
            TransferGridToNavierStokes(tempText);
            mLevelMasks.Add(tempText);

            mNavierStokesManager.UpdateSimulation(dt, data);

            RenderTexture temp;
            if (mNavierStokesManager.TryGetRenderTexture(data, out temp))
            {
                if (!mFlowFields.TryAdd(data, temp))
                {
                    mFlowFields.Add(data, temp);
                }
            }
        }
    }

    /// <summary>
    /// Transfers the grid texture from the grid system to the NavierStokes simulator.
    /// </summary>
    public void TransferGridToNavierStokes(RenderTexture grid)
    {
        if (mGridGenerator == null || mNavierStokesManager == null)
        {
            Debug.LogError("Subsystem instances are not initialized.");
            return;
        }

        RenderTexture gridTexture = grid;    
        mNavierStokesManager.InputTexture = gridTexture;

        Debug.Log("Grid texture transferred to NavierStokes system.");
    }

    public void UpdateSinkObjects(List<GameObject> objects)
    {
        mSinkObjects = objects;
    }

    public bool GetFlowDirection(Vector3 worldPosition, out Vector2 output, eAgentSize size)
    {
        output = Vector2.zero;
        NavMeshData data;
        if(!mAgentData.TryGetValue(size, out data)) return false;

        return GetFlowDirection(worldPosition, out output, data);
    }

    public bool GetFlowDirection(Vector3 worldPosition, out Vector2 output, NavMeshData data)
    {
        RenderTexture texture;
        if (!mFlowFields.TryGetValue(data, out texture))
        {
            Debug.LogError("Flow-field texture is not set or could not be converted for CPU access.");
            output = Vector2.zero;
            return false;
        }

        float u = Mathf.InverseLerp(mWorldMin.x, mWorldMax.x, worldPosition.x);
        float v = Mathf.InverseLerp(mWorldMin.y, mWorldMax.y, worldPosition.y);

        int texX = Mathf.Clamp(Mathf.FloorToInt(u * texture.width), 0, texture.width - 1);
        int texY = Mathf.Clamp(Mathf.FloorToInt(v * texture.height), 0, texture.height - 1);

        Color pixelColor = ToTexture2D(texture).GetPixel(texX, texY);
        Vector2 direction = (new Vector2(pixelColor.r, pixelColor.g) - Vector2.one * 0.5f) * 2f;
        output = direction.normalized;
        return true;
    }

    private Texture2D ToTexture2D(RenderTexture source)
    {
        Texture2D output = new Texture2D(source.width,source.height,TextureFormat.RGB24,source.useMipMap);
        RenderTexture.active = source;
        output.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
        return output;
    }
}
