using UnityEngine;
using System.Collections.Generic;

namespace MipmapPathfinding
{
    public class ResolutionBiasController : MonoBehaviour
    {
        // References to other components
        [SerializeField] private MipmapGenerator mipmapGenerator;
        [SerializeField] private ComputeShader biasShader;
        [SerializeField] private ComputeShader JunctionDetector;

        // Junction detection parameters
        [Header("Junction Detection")]
        [SerializeField] private bool autoDetectJunctions = true;
        [SerializeField] private float junctionDetectionThreshold = 0.5f;
        [SerializeField] private int maxJunctions = 50;
        
        // Target bias parameters
        [Header("Target Bias")]
        [SerializeField] private Transform[] targets;
        [SerializeField] private float targetBiasStrength = 2.0f;
        
        // Bias parameters
        [Header("Bias Settings")]
        [SerializeField] private float biasRadius = 10.0f;
        [Range(0, 4)]
        [SerializeField] private float maxBiasStrength = 4.0f;
        [SerializeField] private AnimationCurve biasRadiusFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        
        // Output texture
        private RenderTexture biasTexture;
        
        // Internal storage
        private List<Vector2> junctionPoints = new List<Vector2>();
        private List<float> junctionImportance = new List<float>();
        private bool isDirty = true;
        
        // Properties
        public RenderTexture BiasTexture => biasTexture;
        
        private void Awake()
        {
            if (mipmapGenerator == null)
                mipmapGenerator = GetComponent<MipmapGenerator>();
                
            // Load the compute shader
            if(!biasShader) biasShader = Resources.Load<ComputeShader>("BiasGenerator");
            
            // Initialize on start - uncommented to ensure proper initialization
            Initialize();
        }
        
        public void Initialize()
        {
            Debug.Log("ResolutionBiasController: Initializing...");
            
            // Ensure MipmapGenerator is initialized first
            if (mipmapGenerator == null)
            {
                Debug.LogError("ResolutionBiasController: MipmapGenerator reference is missing!");
                return;
            }

            // Check if MipmapGenerator is initialized
            if (!mipmapGenerator.IsInitialized())
            {
                Debug.Log("ResolutionBiasController: MipmapGenerator not initialized yet, initializing it now...");
                mipmapGenerator.Initialize();
                
                // Wait a frame to ensure initialization completes
                StartCoroutine(DelayedInitialization());
                return;
            }

            // Force initialization of MipmapGenerator if needed - use a safer check
            RenderTexture testTexture = null;
            try
            {
                // Attempt to get level 0 - this will trigger auto-initialization if needed
                testTexture = mipmapGenerator.GetMipmapLevel(0);

                if (testTexture == null)
                {
                    Debug.LogError("ResolutionBiasController: Failed to get mipmap level 0 after initialization attempt");
                    // Try again after a delay
                    StartCoroutine(DelayedInitialization());
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ResolutionBiasController: Error during MipmapGenerator initialization: {e.Message}");
                // Try again after a delay
                StartCoroutine(DelayedInitialization());
                return;
            }

            // Create bias texture at half the resolution of base mipmap level
            int width = mipmapGenerator.GetBaseWidth() / 2;
            int height = mipmapGenerator.GetBaseHeight() / 2;
            
            // Create texture with R8 format (single channel is sufficient for bias)
            biasTexture = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
            biasTexture.enableRandomWrite = true;
            biasTexture.Create();
            
            if (autoDetectJunctions)
                DetectJunctions();
                
            GenerateBiasTexture();
        }
        
        public void DetectJunctions()
        {
            junctionPoints.Clear();
            junctionImportance.Clear();
            
            // Get the base navigation texture
            RenderTexture navTexture = mipmapGenerator.GetMipmapLevel(0);
            Bounds navBounds = mipmapGenerator.GetNavigationBounds();

            // Load junction detection compute shader
            //ComputeShader junctionDetectionShader = Resources.Load<ComputeShader>("JunctionDetector");
            ComputeShader junctionDetectionShader = JunctionDetector;
            if (junctionDetectionShader == null)
            {
                Debug.LogError("JunctionDetector compute shader not found in Resources folder.");
                return;
            }
            
            // Create texture to store junction candidates
            int width = navTexture.width;
            int height = navTexture.height;
            RenderTexture junctionCandidatesTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            junctionCandidatesTexture.enableRandomWrite = true;
            junctionCandidatesTexture.Create();
            
            // Step 1: Detect junction candidates using the compute shader
            int kernelDetect = junctionDetectionShader.FindKernel("DetectJunctions");
            junctionDetectionShader.SetTexture(kernelDetect, "NavigationTexture", navTexture);
            junctionDetectionShader.SetTexture(kernelDetect, "JunctionCandidates", junctionCandidatesTexture);
            junctionDetectionShader.SetFloat("JunctionThreshold", junctionDetectionThreshold);
            
            int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
            junctionDetectionShader.Dispatch(kernelDetect, threadGroupsX, threadGroupsY, 1);
            
            // Step 2: Read back junction candidate data
            RenderTexture.active = junctionCandidatesTexture;
            Texture2D junctionReadback = new Texture2D(width, height, TextureFormat.RFloat, false);
            junctionReadback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            junctionReadback.Apply();
            
            // Step 3: Analyze and filter junction candidates
            List<Vector2Int> localJunctionPoints = new List<Vector2Int>();
            List<float> localJunctionScores = new List<float>();
            
            // Scan the texture for junction points
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float score = junctionReadback.GetPixel(x, y).r;
                    
                    // Only consider points above threshold
                    if (score > junctionDetectionThreshold)
                    {
                        // Check if it's a local maximum
                        bool isLocalMax = true;
                        
                        // Check 3x3 neighborhood
                        for (int ny = -1; ny <= 1 && isLocalMax; ny++)
                        {
                            for (int nx = -1; nx <= 1; nx++)
                            {
                                if (nx == 0 && ny == 0) continue;
                                
                                float neighborScore = junctionReadback.GetPixel(x + nx, y + ny).r;
                                if (neighborScore > score)
                                {
                                    isLocalMax = false;
                                    break;
                                }
                            }
                        }
                        
                        if (isLocalMax)
                        {
                            localJunctionPoints.Add(new Vector2Int(x, y));
                            localJunctionScores.Add(score);
                        }
                    }
                }
            }
            
