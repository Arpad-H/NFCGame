using System.Collections.Generic;

public class BoardEventDispatcher
{
    private Board board;

    public BoardEventDispatcher(Board board)
    {
        this.board = board;
    }

    private void Dispatch(GameEvent evt)
    {
        board.HandleEventOnBoard(evt);
    }

    public void RoundStart(int roundNumber)
    {
        Dispatch(new GameEvent(GameEventType.OnRoundStart, null, roundNumber));
    }

    public void RoundEnd()
    {
        Dispatch(new GameEvent(GameEventType.OnRoundEnd, null));
    }

    public void CombatResolution()
    {
        Dispatch(new GameEvent(GameEventType.OnCombatResolution, null));
    }
    public void CardDrawn(Player player)
    {
        Dispatch(new GameEvent(GameEventType.OnCardDrawn, null, player));
    }
    public void CardDiscarded(Player player)
    {
        Dispatch(new GameEvent(GameEventType.OnCardDiscarded, null, player));
    }
}