using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VectorFlowFieldCalculator
{
    private int mIterations;
    public int Iterations { get => mIterations; set => mIterations = value; }

    private ComputeShader mNavierStokes;
    public ComputeShader NavierStokes { get => mNavierStokes; set => mNavierStokes = value; }

    private Texture mInputTexture;
    public Texture InputTexture { get => mInputTexture; set => mInputTexture = value; }

    private RenderTexture mOutputTexture;
    public RenderTexture OutputTexture { get => mOutputTexture; set => mOutputTexture = value; }

    private Dictionary<NavMeshData, RenderTexture> mOutputTextures;

    // Private internal textures for simulation
    private RenderTexture mVelocityRT1, mVelocityRT2;
    private RenderTexture mPressureRT, mPressureRT2, mDivergenceRT;
    private int mKernelAdvect, mKernelForces, mKernelDivergence, mKernelPressure, mKernelProject, mKernelVisualize;

    /// <summary>
    /// Initializes the NavierStokes simulation by setting up textures and finding shader kernels.
    /// Must be called after InputTexture and NavierStokes are set.
    /// </summary>
    public void Initialize(Vector2Int gridResolution)
    {
        mOutputTextures = new Dictionary<NavMeshData, RenderTexture>();
        InitializeTextures(gridResolution);
        FindKernels();
    }

    /// <summary>
    /// Updates the simulation by performing a single simulation step.
    /// </summary>
    /// <param name="dt">Time delta for the simulation step.</param>
    public void UpdateSimulation(float dt,NavMeshData data)
    {
        mOutputTextures.TryAdd(data, CreateRenderTexture(mInputTexture.width, mInputTexture.height, RenderTextureFormat.ARGB32));
        RunSimulationStep(dt,data);
    }

    private void InitializeTextures(Vector2Int gridResolution)
    {
        // NOTE: Ensure mInputTexture is not null before accessing its width and height.
        int width = gridResolution.x;
        int height = gridResolution.y;

        mVelocityRT1 = CreateRenderTexture(width, height, RenderTextureFormat.RGFloat);
        mVelocityRT2 = CreateRenderTexture(width, height, RenderTextureFormat.RGFloat);
        mPressureRT = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);
        mPressureRT2 = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);
        mDivergenceRT = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);
        mOutputTexture = CreateRenderTexture(width, height, RenderTextureFormat.ARGB32);
    }

    private void FindKernels()
    {
        mKernelAdvect = mNavierStokes.FindKernel("Advect");
        mKernelForces = mNavierStokes.FindKernel("ApplyForces");
        mKernelDivergence = mNavierStokes.FindKernel("ComputeDivergence");
        mKernelPressure = mNavierStokes.FindKernel("SolvePressure");
        mKernelProject = mNavierStokes.FindKernel("Project");
        mKernelVisualize = mNavierStokes.FindKernel("Visualize");
    }

    private void RunSimulationStep(float dt, NavMeshData data)
    {
        mNavierStokes.SetVector("TexelSize", new Vector2(1f / mInputTexture.width, 1f / mInputTexture.height));
        mNavierStokes.SetFloat("DeltaTime", dt);

        // Advection
        SwapBuffers(ref mVelocityRT1, ref mVelocityRT2);
        mNavierStokes.SetTexture(mKernelAdvect, "Velocity", mVelocityRT1);
        mNavierStokes.SetTexture(mKernelAdvect, "PrevVelocity", mVelocityRT2);
        Dispatch(mKernelAdvect);

        // Apply forces
        mNavierStokes.SetTexture(mKernelForces, "InputTexture", mInputTexture);
        mNavierStokes.SetTexture(mKernelForces, "PrevVelocity", mVelocityRT2);
        mNavierStokes.SetTexture(mKernelForces, "Velocity", mVelocityRT1);
        Dispatch(mKernelForces);

        // Compute divergence
        mNavierStokes.SetTexture(mKernelDivergence, "Velocity", mVelocityRT1);
        mNavierStokes.SetTexture(mKernelDivergence, "Divergence", mDivergenceRT);
        Dispatch(mKernelDivergence);

        // Solve pressure iterations
        for (int i = 0; i < mIterations; i++)
        {
            mNavierStokes.SetTexture(mKernelPressure, "Pressure", mPressureRT);
            mNavierStokes.SetTexture(mKernelPressure, "PressureOut", mPressureRT2);
            mNavierStokes.SetTexture(mKernelPressure, "Divergence", mDivergenceRT);
            Dispatch(mKernelPressure);
            SwapBuffers(ref mPressureRT, ref mPressureRT2);
        }

        // Project velocity using the final pressure texture
        mNavierStokes.SetTexture(mKernelProject, "Velocity", mVelocityRT1);
        mNavierStokes.SetTexture(mKernelProject, "Pressure", mPressureRT);
        Dispatch(mKernelProject);

        // Visualize
        RenderTexture temp;
        if (mOutputTextures.TryGetValue(data, out temp))
        {
            mNavierStokes.SetTexture(mKernelVisualize, "Velocity", mVelocityRT1);
            mNavierStokes.SetTexture(mKernelVisualize, "OutputTexture", temp);
            Dispatch(mKernelVisualize);
        }
    }

    private RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, format);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void Dispatch(int kernel)
    {
        mNavierStokes.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
        int groupsX = Mathf.CeilToInt(mInputTexture.width / (float)x);
        int groupsY = Mathf.CeilToInt(mInputTexture.height / (float)y);
        mNavierStokes.Dispatch(kernel, groupsX, groupsY, 1);
    }

    private void SwapBuffers(ref RenderTexture a, ref RenderTexture b)
    {
        RenderTexture temp = a;
        a = b;
        b = temp;
    }

    /// <summary>
    /// Configures the simulation parameters.
    /// </summary>
    /// <param name="iterations">Number of pressure solve iterations.</param>
    public void ConfigureNavierStokes(int iterations)
    {
        mIterations = iterations;
        Debug.Log("NavierStokes simulation configured with " + mIterations + " iterations.");
    }

    public bool TryGetRenderTexture(NavMeshData data, out RenderTexture texture)
    {
        return mOutputTextures.TryGetValue(data, out texture);
    }
}
