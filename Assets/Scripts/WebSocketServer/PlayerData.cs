[System.Serializable]
public class PlayerData
{
    public int id;
    public string name;

    public PlayerData(int id, string name) {
        this.id = id;
        this.name = name;
    }
}