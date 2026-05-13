using System.Collections.Generic;
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
        return Dispatch(new GameEvent(GameEventType.OnRoundStart, null, roundNumber));
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
        return Dispatch(new GameEvent(GameEventType.OnCardDrawn, null, player));
    }
    public Task CardDiscarded(Player player)
    {
        return Dispatch(new GameEvent(GameEventType.OnCardDiscarded, null, player));
    }
}