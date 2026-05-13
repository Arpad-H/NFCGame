namespace GameSystems.Cards
{
public interface ICalculateValueLogic
{
        int CalculateValue(EffectContext context);
}

[System.Serializable]
public class IntegerValue : ICalculateValueLogic
{
    public int value = 0;

    public int CalculateValue(EffectContext context)
    {
        return value;
    }
}
}