using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages GPU buffer allocation and disposal for SPH simulation
/// Implements proper resource management patterns to prevent memory leaks
/// </summary>
public class SPHBufferManager : IDisposable
{
    // Dictionary to track all allocated buffers for debugging and management
    private Dictionary<string, ComputeBuffer> _managedBuffers = new Dictionary<string, ComputeBuffer>();
    private bool _isDisposed = false;

    // Core SPH simulation buffers
    public ComputeBuffer PositionBuffer { get; private set; }
    public ComputeBuffer VelocityBuffer { get; private set; }
    public ComputeBuffer ForceBuffer { get; private set; }
    public ComputeBuffer DensityBuffer { get; private set; }
    public ComputeBuffer PressureBuffer { get; private set; }

    // Spatial hashing buffers
    public ComputeBuffer ParticleGridIndicesBuffer { get; private set; }
    public ComputeBuffer GridStartIndicesBuffer { get; private set; }
    public ComputeBuffer GridEndIndicesBuffer { get; private set; }
    public ComputeBuffer SortedParticleIndicesBuffer { get; private set; }

    // Debug and utility buffers
    public ComputeBuffer DebugColorBuffer { get; private set; }
    public ComputeBuffer NeighborCountBuffer { get; private set; }

    private int _maxParticles;
    private int _maxGridCells;

    /// <summary>
    /// Initialize all buffers needed for SPH simulation
    /// </summary>
    /// <param name="maxParticles">Maximum number of particles to support</param>
    /// <param name="maxGridCells">Maximum number of grid cells for spatial hashing</param>
    public void InitializeBuffers(int maxParticles, int maxGridCells)
    {
        // Always dispose existing buffers before creating new ones
        DisposeAllBuffers();

        _maxParticles = maxParticles;
        _maxGridCells = maxGridCells;

        try
        {
            // Core particle data buffers
            // Each particle needs position (float3), velocity (float3), etc.
            PositionBuffer = CreateBuffer("Positions", maxParticles, sizeof(float) * 3);
            VelocityBuffer = CreateBuffer("Velocities", maxParticles, sizeof(float) * 3);
            ForceBuffer = CreateBuffer("Forces", maxParticles, sizeof(float) * 3);
            DensityBuffer = CreateBuffer("Densities", maxParticles, sizeof(float));
            PressureBuffer = CreateBuffer("Pressures", maxParticles, sizeof(float));

            // Spatial hashing buffers
            // Grid indices: which grid cell each particle belongs to
            ParticleGridIndicesBuffer = CreateBuffer("ParticleGridIndices", maxParticles, sizeof(int));

            // Grid start/end: where each grid cell begins and ends in sorted particle list
            GridStartIndicesBuffer = CreateBuffer("GridStartIndices", maxGridCells, sizeof(int));
            GridEndIndicesBuffer = CreateBuffer("GridEndIndices", maxGridCells, sizeof(int));

            // Sorted particle indices for efficient neighbor finding
            SortedParticleIndicesBuffer = CreateBuffer("SortedParticleIndices", maxParticles, sizeof(int));

            // Debug and utility buffers
            DebugColorBuffer = CreateBuffer("DebugColors", maxParticles, sizeof(float) * 4); // RGBA
            NeighborCountBuffer = CreateBuffer("NeighborCounts", maxParticles, sizeof(int));

            // --- Add gridCellCounts buffer for spatial hashing debug/counting ---
            var gridCellCountsBuffer = CreateBuffer("gridCellCounts", maxGridCells, sizeof(uint));
            // Optionally clear to zero
            uint[] zeroCounts = new uint[maxGridCells];
            gridCellCountsBuffer.SetData(zeroCounts);

            Debug.Log($"SPH Buffers initialized successfully for {maxParticles} particles");
            LogMemoryUsage();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize SPH buffers: {e.Message}");
            DisposeAllBuffers(); // Clean up any partially created buffers
            throw;
        }
    }

