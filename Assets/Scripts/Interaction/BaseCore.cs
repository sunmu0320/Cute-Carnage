using System;
using UnityEngine;

public class BaseCore : MonoBehaviour, IStructureHpSource
{
    [Header("Health")]
    [SerializeField] private float maxHp = 200f;

    [Header("Combat")]
    [SerializeField] private Collider attackCollider;

    private float currentHp;
    private bool hasLoggedDestroyed;
    private bool hasNotifiedDestroyed;

    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public bool IsDestroyed => currentHp <= 0f;
    public Collider AttackCollider => attackCollider;
    public Transform HpAnchorTransform => transform;

    /// <summary>Fires once when CurrentHp reaches 0. Single-shot, not re-raised until ResetToFull().</summary>
    public event Action OnBaseDestroyed;

    private void Awake()
    {
        maxHp = Mathf.Max(1f, maxHp);
        currentHp = maxHp;
        hasLoggedDestroyed = false;
        hasNotifiedDestroyed = false;
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

        if (currentHp > 0f)
        {
            return;
        }

        if (!hasLoggedDestroyed)
        {
            hasLoggedDestroyed = true;
            Debug.Log("BaseCore destroyed - Game Over", this);
        }

        if (!hasNotifiedDestroyed)
        {
            hasNotifiedDestroyed = true;
            OnBaseDestroyed?.Invoke();
        }
    }

    /// <summary>Restores full HP and re-arms the destroyed notification for a day retry.</summary>
    public void ResetToFull()
    {
        currentHp = maxHp;
        hasLoggedDestroyed = false;
        hasNotifiedDestroyed = false;
    }
}
