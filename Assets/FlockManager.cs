using UnityEngine;
using System.Collections.Generic;

public class FlockManager : MonoBehaviour
{
    public static FlockManager Instance;

    [Header("Spawning (500+ Boids)")]
    public GameObject boidPrefab;
    public int boidCount = 500;
    public float spawnRadius = 25f;
    public LayerMask boidLayer;

    [Header("Spatial Grid")]
    public float cellSize = 2f;
    [HideInInspector] public Dictionary<Vector3Int, List<BoidAgent>> spatialGrid = new Dictionary<Vector3Int, List<BoidAgent>>();
    [HideInInspector] public List<BoidAgent> allBoids = new List<BoidAgent>();

    [Header("Flocking Weights")]
    public float neighborRadius = 5f;
    public float separationRadius = 1.5f;
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float obstacleWeight = 3.0f;

    [Header("World Limits")]
    public Vector3 worldCenter = Vector3.zero;
    public float worldRadius = 60f;
    public float worldBoundaryPullStrength = 2.0f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SpawnBoids();
    }

    void SpawnBoids()
    {
        if (boidPrefab == null) return;

        for (int i = 0; i < boidCount; i++)
        {
            Vector3 randomPos = worldCenter + Random.insideUnitSphere * spawnRadius;
            randomPos.y = 0f;

            GameObject boidObj = Instantiate(boidPrefab, randomPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            BoidAgent agent = boidObj.GetComponent<BoidAgent>();
            if (agent != null)
            {
                agent.manager = this;
                agent.boidLayer = boidLayer;
                allBoids.Add(agent);
            }
        }
    }

    void FixedUpdate()
    {
        RebuildSpatialGrid();
    }

    void RebuildSpatialGrid()
    {
        spatialGrid.Clear();

        foreach (var agent in allBoids)
        {
            if (agent == null) continue;

            Vector3 pos = agent.transform.position;
            Vector3Int cellKey = new Vector3Int(
                Mathf.FloorToInt(pos.x / cellSize),
                0,
                Mathf.FloorToInt(pos.z / cellSize)
            );

            if (!spatialGrid.TryGetValue(cellKey, out var list))
            {
                list = new List<BoidAgent>();
                spatialGrid[cellKey] = list;
            }
            list.Add(agent);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(worldCenter, worldRadius);
    }
}