using System;
using UnityEngine;
/// <summary>
/// Example usage class showing how to integrate the buffer manager
/// </summary>
public class SPHSimulation : MonoBehaviour
{
    [Header("Simulation Parameters")]
    [SerializeField] private int maxParticles = 10000;
    [SerializeField] private Vector3 boundarySize = new Vector3(10, 10, 10);

    [Header("Compute Shaders")]
    [SerializeField] private ComputeShader spatialHashShader;
    [SerializeField] private ComputeShader densityPressureShader;
    [SerializeField] private ComputeShader forceShader;
    [SerializeField] private ComputeShader integrationShader;

    private SPHBufferManager _bufferManager;
    private int _activeParticleCount;

    private void Start()
    {
        InitializeSimulation();
    }

    private void InitializeSimulation()
    {
        try
        {
            // Create and initialize buffer manager
            _bufferManager = new SPHBufferManager();

            // Calculate max grid cells based on boundary size and smoothing radius
            float smoothingRadius = 0.5f;
            int gridCellsX = Mathf.CeilToInt(boundarySize.x / smoothingRadius);
            int gridCellsY = Mathf.CeilToInt(boundarySize.y / smoothingRadius);
            int gridCellsZ = Mathf.CeilToInt(boundarySize.z / smoothingRadius);
            int maxGridCells = gridCellsX * gridCellsY * gridCellsZ;

            _bufferManager.InitializeBuffers(maxParticles, maxGridCells);

            // Create initial particle positions (simple grid arrangement)
            var initialPositions = GenerateInitialParticlePositions(1000); // Start with 1000 particles
            _bufferManager.InitializeParticleData(initialPositions);
            _activeParticleCount = initialPositions.Length;

            Debug.Log($"SPH Simulation initialized with {_activeParticleCount} particles");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize SPH simulation: {e.Message}");
            enabled = false;
        }
    }

    private Vector3[] GenerateInitialParticlePositions(int count)
    {
        var positions = new Vector3[count];
        int particlesPerSide = Mathf.CeilToInt(Mathf.Pow(count, 1f / 3f));
        float spacing = 0.4f;

        int index = 0;
        for (int x = 0; x < particlesPerSide && index < count; x++)
        {
            for (int y = 0; y < particlesPerSide && index < count; y++)
            {
                for (int z = 0; z < particlesPerSide && index < count; z++)
                {
                    positions[index] = new Vector3(
                        x * spacing - (particlesPerSide * spacing * 0.5f),
                        y * spacing + 2f,
                        z * spacing - (particlesPerSide * spacing * 0.5f)
                    );
                    index++;
                }
            }
        }

        return positions;
    }

    private void Update()
    {
        if (_bufferManager == null || !_bufferManager.AreBuffersValid())
            return;

        // Run simulation step
        RunSimulationStep();
    }

    private void RunSimulationStep()
    {
        try
        {
            // Example of how to use the buffer manager in your simulation loop
            int threadsPerGroup = 64;
            int groupCount = Mathf.CeilToInt((float)_activeParticleCount / threadsPerGroup);

            // Spatial hash phase
            if (spatialHashShader != null)
            {
                int kernelIndex = spatialHashShader.FindKernel("CSMain");
                _bufferManager.BindBuffersToShader(spatialHashShader, kernelIndex, "spatialhash");
                spatialHashShader.Dispatch(kernelIndex, groupCount, 1, 1);
            }

            // Add other simulation phases here...
            // This is just an example of how to use the buffer manager
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in simulation step: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        // CRITICAL: Always dispose of the buffer manager
        _bufferManager?.Dispose();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Handle application pause/resume
        if (pauseStatus)
        {
            // Optionally pause simulation
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Handle application focus changes
        if (!hasFocus)
        {
            // Optionally pause simulation when app loses focus
        }
    }
}