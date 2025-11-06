using System;

public class MasterWeight
{
    // Properties
    public string Name { get; set; }
    public string Job { get; set; }
    public int Rank { get; set; }
    public int Score { get; set; }

    // Default Constructor
    public MasterWeight()
    {
        // Default constructor sets default values
        Name = "";
        Job = "";
        Rank = 0;
        Score = 0;
    }

    // Parameterized Constructor
    public MasterWeight(string name, string job, int rank, int score)
    {
        Name = name;
        Job = job;
        Rank = rank;
        Score = score;
    }

    // Override ToString() method to provide custom string representation
    public override string ToString()
    {
        return $"Name: {Name}, Job: {Job}, Rank: {Rank}, Score: {Score}";
    }

    public string ToStringJobScore()
    {


        return $"{Job}, {Score}, ";
    }
}
