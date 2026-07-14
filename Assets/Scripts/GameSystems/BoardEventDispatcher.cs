using System.Threading.Tasks;
using GameSystems;

public class BoardEventDispatcher
{
    private Board board;

    public BoardEventDispatcher(Board board)
    {
        this.board = board;
    }

    private Task Dispatch(GameEvent evt)
    {
        return board.HandleEventOnBoard(evt);
    }

    public Task RoundStart(int roundNumber)
    {
        board.CurrentRound = roundNumber;
        return Dispatch(new GameEvent(GameEventType.OnRoundStart, null, new RoundEventData(roundNumber)));
    }

    public async Task RoundEnd()
    {
        await Dispatch(new GameEvent(GameEventType.OnRoundEnd, null));
        // After the broadcast, so end-of-round triggers still enjoy the final
        // multiplied turn — matching how status durations tick post-trigger.
        board.TickPortalDamageMultipliers();
    }

    // activeSide is the player whose turn is resolving; their minions take
    // priority in lanes that can't resolve simultaneously.
    public Task CombatResolution(PlayerSide activeSide)
    {
        // Default attacks resolve lane by lane with simultaneous clashes; the
        // OnCombatResolution broadcast (for cards with their own combat
        // triggers) is raised by the board at the end of that phase.
        return board.ResolveCombat(activeSide);
    }

    public Task CardDrawn(Player player)
    {
        return Dispatch(new GameEvent(GameEventType.OnCardDrawn, null, new PlayerEventData(player)));
    }

    public Task CardDiscarded(Player player)
    {
        return Dispatch(new GameEvent(GameEventType.OnCardDiscarded, null, new PlayerEventData(player)));
    }
}
