public static class CardFactory
{
    public static FieldableCardInstance CreateInstance(CardData data, Player owner, Player opponent, Board board, int  currentRound)
    {
        // Start with the base setup
        FieldableCardInstance instance;

        // Determine the logic type based on the data
        if (data.cardType is MinionType)
        {
            instance = new MinionInstance();
        }
        else
        {
            instance = new FieldableCardInstance();
        }

        // Initialize common properties (Fluent style)
       instance.SetSourceCard(data)
            .SetOwner(owner)
            .SetBoard(board)
            .SetOpponent(opponent).SetSummonedOnRound(currentRound);
       instance.Initialize();
       return instance;
    }
}