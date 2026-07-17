[System.Serializable]
public class RunRuntimeState
{
    public BaseRuntimeState baseState;
    public ResourceRuntimeState resourceState;
    public PlayerRuntimeState playerState;

    public RunRuntimeState()
    {
        baseState = new BaseRuntimeState();
        resourceState = new ResourceRuntimeState();
        playerState = new PlayerRuntimeState();
    }

    public RunRuntimeState(BaseRuntimeState baseState, ResourceRuntimeState resourceState, PlayerRuntimeState playerState)
    {
        this.baseState = baseState ?? new BaseRuntimeState();
        this.resourceState = resourceState ?? new ResourceRuntimeState();
        this.playerState = playerState ?? new PlayerRuntimeState();
    }
}
