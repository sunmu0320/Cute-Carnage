using UnityEngine;

public class ZombieAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private Zombie zombie;

    private void Awake()
    {
        if (zombie == null)
        {
            zombie = GetComponentInParent<Zombie>();
        }
    }

    public void AnimationEvent_ApplyAttackHit()
    {
        if (zombie != null)
        {
            zombie.AnimationEvent_ApplyAttackHit();
        }
    }
}
