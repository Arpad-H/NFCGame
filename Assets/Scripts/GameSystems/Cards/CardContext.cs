using GameSystems;

public class CardContext
{
    public PlayerSide Owner;       
    public PlayerSide Opponent;    
    public Board Board;        
    
    // Contextual Data
    public Lane Lane;    // The lane where the card was played/targeted
    public Portal SourcePortal; // The specific portal the card is interacting with
    public CardData SourceCard; // The card itself (for "self" buffs)
    
   //Builder pattern for easy context construction
    public CardContext()
    {
        
    }
    public CardContext  SetOwner(PlayerSide owner)
    {
        Owner = owner;
        return this;
    }

    public CardContext SetOpponent(PlayerSide opponent)
    {
        Opponent = opponent;
        return this;
    }

    public CardContext SetBoard(Board board)
    {
        Board = board;
        return this;
    }
    public CardContext SetTargetLane(Lane targetLane) 
    {
        Lane = targetLane;
        return this;
    }
    public CardContext SetSourcePortal(Portal sourcePortal) 
    {
        SourcePortal = sourcePortal;
        return this;
    }
    public CardContext SetSourceCard(CardData sourceCard) 
    {
        SourceCard = sourceCard;
        return this;
    }
}