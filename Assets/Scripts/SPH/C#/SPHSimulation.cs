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

            // Density and pressure phase
            if (densityPressureShader != null)
            {
                int kernelIndex = densityPressureShader.FindKernel("CalculateDensityPressure");
                
                // Set parameters
                float smoothingRadius = 0.5f;
                densityPressureShader.SetFloat("smoothingRadius", smoothingRadius);
                densityPressureShader.SetFloat("smoothingRadius2", smoothingRadius * smoothingRadius);
                densityPressureShader.SetFloat("smoothingRadius6", Mathf.Pow(smoothingRadius, 6));
                densityPressureShader.SetFloat("smoothingRadius9", Mathf.Pow(smoothingRadius, 9));
                
                // Calculate Poly6 kernel coefficient
                float poly6Coeff = 315.0f / (64.0f * Mathf.PI * Mathf.Pow(smoothingRadius, 9));
                densityPressureShader.SetFloat("poly6Coefficient", poly6Coeff);
                
                // Set grid parameters
                densityPressureShader.SetInt("numParticles", _activeParticleCount);
                Vector3Int gridDims = new Vector3Int(
                    Mathf.CeilToInt(boundarySize.x / smoothingRadius),
                    Mathf.CeilToInt(boundarySize.y / smoothingRadius),
                    Mathf.CeilToInt(boundarySize.z / smoothingRadius)
                );
                densityPressureShader.SetInts("gridDimensions", gridDims.x, gridDims.y, gridDims.z);
                densityPressureShader.SetFloat("cellSize", smoothingRadius);
                
                _bufferManager.BindBuffersToShader(densityPressureShader, kernelIndex, "densitypressure");
                densityPressureShader.Dispatch(kernelIndex, groupCount, 1, 1);
            }

            // Force calculation phase
            if (forceShader != null)
            {
                int kernelIndex = forceShader.FindKernel("CalculateForces");
                float smoothingRadius = 0.5f;
                forceShader.SetFloat("smoothingRadius", smoothingRadius);
                forceShader.SetFloat("smoothingRadius2", smoothingRadius * smoothingRadius);
                // Spiky kernel gradient coefficient:  -45/(PI * h^6)
                float spikyGradCoeff = -45.0f / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
                forceShader.SetFloat("spikyGradCoefficient", spikyGradCoeff);
                // Viscosity kernel laplacian coefficient: 45/(PI * h^6)
                float viscosityLaplacianCoeff = 45.0f / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
                forceShader.SetFloat("viscosityLaplacianCoefficient", viscosityLaplacianCoeff);
                forceShader.SetInt("numParticles", _activeParticleCount);
                Vector3Int gridDims = new Vector3Int(
                    Mathf.CeilToInt(boundarySize.x / smoothingRadius),
                    Mathf.CeilToInt(boundarySize.y / smoothingRadius),
                    Mathf.CeilToInt(boundarySize.z / smoothingRadius)
                );
                forceShader.SetInts("gridDimensions", gridDims.x, gridDims.y, gridDims.z);
                forceShader.SetFloat("cellSize", smoothingRadius);
                _bufferManager.BindBuffersToShader(forceShader, kernelIndex, "forces");
                forceShader.Dispatch(kernelIndex, groupCount, 1, 1);
            }

            // Integration phase
            if (integrationShader != null)
            {
                int kernelIndex = integrationShader.FindKernel("Integrate");
                float timeStep = 0.01f; // You can expose this as a parameter
                integrationShader.SetFloat("timeStep", timeStep);
                integrationShader.SetInt("numParticles", _activeParticleCount);
                Vector3 boundsMin = Vector3.zero;
                Vector3 boundsMax = boundarySize;
                integrationShader.SetFloats("boundsMin", boundsMin.x, boundsMin.y, boundsMin.z);
                integrationShader.SetFloats("boundsMax", boundsMax.x, boundsMax.y, boundsMax.z);
                _bufferManager.BindBuffersToShader(integrationShader, kernelIndex, "integration");
                integrationShader.Dispatch(kernelIndex, groupCount, 1, 1);
            }

            // Add other simulation phases here...
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