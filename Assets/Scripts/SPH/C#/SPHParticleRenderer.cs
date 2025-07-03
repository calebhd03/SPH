using UnityEngine;

public class SPHParticleRenderer : MonoBehaviour
{
    public SPHSimulation simulation; // Reference to your simulation script
    public Material particleMaterial;
    public Mesh particleMesh;
    public float particleScale = 0.1f;

    public int particleToDisplay = 0; // If 0 display all particles, otherwise display only the specified particle

    public ColorDisplayMode colorDisplayMode = ColorDisplayMode.None;
    public enum ColorDisplayMode
    {
        None,
        Density,
        Pressure,
        Velocity
    }


    void OnRenderObject()
    {
        if (simulation == null || simulation.maxParticles == 0) return;
        var buffer = simulation._bufferManager.PositionBuffer;
        if (buffer == null) return;

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

        if (particleToDisplay == 0)
        {
            for (int i = 0; i < simulation.maxParticles; i += batchSize)
            {
                particleMaterial.color = GetParticleColor(particleToDisplay);
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
        }
        else
        {
            // Display only the specified particle
            if (particleToDisplay < simulation.maxParticles)
            {
                particleMaterial.color = GetParticleColor(particleToDisplay);
                Vector3 position = positions[particleToDisplay];
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * particleScale);
                Graphics.DrawMeshInstanced(
                    particleMesh,
                    0,
                    particleMaterial,
                    new Matrix4x4[] { matrix },
                    1,
                    null,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false,
                    0,
                    null,
                    UnityEngine.Rendering.LightProbeUsage.Off,
                    null
                );
            }
        }


        //Debug.Log($"Rendered {simulation.act} particles.");
    }

    Color GetParticleColor(int particleIndex)
    {
        Color color = Color.magenta; // Default color

        if (simulation == null || simulation._bufferManager == null)
        {
            return color;
        }

        switch (colorDisplayMode)
        {

            case ColorDisplayMode.Density:
                Vector3 densityVec = simulation._bufferManager.GetParticleDensity(particleIndex);
                // Assuming density is stored in x, and you want to map it to grayscale (black = dense, white = not dense)
                float density = densityVec.x;

                // You may want to define minDensity and maxDensity for normalization
                float minDensity = 0;
                float maxDensity = 1;

                // Clamp and invert: black (0) for maxDensity, white (1) for minDensity
                float t = Mathf.InverseLerp(maxDensity, minDensity, density);
                color = Color.Lerp(Color.black, Color.white, t);
                break;
            case ColorDisplayMode.Pressure:
                simulation._bufferManager.GetParticlePressure(particleIndex);

                Vector3 pressureVec = simulation._bufferManager.GetParticleVelocity(particleIndex);
                // Assuming velocity is stored in x, and you want to map it to grayscale (black = dense, white = not dense)
                float pressure = pressureVec.x;

                // You may want to define minvelocity and maxvelocity for normalization
                float minPressure = 0;
                float maxPressure = 1;

                // Clamp and invert: black (0) for maxvelocity, white (1) for minvelocity
                float pressureLerp = Mathf.InverseLerp(maxPressure, minPressure, pressure);
                color = Color.Lerp(Color.blue, Color.green, pressureLerp);
                break;
            case ColorDisplayMode.Velocity:
                simulation._bufferManager.GetParticleVelocity(particleIndex);

                Vector3 velocityVec = simulation._bufferManager.GetParticleVelocity(particleIndex);
                // Assuming velocity is stored in x, and you want to map it to grayscale (black = dense, white = not dense)
                float velocity = velocityVec.x;

                // You may want to define minvelocity and maxvelocity for normalization
                float minVelocity = 0;
                float maxVelocity = 1;

                // Clamp and invert: black (0) for maxvelocity, white (1) for minvelocity
                float colorLerp = Mathf.InverseLerp(maxVelocity, minVelocity, velocity);
                color = Color.Lerp(Color.red, Color.green, colorLerp);
                break;
            default:
                color = Color.white; // Default color if no mode is selected
                break;
        }

        return color;
    }
}