    /// <summary>
    /// Creates a compute buffer with proper error handling and tracking
    /// </summary>
    private ComputeBuffer CreateBuffer(string name, int count, int stride)
    {
        if (count <= 0 || stride <= 0)
        {
            throw new ArgumentException($"Invalid buffer parameters for {name}: count={count}, stride={stride}");
        }

        try
        {
            var buffer = new ComputeBuffer(count, stride);
            _managedBuffers[name] = buffer;

            Debug.Log($"Created buffer '{name}': {count} elements × {stride} bytes = {count * stride} bytes");
            return buffer;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create buffer '{name}': {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Safely resize buffers if particle count changes
    /// </summary>
    public void ResizeBuffers(int newMaxParticles, int newMaxGridCells)
    {
        if (newMaxParticles == _maxParticles && newMaxGridCells == _maxGridCells)
            return; // No change needed

        Debug.Log($"Resizing buffers from {_maxParticles} to {newMaxParticles} particles");

        // Store any data that needs to be preserved (if needed)
        // For simplicity, we'll just recreate all buffers
        InitializeBuffers(newMaxParticles, newMaxGridCells);
    }

    /// <summary>
    /// Initialize particle data with default values
    /// </summary>
    public void InitializeParticleData(Vector3[] initialPositions, Vector3[] initialVelocities = null)
    {
        if (initialPositions == null)
            throw new ArgumentNullException(nameof(initialPositions));

        if (initialPositions.Length > _maxParticles)
            throw new ArgumentException($"Too many initial positions: {initialPositions.Length} > {_maxParticles}");

        // Convert Vector3 arrays to the format expected by compute shaders
        float[] positionData = new float[_maxParticles * 3];
        float[] velocityData = new float[_maxParticles * 3];
        float[] densityData = new float[_maxParticles];
        float[] pressureData = new float[_maxParticles];

        for (int i = 0; i < initialPositions.Length; i++)
        {
            // Position data
            positionData[i * 3] = initialPositions[i].x;
            positionData[i * 3 + 1] = initialPositions[i].y;
            positionData[i * 3 + 2] = initialPositions[i].z;

            // Velocity data
            if (initialVelocities != null && i < initialVelocities.Length)
            {
                velocityData[i * 3] = initialVelocities[i].x;
                velocityData[i * 3 + 1] = initialVelocities[i].y;
                velocityData[i * 3 + 2] = initialVelocities[i].z;
            }
            else
            {
                velocityData[i * 3] = 0f;
                velocityData[i * 3 + 1] = 0f;
                velocityData[i * 3 + 2] = 0f;
            }

            // Initialize density and pressure with reasonable defaults
            densityData[i] = 1000f; // Water density
            pressureData[i] = 0f;
        }

        // Upload data to GPU
        PositionBuffer.SetData(positionData);
        VelocityBuffer.SetData(velocityData);
        DensityBuffer.SetData(densityData);
        PressureBuffer.SetData(pressureData);

        // Initialize other buffers with zeros
        var zeroFloats = new float[_maxParticles * 3];
        var zeroInts = new int[_maxParticles];

        ForceBuffer.SetData(zeroFloats);
        ParticleGridIndicesBuffer.SetData(zeroInts);
        NeighborCountBuffer.SetData(zeroInts);

        Debug.Log($"Initialized {initialPositions.Length} particles with data");
    }

    /// <summary>
    /// Bind buffers to a compute shader for a specific kernel
    /// </summary>
    public void BindBuffersToShader(ComputeShader shader, int kernelIndex, string phase)
    {
        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        try
        {
            switch (phase.ToLower())
            {
                case "spatialhash":
                    shader.SetBuffer(kernelIndex, "positions", PositionBuffer);
                    shader.SetBuffer(kernelIndex, "particleGridIndices", ParticleGridIndicesBuffer);
                    if (_managedBuffers.ContainsKey("gridCellCounts"))
                        shader.SetBuffer(kernelIndex, "gridCellCounts", _managedBuffers["gridCellCounts"]);
                    if (DebugColorBuffer != null)
                        shader.SetBuffer(kernelIndex, "debugColors", DebugColorBuffer);
                    break;

                case "densitypressure":
                    shader.SetBuffer(kernelIndex, "positions", PositionBuffer);
                    shader.SetBuffer(kernelIndex, "densities", DensityBuffer);
                    shader.SetBuffer(kernelIndex, "pressures", PressureBuffer);
                    shader.SetBuffer(kernelIndex, "gridStartIndices", GridStartIndicesBuffer);
                    shader.SetBuffer(kernelIndex, "gridEndIndices", GridEndIndicesBuffer);
                    shader.SetBuffer(kernelIndex, "sortedParticleIndices", SortedParticleIndicesBuffer);
                    break;

                case "forces":
                    shader.SetBuffer(kernelIndex, "positions", PositionBuffer);
                    shader.SetBuffer(kernelIndex, "velocities", VelocityBuffer);
                    shader.SetBuffer(kernelIndex, "densities", DensityBuffer);
                    shader.SetBuffer(kernelIndex, "pressures", PressureBuffer);
                    shader.SetBuffer(kernelIndex, "forces", ForceBuffer);
                    shader.SetBuffer(kernelIndex, "gridStartIndices", GridStartIndicesBuffer);
                    shader.SetBuffer(kernelIndex, "gridEndIndices", GridEndIndicesBuffer);
                    shader.SetBuffer(kernelIndex, "sortedParticleIndices", SortedParticleIndicesBuffer);
                    break;

                case "integration":
                    shader.SetBuffer(kernelIndex, "positions", PositionBuffer);
                    shader.SetBuffer(kernelIndex, "velocities", VelocityBuffer);
                    shader.SetBuffer(kernelIndex, "forces", ForceBuffer);
                    shader.SetBuffer(kernelIndex, "densities", DensityBuffer);
                    shader.SetBuffer(kernelIndex, "pressures", PressureBuffer);
                    break;

                default:
                    Debug.LogWarning($"Unknown shader phase: {phase}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to bind buffers to shader for phase '{phase}': {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Read particle positions back from GPU for rendering or debugging
    /// </summary>
    public Vector3[] GetParticlePositions(int particleCount = -1)
    {
        if (PositionBuffer == null)
            throw new InvalidOperationException("Position buffer is not initialized");

        if (particleCount == -1)
            particleCount = _maxParticles;

        particleCount = Mathf.Min(particleCount, _maxParticles);

        var positionData = new float[particleCount * 3];
        PositionBuffer.GetData(positionData, 0, 0, particleCount * 3);

        var positions = new Vector3[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            positions[i] = new Vector3(
                positionData[i * 3],
                positionData[i * 3 + 1],
                positionData[i * 3 + 2]
            );
        }

        return positions;
    }

    /// <summary>
    /// Get debug information about buffer usage
    /// </summary>
    public void LogMemoryUsage()
    {
        long totalBytes = 0;
        foreach (var kvp in _managedBuffers)
        {
            var buffer = kvp.Value;
            long bufferBytes = buffer.count * buffer.stride;
            totalBytes += bufferBytes;
            Debug.Log($"Buffer '{kvp.Key}': {bufferBytes} bytes ({buffer.count} × {buffer.stride})");
        }
        Debug.Log($"Total GPU memory usage: {totalBytes} bytes ({totalBytes / (1024f * 1024f):F2} MB)");
    }

    /// <summary>
    /// Check if all buffers are properly initialized
    /// </summary>
    public bool AreBuffersValid()
    {
        return PositionBuffer != null &&
               VelocityBuffer != null &&
               ForceBuffer != null &&
               DensityBuffer != null &&
               PressureBuffer != null &&
               ParticleGridIndicesBuffer != null &&
               GridStartIndicesBuffer != null &&
               GridEndIndicesBuffer != null;
    }

    /// <summary>
    /// Dispose all managed buffers
    /// </summary>
    public void DisposeAllBuffers()
    {
        foreach (var kvp in _managedBuffers)
        {
            try
            {
                kvp.Value?.Dispose();
                Debug.Log($"Disposed buffer: {kvp.Key}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing buffer '{kvp.Key}': {e.Message}");
            }
        }

        _managedBuffers.Clear();

        // Clear individual buffer references
        PositionBuffer = null;
        VelocityBuffer = null;
        ForceBuffer = null;
        DensityBuffer = null;
        PressureBuffer = null;
        ParticleGridIndicesBuffer = null;
        GridStartIndicesBuffer = null;
        GridEndIndicesBuffer = null;
        SortedParticleIndicesBuffer = null;
        DebugColorBuffer = null;
        NeighborCountBuffer = null;
    }

    /// <summary>
    /// Implement IDisposable pattern
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            DisposeAllBuffers();
            _isDisposed = true;
            Debug.Log("SPHBufferManager disposed");
        }
    }

    /// <summary>
    /// Finalizer to catch missed disposal
    /// </summary>
    ~SPHBufferManager()
    {
        if (!_isDisposed)
        {
            Debug.LogWarning("SPHBufferManager was not properly disposed! Always call Dispose() or use using statements.");
            // Cannot dispose GPU resources from finalizer thread
            // This is just a warning to help developers catch mistakes
        }
    }
}