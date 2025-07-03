using UnityEngine;

public class SPHParticleRenderer : MonoBehaviour
{
    public SPHSimulation simulation; // Reference to your simulation script
    public Material particleMaterial;
    public Mesh particleMesh;
    public float particleScale = 0.1f;

    void OnRenderObject()
    {
        if (simulation == null || simulation.maxParticles == 0) return;
        var buffer = simulation._bufferManager.PositionBuffer;
        if (buffer == null) return;

        particleMaterial.SetBuffer("_Positions", buffer);
        particleMaterial.SetFloat("_ParticleScale", particleScale);

        // Draw all particles as instanced meshes
        Graphics.DrawMeshInstancedProcedural(
            particleMesh,
            0,
            particleMaterial,
            new Bounds(Vector3.zero, simulation.boundarySize * 2f),
            simulation.maxParticles
        );
    }
}