using UnityEngine;

public class Predator : MonoBehaviour
{
    public static Predator Instance;

    [Header("Movimento do Predador")]
    public float speed = 5f;
    public float patrolRadius = 20f;

    private Vector3 targetPosition;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GetNewTarget();
    }

    void GetNewTarget()
    {
        Vector2 randomPoint = Random.insideUnitCircle * patrolRadius;
        targetPosition = new Vector3(randomPoint.x, 0f, randomPoint.y);
    }

}