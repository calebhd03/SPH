using System;
using UnityEngine;
/// <summary>
/// Example usage class showing how to integrate the buffer manager
/// </summary>
public class SPHSimulation : MonoBehaviour
{
    [Header("Simulation Parameters")]
    public int maxParticles = 10000;
    public float particleMass = 0.02f; // Mass of each particle
    //public float smoothingLength = 0.1f; // Smoothing length for the SPH kernel
    public float restDensity = 1000f; // Rest density of the fluid
    public float gasConstant = 2000f; // Gas constant for the equation of state
    public float viscosityCoefficient = 0.01f; // Viscosity coefficient for the fluid
    public float timeStep = 0.01f; // Time step for simulation updates
    public Vector3 boundarySize = new Vector3(10, 10, 10);
    public float smoothingRadius = 0.5f; 

    [Header("Compute Shaders")]
    [SerializeField] private ComputeShader spatialHashShader;
    [SerializeField] private ComputeShader densityPressureShader;
    [SerializeField] private ComputeShader forceShader;
    [SerializeField] private ComputeShader integrationShader;

    public SPHBufferManager _bufferManager { get; private set; }
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
            var initialPositions = GenerateInitialParticlePositions(maxParticles);
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
            int threadsPerGroup = 64;
            int groupCount = Mathf.CeilToInt((float)_activeParticleCount / threadsPerGroup);

            Vector3Int gridDims = new Vector3Int(
                        Mathf.CeilToInt(boundarySize.x / smoothingRadius),
                        Mathf.CeilToInt(boundarySize.y / smoothingRadius),
                        Mathf.CeilToInt(boundarySize.z / smoothingRadius)
                    );

            // --- Spatial hash phase ---
            if (spatialHashShader != null)
            {
                if (spatialHashShader.HasKernel("AssignParticlesToGrid"))
                {
                    int assignKernel = spatialHashShader.FindKernel("AssignParticlesToGrid");
                    spatialHashShader.SetInt("numParticles", _activeParticleCount);
                    spatialHashShader.SetVector("boundsMin", Vector3.zero);
                    spatialHashShader.SetVector("boundsMax", boundarySize);
                    spatialHashShader.SetFloat("cellSize", smoothingRadius);

                    spatialHashShader.SetInts("gridDims", gridDims.x, gridDims.y, gridDims.z);
                    _bufferManager.BindBuffersToShader(spatialHashShader, assignKernel, "spatialhash");
                    spatialHashShader.Dispatch(assignKernel, Mathf.CeilToInt(_activeParticleCount / 64f), 1, 1);
                }
                if (spatialHashShader.HasKernel("DebugColorByGridCell"))
                {
                    int debugKernel = spatialHashShader.FindKernel("DebugColorByGridCell");
                    spatialHashShader.SetInt("numParticles", _activeParticleCount);
                    _bufferManager.BindBuffersToShader(spatialHashShader, debugKernel, "spatialhash");
                    spatialHashShader.Dispatch(debugKernel, Mathf.CeilToInt(_activeParticleCount / 64f), 1, 1);
                }
            }

            // Density and pressure phase
            if (densityPressureShader != null)
            {
                int kernelIndex = densityPressureShader.FindKernel("CalculateDensityPressure");

                // Set parameters
                densityPressureShader.SetFloat("smoothingRadius", smoothingRadius);
                densityPressureShader.SetFloat("smoothingRadius2", smoothingRadius * smoothingRadius);
                densityPressureShader.SetFloat("smoothingRadius6", Mathf.Pow(smoothingRadius, 6));
                densityPressureShader.SetFloat("smoothingRadius9", Mathf.Pow(smoothingRadius, 9));
                densityPressureShader.SetFloat("restDensity", restDensity);
                densityPressureShader.SetFloat("gasConstant", gasConstant);
                densityPressureShader.SetFloat("particleMass", particleMass);

                // Calculate Poly6 kernel coefficient
                float poly6Coeff = 315.0f / (64.0f * Mathf.PI * Mathf.Pow(smoothingRadius, 9));
                densityPressureShader.SetFloat("poly6Coefficient", poly6Coeff);

                // Set grid parameters
                densityPressureShader.SetInt("numParticles", _activeParticleCount);
                densityPressureShader.SetInts("gridDimensions", gridDims.x, gridDims.y, gridDims.z);
                densityPressureShader.SetFloat("cellSize", smoothingRadius);

                _bufferManager.BindBuffersToShader(densityPressureShader, kernelIndex, "densitypressure");
                densityPressureShader.Dispatch(kernelIndex, groupCount, 1, 1);
            }

            // Force calculation phase
            if (forceShader != null)
            {
                int kernelIndex = forceShader.FindKernel("CalculateForces");

                forceShader.SetFloat("smoothingRadius", smoothingRadius);
                forceShader.SetFloat("smoothingRadius2", smoothingRadius * smoothingRadius);
                
                float spikyGradCoeff = -45.0f / (Mathf.PI * Mathf.Pow(smoothingRadius, 6)); // Spiky kernel gradient coefficient:  -45/(PI * h^6)
                forceShader.SetFloat("spikyGradCoefficient", spikyGradCoeff);
                // Viscosity kernel laplacian coefficient: 45/(PI * h^6)
                float viscosityLaplacianCoeff = 45.0f / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
                forceShader.SetFloat("viscosityLaplacianCoefficient", viscosityLaplacianCoeff);
                forceShader.SetInt("numParticles", _activeParticleCount);
                forceShader.SetInts("gridDimensions", gridDims.x, gridDims.y, gridDims.z);
                forceShader.SetFloat("cellSize", smoothingRadius);
                _bufferManager.BindBuffersToShader(forceShader, kernelIndex, "forces");
                forceShader.Dispatch(kernelIndex, groupCount, 1, 1);
            }

            // Integration phase
            if (integrationShader != null)
            {
                int kernelIndex = integrationShader.FindKernel("Integrate");
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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, boundarySize);
    }
}