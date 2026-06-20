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
        return Dispatch(new GameEvent(GameEventType.OnCombatResolution, null));
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
