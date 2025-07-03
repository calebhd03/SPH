using UnityEngine;

[CreateAssetMenu(fileName = "SPHParameters", menuName = "SPH/Parameters", order = 1)]
public class SPHParameters : ScriptableObject
{
    public float particleMass = 0.02f; // Mass of each particle
    public float smoothingLength = 0.1f; // Smoothing length for the SPH kernel
    public float restDensity = 1000f; // Rest density of the fluid
    public float gasConstant = 2000f; // Gas constant for the equation of state
    public float viscosityCoefficient = 0.01f; // Viscosity coefficient for the fluid
    public float timeStep = 0.01f; // Time step for simulation updates
    public int maxParticles = 10000; // Maximum number of particles in the simulation

    // Add more parameters as needed for your SPH simulation
}
