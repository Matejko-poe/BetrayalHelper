public class BetrayalMaster
{
    public string Name { get; set; }
    public string Job { get; set; }
    public int Rank { get; set; }
    public bool IsLeader { get; set; }
    public List<string> Relationships { get; set; }
    public Dictionary<string, string> RelationshipStatuses { get; set; }
    public int Score { get; set; }

    // Board detection & visual tracking
    public int SlotIndex { get; set; } = -1;
    public float ScreenX { get; set; } = 0;
    public float ScreenY { get; set; } = 0;
    public float Width { get; set; } = 0;

    // Strategic goal analysis
    public string TargetDivision { get; set; } = "";
    public bool IsWellPlaced { get; set; } = false;
    public string GoalStatus { get; set; } = "";
    public string GoalAction { get; set; } = "";

    // Constructor
    public BetrayalMaster()
    {
        Name = string.Empty;
        Job = string.Empty;
        Rank = 0;
        IsLeader = false;
        Relationships = new List<string>();
        RelationshipStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Score = 0;
    }
    public BetrayalMaster(string name, string job, int rank, bool isLeader, List<string> relationships, int score)
    {
        Name = name;
        Job = job;
        Rank = rank;
        IsLeader = isLeader;
        Relationships = relationships;
        RelationshipStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Score = score;
    }
    public override string ToString()
    {

        string relationshipNames = "";
        if (Relationships != null)
        {
            relationshipNames = string.Join(", ", Relationships);
        }

        string typedRelationships = string.Join(", ", RelationshipStatuses.Select(x => $"{x.Key}:{x.Value}"));
        return $"Name: {Name}, Job: {Job}, Rank: {Rank}, IsLeader: {IsLeader}, Relationships: {relationshipNames}, RelationshipTypes: {typedRelationships}, Score: {Score}";
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
