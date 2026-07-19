using UnityEngine;

public class BaseCore : MonoBehaviour, IStructureHpSource
{
    [Header("Health")]
    [SerializeField] private float maxHp = 200f;

    [Header("Combat")]
    [SerializeField] private Collider attackCollider;

    private float currentHp;
    private bool hasLoggedDestroyed;

    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public bool IsDestroyed => currentHp <= 0f;
    public Collider AttackCollider => attackCollider;
    public Transform HpAnchorTransform => transform;

    private void Awake()
    {
        maxHp = Mathf.Max(1f, maxHp);
        currentHp = maxHp;
        hasLoggedDestroyed = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHp = Mathf.Max(1f, maxHp);
    }
#endif

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDestroyed)
        {
            return;
        }

        float oldHp = currentHp;
        currentHp = Mathf.Clamp(currentHp - amount, 0f, maxHp);

        Debug.Log($"[BaseCore] Damaged: {amount}. HP: {oldHp:0.##} -> {currentHp:0.##}/{maxHp:0.##}", this);

        if (currentHp > 0f || hasLoggedDestroyed)
        {
            return;
        }

        hasLoggedDestroyed = true;
        Debug.Log("BaseCore destroyed - Game Over", this);
    }
}
