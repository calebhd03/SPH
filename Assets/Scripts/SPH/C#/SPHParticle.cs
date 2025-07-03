using Unity.Mathematics;

public class SPHParticle
{
    public float3 position; // Position of the particle
    public float3 velocity; // Velocity of the particle
    public float density; // Density of the particle
    public float pressure; // Pressure of the particle
    public float mass; // Mass of the particle
    public float radius; // Radius of the particle

    public SPHParticle(float3 position, float mass, float radius)
    {
        this.position = position;
        this.mass = mass;
        this.radius = radius;
        this.density = 0f;
        this.pressure = 0f;
        this.velocity = float3.zero;
    }
}