            // Step 4: Sort by importance and limit to maxJunctions
            int[] sortedIndices = new int[localJunctionScores.Count];
            for (int i = 0; i < sortedIndices.Length; i++)
            {
                sortedIndices[i] = i;
            }
            
            // Sort indices by score (descending)
            System.Array.Sort(sortedIndices, (a, b) => localJunctionScores[b].CompareTo(localJunctionScores[a]));
            
            // Take top maxJunctions
            int count = Mathf.Min(maxJunctions, localJunctionPoints.Count);
            
            for (int i = 0; i < count; i++)
            {
                int idx = sortedIndices[i];
                Vector2Int texCoord = localJunctionPoints[idx];
                float importance = localJunctionScores[idx];
                
                // Convert texture coordinates to world space
                float u = texCoord.x / (float)(width - 1);
                float v = texCoord.y / (float)(height - 1);
                
                float worldX = Mathf.Lerp(navBounds.min.x, navBounds.max.x, u);
                float worldZ = Mathf.Lerp(navBounds.min.z, navBounds.max.z, v);
                
                junctionPoints.Add(new Vector2(worldX, worldZ));
                junctionImportance.Add(importance);
            }
            
            // Clean up temporary resources
            Destroy(junctionReadback);
            junctionCandidatesTexture.Release();
            Destroy(junctionCandidatesTexture);
            
            isDirty = true;
            
            Debug.Log($"Detected {junctionPoints.Count} junction points.");
        }
        
        public void SetTargets(Transform[] newTargets)
        {
            targets = newTargets;
            isDirty = true;
        }
        
