using System.Collections.Generic;
using GameSystems;
using UnityEngine;

public class Board
{
    public Lane[] Lanes = new Lane[3];
    private Dictionary<ResonanceType, List<Portal>> resonanceMap = new Dictionary<ResonanceType, List<Portal>>();

    public void SetUpBoard()
    {
        // 1. Find all portals and initialize lanes
        List<Portal> portals = new List<Portal>(GameObject.FindObjectsByType<Portal>(FindObjectsSortMode.None));

        for (int i = 0; i < Lanes.Length; i++)
        {
            Lanes[i] = new Lane(i + 1);
        }

        Portal[] allPortals = GameObject.FindObjectsByType<Portal>(FindObjectsSortMode.None);
        List<Portal> leftPortals = new List<Portal>();
        List<Portal> rightPortals = new List<Portal>();

        foreach (var p in allPortals)
        {
            // Assuming your Portal class has a 'side' or 'isLeft' property
            if (p.ownerSide == PlayerSide.Left)
                leftPortals.Add(p);
            else
                rightPortals.Add(p);
        }

        // 3. Shuffle both lists individually
        ShuffleList(leftPortals);
        ShuffleList(rightPortals);

        // 4. Assign to Lanes (matching index to index)
        if (leftPortals.Count == 3 && rightPortals.Count == 3)
        {
            for (int i = 0; i < Lanes.Length; i++)
            {
                Lanes[i].LeftPortal = leftPortals[i];
                Lanes[i].RightPortal = rightPortals[i];
            }
        }

        // 4. Assign resonance types based on player data
        if (WebSocketServerBehaviour.Instance ==
            null) //TODO game launched without server, generate mock data for testing without main menu
        {
            Lanes[0].LeftPortal.SetResonanceType(ResonanceType.Fire);
            Lanes[0].RightPortal.SetResonanceType(ResonanceType.Wind);
            Lanes[1].LeftPortal.SetResonanceType(ResonanceType.Death);
            Lanes[1].RightPortal.SetResonanceType(ResonanceType.Darkness);
            Lanes[2].LeftPortal.SetResonanceType(ResonanceType.Spirit);
            Lanes[2].RightPortal.SetResonanceType(ResonanceType.Light);
        }
        else
        {
            foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
            {
                for (int i = 0; i < Lanes.Length; i++)
                {
                    if (player.id == 1)
                    {
                        Lanes[i].LeftPortal.SetResonanceType(player.resonances[i]);
                    }
                    else if (player.id == 2)
                    {
                        Lanes[i].RightPortal.SetResonanceType(player.resonances[i]);
                    }
                }
            }
        }

        BuildResonanceIndex();
    }

    private void BuildResonanceIndex()
    {
        resonanceMap.Clear();
        foreach (var lane in Lanes)
        {
            IndexPortal(lane.LeftPortal);
            IndexPortal(lane.RightPortal);
        }
    }

    private void IndexPortal(Portal portal)
    {
        if (!resonanceMap.ContainsKey(portal.resonance.ResonanceType))
            resonanceMap[portal.resonance.ResonanceType] = new List<Portal>();

        resonanceMap[portal.resonance.ResonanceType].Add(portal);
    }

    public bool PlaceCard(PlayerSide playerSide, int cardId)
    {
        CardData card;
        switch (cardId)
        {
            case 1:
                card = new CardData("placeholder", ResonanceType.Life); //TODO Placeholder, replace with actual card lookup
                break;
            case 2:
                card = new CardData("placeholder", ResonanceType.Death); //TODO Placeholder, replace with actual card lookup
                break;
            case 3:
                card = new CardData("placeholder", ResonanceType.Fire); //TODO Placeholder, replace with actual card lookup
                break;
            case 4:
                card = new CardData("placeholder", ResonanceType.Gravity); //TODO Placeholder, replace with actual card lookup
                break;
            case 5 :
                card = new CardData("placeholder", ResonanceType.Spirit); //TODO Placeholder, replace with actual card lookup
                break;
            case 6 :
                card = new CardData("placeholder", ResonanceType.Wind); //TODO Placeholder, replace with actual card lookup
                break;
            default:
                Debug.LogWarning($"Invalid card ID: {cardId}");
                return false;
            
        }
        if (resonanceMap.TryGetValue(card.resonance, out List<Portal> matchingPortals))
        {
            foreach (var portal in matchingPortals)
            {
                // Ensure the portal belongs to the player trying to place the card
                if (portal.ownerSide == playerSide)
                {
                    portal.AddCard(card);
                    return true;
                }
            }
        }

        Debug.LogWarning($"No matching {card.resonance} portal found for {playerSide}");
        return false;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}


public class Lane
{
    public int LaneIndex; // 1, 2, or 3; 1 is Top, 2 is Middle, 3 is Bottom
    public Portal LeftPortal;
    public Portal RightPortal;

    public Lane(int index)
    {
        LaneIndex = index;
    }
}