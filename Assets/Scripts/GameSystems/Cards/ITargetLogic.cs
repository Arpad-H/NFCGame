using System.Collections.Generic;
using UnityEngine;

public interface ITargetLogic
{
    List<CardData> GetTargets(); 
}
[System.Serializable]
public class EnemyHeroTarget : ITargetLogic
{
    public List<CardData> GetTargets()    {
        Debug.Log("Targeting enemy hero");
        return new List<CardData>(); // Placeholder
    }
}