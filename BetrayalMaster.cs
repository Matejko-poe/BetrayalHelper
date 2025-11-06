public class BetrayalMaster
{
    public string Name { get; set; }
    public string Job { get; set; }
    public int Rank { get; set; }
    public bool IsLeader { get; set; }
    public List<string> Relationships { get; set; }
    public int Score { get; set; }

    // Constructor
    public BetrayalMaster()
    {
        Name = string.Empty;
        Job = string.Empty;
        Rank = 0;
        IsLeader = false;
        Relationships = new List<string>();
        Score = 0;
    }
    public BetrayalMaster(string name, string job, int rank, bool isLeader, List<string> relationships, int score)
    {
        Name = name;
        Job = job;
        Rank = rank;
        IsLeader = isLeader;
        Relationships = relationships;
        Score = score;
    }
    public override string ToString()
    {

        string relationshipNames = "";
        if (Relationships != null)
        {
            relationshipNames = string.Join(", ", Relationships);
        }

        return $"Name: {Name}, Job: {Job}, Rank: {Rank}, IsLeader: {IsLeader}, Relationships: {relationshipNames},Score: {Score}";
    }

    public string ToStringShort()
    {

        var relationshipCount = Relationships.Count();

        return $"{Rank}, {Score}, {Name}, {relationshipCount}";
    }

    public string ToStringJobScore()
    {


        return $"{Job}, {Score}, ";
    }
}