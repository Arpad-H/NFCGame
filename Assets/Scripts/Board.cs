using UnityEngine;

public class Board : MonoBehaviour
{
    public GameObject[] portalsLeft;
    public GameObject[] portalsRight;
    void Start()
    {
        SetUpBoard();
    }

    void SetUpBoard()
    {
        foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
        {
            if (player.id == 1)
            {
                // Set up player 1's portals on the left
                for (int i = 0; i < portalsLeft.Length; i++)
                {
                    var portal = portalsLeft[i].GetComponent<Portal>();
                    if (portal != null)
                    {
                        portal.SetResonanceType(player.resonances[i]);
                    }
                }
            }
            else if (player.id == 2)
            {
                // Set up player 2's portals on the right
                for (int i = 0; i < portalsRight.Length; i++)
                {
                    var portal = portalsRight[i].GetComponent<Portal>();
                    if (portal != null)
                    {
                        portal.SetResonanceType(player.resonances[i]);
                    }
                }
            }
        }
    }

}