        public void ActivateTarget(Transform target)
        {
            // Find the target in the array and mark as active
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == target)
                {
                    // Target is now active - mark dirty to regenerate
                    isDirty = true;
                    break;
                }
            }
        }
        
        private void Update()
        {
            // Check if we need to update the bias texture
            if (isDirty)
            {
                if(biasTexture)
                {
                    GenerateBiasTexture();
                    isDirty = false;
                }
            }
        }
        
        public void GenerateBiasTexture()
        {
            // Prepare the bias texture generation using the compute shader
            int kernel = biasShader.FindKernel("GenerateBias");
            
            // Set texture dimensions and settings
            biasShader.SetTexture(kernel, "BiasOutput", biasTexture);
            biasShader.SetInt("TextureWidth", biasTexture.width);
            biasShader.SetInt("TextureHeight", biasTexture.height);
            
            // Set navigation bounds
            Bounds navBounds = mipmapGenerator.GetNavigationBounds();
            biasShader.SetVector("BoundsMin", new Vector4(navBounds.min.x, navBounds.min.z, 0, 0));
            biasShader.SetVector("BoundsMax", new Vector4(navBounds.max.x, navBounds.max.z, 0, 0));
            
            // Set junctions
            ComputeBuffer junctionBuffer = null;
            ComputeBuffer importanceBuffer = null;
            
            try
            {
                // Create buffers regardless of junction count (use dummy buffer if empty)
                junctionBuffer = junctionPoints.Count > 0 
                    ? new ComputeBuffer(junctionPoints.Count, sizeof(float) * 2) 
                    : new ComputeBuffer(1, sizeof(float) * 2); // Dummy buffer with one element

                importanceBuffer = junctionImportance.Count > 0
                    ? new ComputeBuffer(junctionImportance.Count, sizeof(float))
                    : new ComputeBuffer(1, sizeof(float)); // Dummy buffer with one element
                
                if (junctionPoints.Count > 0)
                {
                    // Set real data
                    junctionBuffer.SetData(junctionPoints.ToArray());
                    importanceBuffer.SetData(junctionImportance.ToArray());
                }
                else
                {
                    // Set dummy data
                    junctionBuffer.SetData(new Vector2[] { Vector2.zero });
                    importanceBuffer.SetData(new float[] { 0f });
                }
                
                // Always set the buffers regardless of junctionPoints.Count
                biasShader.SetBuffer(kernel, "Junctions", junctionBuffer);
                biasShader.SetBuffer(kernel, "JunctionImportance", importanceBuffer);
                biasShader.SetInt("JunctionCount", junctionPoints.Count);
                
                // Set targets
                List<Vector4> activeTargets = new List<Vector4>();
                foreach (Transform target in targets)
                {
                    if (target != null && target.gameObject.activeInHierarchy)
                    {
                        // Pack position and bias strength into Vector4
                        activeTargets.Add(new Vector4(
                            target.position.x,
                            target.position.z,
                            targetBiasStrength,
                            0
                        ));
                    }
                }
                
                ComputeBuffer targetBuffer = null;
                
                try
                {
                    if (activeTargets.Count > 0)
                    {
                        targetBuffer = new ComputeBuffer(activeTargets.Count, sizeof(float) * 4);
                        targetBuffer.SetData(activeTargets.ToArray());
                        
                        biasShader.SetBuffer(kernel, "Targets", targetBuffer);
                        biasShader.SetInt("TargetCount", activeTargets.Count);
                    }
                    else
                    {
                        biasShader.SetInt("TargetCount", 0);
                    }
                    
                    // Set bias parameters
                    biasShader.SetFloat("BiasRadius", biasRadius);
                    biasShader.SetFloat("MaxBiasStrength", maxBiasStrength);
                    
                    // Convert falloff curve to texture
                    Texture2D falloffTexture = new Texture2D(256, 1, TextureFormat.RFloat, false);
                    for (int i = 0; i < 256; i++)
                    {
                        float t = i / 255f;
                        falloffTexture.SetPixel(i, 0, new Color(biasRadiusFalloff.Evaluate(t), 0, 0));
                    }
                    falloffTexture.Apply();
                    
                    biasShader.SetTexture(kernel, "FalloffCurve", falloffTexture);
                    
                    // Dispatch the shader
                    int threadGroupsX = Mathf.CeilToInt(biasTexture.width / 8.0f);
                    int threadGroupsY = Mathf.CeilToInt(biasTexture.height / 8.0f);
                    biasShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
                    
                    Destroy(falloffTexture);
                }
                finally
                {
                    if (targetBuffer != null)
                        targetBuffer.Release();
                }
            }
            finally
            {
                if (junctionBuffer != null)
                    junctionBuffer.Release();
                    
                if (importanceBuffer != null)
                    importanceBuffer.Release();
            }
        }
        
        public float SampleBias(Vector3 worldPosition)
        {
            // Check if biasTexture is initialized
            if (biasTexture == null || !biasTexture.IsCreated())
            {
                Debug.LogWarning("SampleBias called with null or uninitialized biasTexture. Reinitializing...");
                Initialize();
                
                // If still null after initialization, return default bias
                if (biasTexture == null || !biasTexture.IsCreated())
                {
                    Debug.LogError("Failed to initialize biasTexture. Returning default bias value.");
                    return maxBiasStrength * 0.5f; // Return a reasonable default
                }
            }
            
            // Sample the bias texture at the given world position
            // Convert world position to UV coordinates
            Bounds navBounds = mipmapGenerator.GetNavigationBounds();
            
            float u = Mathf.InverseLerp(navBounds.min.x, navBounds.max.x, worldPosition.x);
            float v = Mathf.InverseLerp(navBounds.min.z, navBounds.max.z, worldPosition.z);
            
            // Clamp to texture bounds
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
            
            // Read back texture data at this position
            // NOTE: This method is not optimal for performance - in a real implementation
            // you'd likely want to sample this in a compute shader or use a compute buffer
            RenderTexture.active = biasTexture;
            Texture2D tempTexture = new Texture2D(1, 1, TextureFormat.R8, false);
            tempTexture.ReadPixels(new Rect(u * biasTexture.width, v * biasTexture.height, 1, 1), 0, 0);
            tempTexture.Apply();
            
            Color pixelColor = tempTexture.GetPixel(0, 0);
            float biasValue = pixelColor.r;
            
            Destroy(tempTexture);
            RenderTexture.active = null;
            
            Debug.Log($"SampleBias at ({worldPosition}): UV=({u},{v}), value={biasValue}, final={biasValue * maxBiasStrength}");
            return biasValue * maxBiasStrength;
        }
        
        private void OnDestroy()
        {
            // Clean up resources
            if (biasTexture != null)
            {
                biasTexture.Release();
                Destroy(biasTexture);
            }
        }
        
        /// <summary>
        /// Coroutine to retry initialization after a short delay
        /// </summary>
        private System.Collections.IEnumerator DelayedInitialization()
        {
            Debug.Log("ResolutionBiasController: Delaying initialization to ensure MipmapGenerator is ready...");
            
            // Wait for a short delay (0.5 seconds)
            yield return new WaitForSeconds(0.5f);
            
            // Try initialization again
            Debug.Log("ResolutionBiasController: Retrying initialization after delay...");
            Initialize();
        }
    }
}
