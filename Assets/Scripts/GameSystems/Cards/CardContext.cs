using GameSystems;

public abstract class CardContext 
{
    public Player Owner;
    public Player Opponent;
    public Board Board;
}
public abstract class CardContext<T> : CardContext where T : CardContext<T>
{
    public T SetOwner(Player owner) { Owner = owner; return (T)this; }
    public T SetOpponent(Player opponent) { Opponent = opponent; return (T)this; }
    public T SetBoard(Board board) { Board = board; return (T)this; }
}

public class FieldableCardContext : CardContext<FieldableCardContext>
{
    public Lane Lane;
    public Portal SourcePortal;
    public ITargetable cardInstance;
    public CardData SourceCard;

    public FieldableCardContext SetTargetLane(Lane lane) { Lane = lane; return this; }
    public FieldableCardContext SetSourceCard(CardData card) { SourceCard = card; return this; }
    public FieldableCardContext SetSourcePortal(Portal portal) { SourcePortal = portal; return this; }
    public FieldableCardContext SetTarget(ITargetable newCardInstance) { cardInstance = newCardInstance; return this; }
    
}