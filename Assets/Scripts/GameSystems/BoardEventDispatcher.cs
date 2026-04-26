using System.Collections.Generic;

public class BoardEventDispatcher
{
    private Board board;

    public BoardEventDispatcher(Board board)
    {
        this.board = board;
    }

    public void Dispatch(GameEventType evt)
    {
        board.HandleEventOnBoard(evt);
    }

    public void RoundStart()
    {
        Dispatch(GameEventType.OnRoundStart);
    }

    public void RoundEnd()
    {
        Dispatch(GameEventType.OnRoundEnd);
    }

    public void CombatResolution()
    {
        Dispatch(GameEventType.OnCombatResolution);
    }
}