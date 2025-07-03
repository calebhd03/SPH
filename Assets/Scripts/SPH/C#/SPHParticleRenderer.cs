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

        // Vector3[] positions = simulation._bufferManager.GetParticlePositions();
        // for (int i=0;  i<positions.Length; i++)
        // {
        //     Vector3 pos = positions[i];
        //     Debug.Log($"Particle {i}: Position {pos}");
        // }
        particleMaterial.SetBuffer("_Positions", buffer);
        particleMaterial.SetFloat("_ParticleScale", particleScale);

        // Draw all particles as instanced meshes
        // Prepare matrices for instancing (for demonstration, identity matrices)
        Matrix4x4[] matrices = new Matrix4x4[simulation.maxParticles];
        Vector3[] positions = simulation._bufferManager.GetParticlePositions();
        for (int i = 0; i < simulation.maxParticles; i++)
        {
            matrices[i] = Matrix4x4.TRS(positions[i], Quaternion.identity, Vector3.one * particleScale);
        }
        int batchSize = 1023; // Unity's limit per batch
        for (int i = 0; i < simulation.maxParticles; i += batchSize)
        {
            int count = Mathf.Min(batchSize, simulation.maxParticles - i);
            Graphics.DrawMeshInstanced(
                particleMesh,
                0,
                particleMaterial,
                matrices,
                count,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                0,
                null,
                UnityEngine.Rendering.LightProbeUsage.Off,
                null
            );
        }

        //Debug.Log($"Rendered {simulation.act} particles.");
    }
}