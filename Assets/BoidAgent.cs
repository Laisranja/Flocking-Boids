using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class BoidAgent : MonoBehaviour
{
    [Header("References")]
    [HideInInspector] public FlockManager manager;
    [Header("Layer Setup")]
    public LayerMask boidLayer;

    [Header("Movement Limits")]
    public float maxSpeed = 6f;
    public float minSpeed = 3f;
    public float maxForce = 12f;
    public float rotationSpeed = 10f;

    [Header("Predator Avoidance")]
    public float predatorAvoidDistance = 8f;
    public float predatorAvoidWeight = 5f;

    [Header("Obstacle Avoidance")]
    public float obstacleCheckDistance = 4f;
    public LayerMask obstacleLayer;

    private Rigidbody rb;
    private Vector3 velocity;
    private Vector3 acceleration;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        float randomAngle = Random.Range(0f, 360f);
        velocity = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle)) * minSpeed;
    }

    void FixedUpdate()
    {
        if (manager == null) return;

        acceleration = Vector3.zero;

        Vector3 separationSum = Vector3.zero;
        Vector3 alignmentSum = Vector3.zero;
        Vector3 cohesionSum = Vector3.zero;
        int neighborCount = 0;
        int separationCount = 0;

        // FUGA DO PREDADOR
        if (Predator.Instance != null)
        {
            Vector3 toPredator = Predator.Instance.transform.position - transform.position;
            toPredator.y = 0f; // Manter no plano XZ
            float distToPredator = toPredator.magnitude;

            if (distToPredator < predatorAvoidDistance && distToPredator > 0f)
            {
                // Direção oposta ao predador
                Vector3 fleeDir = -toPredator.normalized;

                // Força inversamente proporcional à distância
                Vector3 fleeForce = Steer(fleeDir * maxSpeed) * (predatorAvoidWeight * (1f - distToPredator / predatorAvoidDistance));
                acceleration += fleeForce;
            }
        }

        // BUSCA VIA SPATIAL GRID
        Vector3 pos = transform.position;
        int currentCellX = Mathf.FloorToInt(pos.x / manager.cellSize);
        int currentCellZ = Mathf.FloorToInt(pos.z / manager.cellSize);

        float neighborRadiusSqr = manager.neighborRadius * manager.neighborRadius;
        float separationRadiusSqr = manager.separationRadius * manager.separationRadius;

        // Varre a célula atual e as 8 células vizinhas ao redor (3x3 grid no plano XZ)
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector3Int neighborCell = new Vector3Int(currentCellX + x, 0, currentCellZ + z);

                if (manager.spatialGrid.TryGetValue(neighborCell, out List<BoidAgent> cellAgents))
                {
                    foreach (var other in cellAgents)
                    {
                        if (other == null || other == this) continue;

                        Vector3 toOther = other.transform.position - transform.position;
                        toOther.y = 0f;
                        float dSqr = toOther.sqrMagnitude;

                        if (dSqr > 0 && dSqr < neighborRadiusSqr)
                        {
                            alignmentSum += other.velocity;
                            cohesionSum += other.transform.position;
                            neighborCount++;

                            if (dSqr < separationRadiusSqr)
                            {
                                float dist = Mathf.Sqrt(dSqr);
                                Vector3 repulseDir = (transform.position - other.transform.position).normalized;
                                separationSum += repulseDir / (dist * dist);
                                separationCount++;
                            }
                        }
                    }
                }
            }
        }

        Vector3 forceSeparation = Vector3.zero;
        Vector3 forceAlignment = Vector3.zero;
        Vector3 forceCohesion = Vector3.zero;

        if (separationCount > 0)
        {
            separationSum /= separationCount;
            forceSeparation = Steer(separationSum.normalized * maxSpeed) * manager.separationWeight;
        }

        if (neighborCount > 0)
        {
            alignmentSum /= neighborCount;
            forceAlignment = Steer(alignmentSum.normalized * maxSpeed) * manager.alignmentWeight;

            cohesionSum /= neighborCount;
            Vector3 cohesionDir = cohesionSum - transform.position;
            cohesionDir.y = 0f;
            forceCohesion = Steer(cohesionDir.normalized * maxSpeed) * manager.cohesionWeight;
        }

        acceleration += forceSeparation + forceAlignment + forceCohesion;

        // Desvio de Obstáculos
        Vector3 obstacleAvoidance = CalculateObstacleAvoidance();
        acceleration += obstacleAvoidance * manager.obstacleWeight;

        // Retorno ao Mundo
        Vector3 worldBoundaryForce = CalculateWorldBoundary();
        acceleration += worldBoundaryForce;

        // Segurança contra NaN
        if (float.IsNaN(acceleration.x) || float.IsNaN(acceleration.y) || float.IsNaN(acceleration.z))
        {
            acceleration = Vector3.zero;
        }

        acceleration = Vector3.ClampMagnitude(acceleration, maxForce);

        velocity += acceleration * Time.fixedDeltaTime;
        velocity.y = 0f;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        if (velocity.magnitude < minSpeed)
        {
            velocity = velocity.normalized * minSpeed;
        }

        rb.linearVelocity = velocity;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private Vector3 Steer(Vector3 targetVelocity)
    {
        Vector3 steer = targetVelocity - velocity;
        steer.y = 0f;
        return Vector3.ClampMagnitude(steer, maxForce);
    }

    private Vector3 CalculateObstacleAvoidance()
    {
        RaycastHit hit;
        Vector3 avoidForce = Vector3.zero;
        Vector3 forwardDir = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;

        if (Physics.SphereCast(transform.position, 0.75f, forwardDir, out hit, obstacleCheckDistance, obstacleLayer))
        {
            Vector3 reflectDir = Vector3.Reflect(forwardDir, hit.normal);
            reflectDir.y = 0f;
            avoidForce = Steer(reflectDir.normalized * maxSpeed) * 2f;
        }
        return avoidForce;
    }

    private Vector3 CalculateWorldBoundary()
    {
        if (manager == null) return Vector3.zero;

        Vector3 offset = transform.position - manager.worldCenter;
        offset.y = 0f;
        float distFromCenter = offset.magnitude;

        if (distFromCenter <= manager.worldRadius)
        {
            return Vector3.zero;
        }

        float excess = distFromCenter - manager.worldRadius;
        Vector3 pullBackDir = -offset.normalized;
        return pullBackDir * (excess * manager.worldBoundaryPullStrength);
    }
}