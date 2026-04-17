public interface IRepairable
{
    bool IsDamaged { get; }

    /// <summary>
    /// Applies repair for this frame. Returns false if repair cannot continue (e.g. cannot pay for next chunk while still damaged).
    /// </summary>
    bool TickRepair(float deltaTime, ResourceManager resourceManager);

    bool IsFullyRepaired();

    void CancelRepair();
}
