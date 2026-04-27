using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;
using Random = UnityEngine.Random;

public class Board
{
    public Lane[] lanes = new Lane[3];
    private Dictionary<ResonanceType, List<Portal>> resonanceMap = new Dictionary<ResonanceType, List<Portal>>();
    private int maxCardsPerPortal;
    public bool shufflePortals = false;
    private readonly List<FieldableCardInstance> boardCards = new();

    public void SetUpBoard(int maxCards)
    {
        maxCardsPerPortal = maxCards;

        //initialize lanes
        for (int i = 0; i < lanes.Length; i++)
        {
            lanes[i] = new Lane(i);
        }

        //Find all portals and assign them to player sides
        Portal[] allPortals = GameObject.FindObjectsByType<Portal>(FindObjectsSortMode.None);
        List<Portal> leftPortals = new List<Portal>();
        List<Portal> rightPortals = new List<Portal>();

        foreach (var p in allPortals)
        {
            int index = p.laneIndex;

            if (p.ownerSide == PlayerSide.Left)
                lanes[index].LeftPortal = p;
            else
                lanes[index].RightPortal = p;
        }

        //Shuffle both lists
        if (shufflePortals)
        {
            Debug.LogWarning("Not implemented functionally yet");
            ShuffleList(leftPortals);
            ShuffleList(rightPortals);
        }


        //Assign portals to Lanes
        if (leftPortals.Count == 3 && rightPortals.Count == 3)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i].LeftPortal = leftPortals[i];
                lanes[i].RightPortal = rightPortals[i];
            }
        }

        //Assign resonance types based on player data (the reosnances they picked in the game setup
        if (WebSocketServerBehaviour.Instance ==
            null) //TODO game launched without server (game scene instead of main menu scene, generate mock data for testing without main menu
        {
            lanes[0].LeftPortal.SetResonanceType(ResonanceType.Fire);
            lanes[0].RightPortal.SetResonanceType(ResonanceType.Wind);
            lanes[1].LeftPortal.SetResonanceType(ResonanceType.Death);
            lanes[1].RightPortal.SetResonanceType(ResonanceType.Darkness);
            lanes[2].LeftPortal.SetResonanceType(ResonanceType.Spirit);
            lanes[2].RightPortal.SetResonanceType(ResonanceType.Light);
        }
        else
        {
            foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
            {
                for (int i = 0; i < lanes.Length; i++)
                {
                    if (player.id == 1)
                    {
                        lanes[i].LeftPortal.SetResonanceType(player.resonances[i]);
                    }
                    else if (player.id == 2)
                    {
                        lanes[i].RightPortal.SetResonanceType(player.resonances[i]);
                    }
                }
            }
        }

        BuildResonanceIndex();
    }

    private void BuildResonanceIndex()
    {
        resonanceMap.Clear();
        foreach (var lane in lanes)
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

    public bool PlaceCard(FieldableCardInstance cardInstance)
    {
        if (resonanceMap.TryGetValue(cardInstance.SourceCard.resonance, out List<Portal> matchingPortals))
        {
            foreach (var portal in matchingPortals)
            {
                // Ensure the portal belongs to the player trying to place the card
                if (portal.ownerSide == cardInstance.Owner.playerSide)
                {
                    if (portal.GetCardCount() >= maxCardsPerPortal)
                    {
                        Debug.LogWarning($"Portal for {portal.resonance} is full. Cannot place card.");
                        return false;
                    }

                    cardInstance.SetSourcePortal(portal).SetTargetLane(GetLaneForPortal(portal));
                    portal.AddCard(cardInstance);
                    boardCards.Add(cardInstance);
                    Debug.Log($"Placed {cardInstance.SourceCard.cardName} in {portal.resonance} portal in Lane {GetLaneForPortal(portal).LaneIndex} for {cardInstance.Owner}");
                    return true;
                }
            }
        }

        Debug.LogWarning($"No matching {cardInstance.SourceCard.resonance} portal found for {cardInstance.Owner}");
        return true;
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

    public Lane GetLaneForPortal(Portal portal)
    {
        foreach (var lane in lanes)
        {
            if (lane.LeftPortal == portal || lane.RightPortal == portal)
                return lane;
        }

        return null;
    }

    public void HandleEventOnBoard(GameEvent gameEvent)
    {
        FieldableCardInstance[] snapshot = boardCards.ToArray();

        foreach (FieldableCardInstance ctx in snapshot)
        {
            if (ctx is not IGameEventReceiver receiver) continue;
            
            receiver.HandleEvent(new GameEvent(gameEvent.Type, ctx, gameEvent.GameEventPayload));
        }
    }
}


public class Lane
{
    public int LaneIndex; // 0,1,2. 0 is Top, 1 is middle, 2 is bottom
    public Portal LeftPortal;
    public Portal RightPortal;

    public Lane(int index)
    {
        LaneIndex = index;
    }
}