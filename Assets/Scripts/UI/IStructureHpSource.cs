using UnityEngine;

public interface IStructureHpSource
{
    float CurrentHp { get; }
    float MaxHp { get; }
    bool IsDestroyed { get; }
    Transform HpAnchorTransform { get; }
}
