using System.Threading.Tasks;

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

    public Task RoundEnd()
    {
        return Dispatch(new GameEvent(GameEventType.OnRoundEnd, null));
    }

    public Task CombatResolution()
    {
        // Default attacks resolve lane by lane with simultaneous clashes; the
        // OnCombatResolution broadcast (for cards with their own combat
        // triggers) is raised by the board at the end of that phase.
        return board.ResolveCombat();
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
