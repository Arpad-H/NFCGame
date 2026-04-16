using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public int id;
    public string name;
    public List<ResonanceType> resonances;
    public PlayerData(int id, string name) {
        this.id = id;
        this.name = name;
        resonances = new List<ResonanceType>();
    }
}