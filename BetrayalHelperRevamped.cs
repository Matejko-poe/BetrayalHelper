using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using Vector2N = System.Numerics.Vector2;

namespace BetrayalHelperRevamped
{
    public class BetrayalHelperRevamped : BaseSettingsPlugin<BetrayalHelperRevampedSettings>
    {
        private const string masterWeightFile = "masters.csv";

        private Action ShowSyndicateBoard;
        List<BetrayalMaster> ListBetrayalMasters = new List<BetrayalMaster>();
        public List<MasterWeight> masterWeights;
        public List<MasterWeight> _masterWeights;
        bool badJobsAreFull = false;
        bool hasClosedWindow = false;


        List<string> allMasters = new List<string>
        {
            "Leo",
            "Jorgin",
            "Vagan",
            "Tora",
            "Janus",
            "Korell",
            "Guff",
            "Elreon",
            "Cameria",
            "It That Fled",
            "Vorici",
            "Aisling",
            "Haku",
            "Hillock",
            "Gravicius",
            "Riker",
            "Rin"


        };
        public List<string> _missingMastersFromBoard;
        public List<string> _mastersToBePresent;
        public List<string> _jobsToGainIntelligence;
        public List<string> _jobsToNOTGainIntelligence;



        string _eventAction = "";
        string _eventText = "";
        string _jobEvent ="";
        string _masterText = "";
        RectangleF _SpecialButton = new RectangleF();
        RectangleF _ReleaseButton = new RectangleF();
        RectangleF _InterrogateButton = new RectangleF();
        BetrayalMaster _eventMaster = new BetrayalMaster();
        List<BetrayalMaster> _eventMasters = new List<BetrayalMaster>();



        public override bool Initialise()
        {

            masterWeights = GetMasterWeights();
            _masterWeights = GetMasterWeights();

           
            Settings.ReloadRules.OnPressed += () => GetMasterWeights();

            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            hasClosedWindow = false;
        }
        private List<MasterWeight> GetMasterWeights()
        {
            var customFilePath = Path.Join(DirectoryFullName, masterWeightFile);
            List<MasterWeight> masterWeights = new List<MasterWeight>();

            if (!File.Exists(customFilePath))
            {
                LogMessage("File does not exist: " + customFilePath, 5, Color.Red);
                return masterWeights;
            }

            // First, generate the full default dataset
            masterWeights = GenerateDefaultMasterWeights();

            // Then, read the simplified CSV and override specific values
            var overrides = ReadSimplifiedCSV(customFilePath);
            ApplyOverrides(masterWeights, overrides);
            //OutputFullCSV(masterWeights);
            return masterWeights;
        }
        private List<MasterWeight> GenerateDefaultMasterWeights()
        {
            var masterWeights = new List<MasterWeight>();

            // List of all masters
            string[] masters = {
        "Aisling", "Cameria", "Elreon", "Gravicius", "Guff", "Haku",
        "Hillock", "It That Fled", "Janus", "Jorgin", "Korell",
        "Leo", "Riker", "Rin", "Tora", "Vagan", "Vorici"
    };

            // List of all jobs
            string[] jobs = { "Fortification", "Intervention", "Research", "Transportation" };

            // List of all ranks
            int[] ranks = { 1, 2, 3, 4 };

            // Generate all combinations with default scores
            foreach (string master in masters)
            {
                foreach (string job in jobs)
                {
                    foreach (int rank in ranks)
                    {
                        int defaultScore = 1;

                        masterWeights.Add(new MasterWeight
                        {
                            Name = master,
                            Job = job,
                            Rank = rank,
                            Score = defaultScore
                        });
                    }
                }
            }

            return masterWeights;
        }

        private Dictionary<string, Dictionary<string, int>> ReadSimplifiedCSV(string filePath)
        {
            var overrides = new Dictionary<string, Dictionary<string, int>>();

            using (var reader = new StreamReader(filePath))
            {
                reader.ReadLine(); // Skip header
                string line;
                int lineNumber = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string[] fields = ParseCSVLine(line);

                    if (fields.Length < 3)
                    {
                        LogMessage($"Invalid number of fields in line {lineNumber}: {line}", 6, Color.Green);
                        continue;
                    }

                    string name = fields[0].Trim();
                    string job = fields[1].Trim();

                    if (!int.TryParse(fields[2], out int score))
                    {
                        LogMessage($"Invalid format for Score field in line {lineNumber}: {line}", 6, Color.Green);
                        continue;
                    }

                    // Store the override
                    if (!overrides.ContainsKey(name))
                    {
                        overrides[name] = new Dictionary<string, int>();
                    }
                    overrides[name][job] = score;
                }
            }

            return overrides;
        }

        private void ApplyOverrides(List<MasterWeight> masterWeights, Dictionary<string, Dictionary<string, int>> overrides)
        {
            // First, identify jobs that have masters with score of 30
            var jobsWithSpecialMasters = new HashSet<string>();
            var mastersWithSpecialJobs = new HashSet<string>();

            foreach (var masterOverride in overrides)
            {
                string masterName = masterOverride.Key;
                mastersWithSpecialJobs.Add(masterName);

                foreach (var jobOverride in masterOverride.Value)
                {
                    string jobName = jobOverride.Key;
                    int score = jobOverride.Value;

                    if (score == 30)
                    {
                        jobsWithSpecialMasters.Add(jobName);
                    }
                }
            }

            // Apply overrides
            foreach (var masterWeight in masterWeights)
            {
                if (overrides.ContainsKey(masterWeight.Name))
                {
                    // This master has overrides specified
                    if (overrides[masterWeight.Name].ContainsKey(masterWeight.Job))
                    {
                        // Apply the override score for this master-job combination
                        masterWeight.Score = overrides[masterWeight.Name][masterWeight.Job];
                    }
                    else
                    {
                        // If this master has overrides but not for this job, set to -1
                        masterWeight.Score = -1;
                    }
                }
                else
                {
                    // This master is not in overrides, but check if the job has a special master
                    if (jobsWithSpecialMasters.Contains(masterWeight.Job))
                    {
                        // Another master has score 30 for this job, so set this master to -1
                        masterWeight.Score = -1;
                    }
                    // Otherwise, keep the default score of 1
                }
            }
        }

        private string[] ParseCSVLine(string line)
        {
            List<string> fields = new List<string>();
            bool inQuotes = false;
            int startField = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(line.Substring(startField, i - startField).Trim('"'));
                    startField = i + 1;
                }
            }

            // Add the last field
            fields.Add(line.Substring(startField).Trim('"'));

            return fields.ToArray();
        }
        private void OutputFullCSV(List<MasterWeight> masterWeights)
        {
            var outputFilePath = Path.Join(DirectoryFullName, "masters_generated_full.csv");

            try
            {
                using (var writer = new StreamWriter(outputFilePath))
                {
                    // Write header
                    writer.WriteLine("Name,Job,Rank,Score");

                    // Write all master weight data
                    foreach (var masterWeight in masterWeights)
                    {
                        writer.WriteLine($"{masterWeight.Name},{masterWeight.Job},{masterWeight.Rank},{masterWeight.Score}");
                    }
                }

                LogMessage($"Full CSV generated successfully: {outputFilePath}", 1, Color.Green);
            }
            catch (Exception ex)
            {
                LogMessage($"Error generating full CSV: {ex.Message}", 5, Color.Red);
            }
        }

        private Vector2N drawTextVector3;
        public override void Render()
        {
            checkIntel();
            scanBetrayal();
            betrayalWindow();


        }



        private void checkIntel()
        {
            var ingameState = GameController.Game.IngameState;
            var syndicatePanel = ingameState.IngameUi.SyndicatePanel;
            var leadersData = syndicatePanel.SyndicateLeadersData.Leaders;
            var warnings = new List<string>();

            if (leadersData is not null && leadersData.Count() ==4)
            {
                foreach (var leaderdata in leadersData)
                {
                    if (leaderdata is not null && leaderdata.Job is not null)
                    {
                        var intel = leaderdata.Intel;
                        var job = leaderdata.Job.Name;

                        if (intel >= 100)
                        {

                            warnings.Add(job);
                        }
                    }


                }
            }

            if (GameController.Area.CurrentArea.Area.IsHideout && warnings.Count > 0 && !hasClosedWindow)
            {
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(800, 500), ImGuiCond.Once);
                ImGui.SetNextWindowSize(new System.Numerics.Vector2(800, 120), ImGuiCond.Once);

                // Link ImGui's open flag directly to your global one
                bool isOpen = !hasClosedWindow;

                if (ImGui.Begin("Hideout Intel Warning", ref isOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
                {
                    string message = "The following jobs have the hideout ready: " + string.Join(", ", warnings);
                    ImGui.TextWrapped(message);

                    ImGui.Spacing();

                    if (ImGui.Button("Close"))
                    {
                        hasClosedWindow = true;
                    }
                }
                ImGui.End();

                // If user clicked the titlebar X, also close it
                if (!isOpen)
                {
                    hasClosedWindow = true;
                }
            }



        }
        
        private void scanBetrayal()
        {

            var ingameState = GameController.Game.IngameState;

            if (!ingameState.IngameUi.SyndicatePanel.IsVisible)
            {

            
            List<BetrayalMaster> masters = new List<BetrayalMaster>();


            var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.BetrayalChoice];

            var betrayalLabels = GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels
                    .Where(x => x != null && x.IsVisible && x.Label != null && x.Label.IsValid &&
                            x.Label.IsVisible && x.ItemOnGround != null &&
                            (x.ItemOnGround.Metadata.Equals("Metadata/Terrain/Leagues/Betrayal/Objects/BetrayalMakeChoice", StringComparison.OrdinalIgnoreCase)));
                       


            if (entities.Count() > 0)
            {
                updateSyndicateInfo();
            

            foreach (var label in betrayalLabels)
            {
                //var auxmaster = entity.GetComponent<StateMachine>().States.FirstOrDefault(state => state.Name == "NPCIndex");
                //var npcindex = (int)auxmaster.Value;
                //var aux = GameController.Files.BetrayalTargets.EntriesList[npcindex];
                var aux = label.Label.GetChildFromIndices(0, 0).Text;
                var master = ListBetrayalMasters.FirstOrDefault(master => master.Name == aux);
                



                RectangleF rect = label.Label.GetClientRect();


                // Create the rectangle starting at aux3.X, aux3.Y

                if (master is not null)
                {
                    if (master.Score == 30)
                    {
                        Graphics.DrawFrame(rect, Color.Green, 5);
                    }
                    else if(master.Score == 1)
                    {
                        Graphics.DrawFrame(rect, Color.Yellow, 5);
                    } else
                    {
                        Graphics.DrawFrame(rect, Color.Red, 5);
                    }
                }


                //if (aux2 != null)
                //    masters.Add(aux2);
            }

                }
            }


        }
        private void betrayalWindow()
        {
            var ingameState = GameController.Game.IngameState;

            if (ingameState.IngameUi.SyndicatePanel.IsVisible)
            {
                DrawSyndicateBoard(ingameState.IngameUi.SyndicatePanel);


            }
        }
        private void DrawBottomBoardGui()
        {




            List<BetrayalMaster> transportationBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Transportation").Select(b => new BetrayalMaster(b.Name, b.Job, b.Rank, b.IsLeader, b.Relationships, b.Score)).OrderByDescending(b => b.IsLeader)
            .ThenBy(b => b.Rank).ToList();
            List<BetrayalMaster> fortificationBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Fortification").Select(b => new BetrayalMaster(b.Name, b.Job, b.Rank, b.IsLeader, b.Relationships, b.Score)).OrderByDescending(b => b.IsLeader)
            .ThenBy(b => b.Rank).ToList();

            List<BetrayalMaster> researchBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Research").Select(b => new BetrayalMaster(b.Name, b.Job, b.Rank, b.IsLeader, b.Relationships, b.Score)).OrderByDescending(b => b.IsLeader)
            .ThenBy(b => b.Rank).ToList();

            List<BetrayalMaster> interventionBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Intervention").Select(b => new BetrayalMaster(b.Name, b.Job, b.Rank, b.IsLeader, b.Relationships, b.Score)).OrderByDescending(b => b.IsLeader)
            .ThenBy(b => b.Rank).ToList();

            List<BetrayalMaster> unnalocatedBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "None").Select(b => new BetrayalMaster(b.Name, "Unnalocated", b.Rank, b.IsLeader, b.Relationships, b.Score)).OrderByDescending(b => b.IsLeader)
.ThenBy(b => b.Rank).ToList();



            List<string> missingNames = allMasters.Except(ListBetrayalMasters.Select(b => b.Name)).ToList();

            var interestingMasters = masterWeights
                .Where(mw => mw.Score >= 30)
                .Select(mw => mw.Name)
                .Distinct()
                .ToList();

            var allLists = new List<BetrayalMaster>[] {
    transportationBetrayalMasters,
    fortificationBetrayalMasters,
    researchBetrayalMasters,
    interventionBetrayalMasters
};

            var headers = new string[] { "Transportation", "Fortification", "Research", "Intervention" };




            // Start main table with one column per list
            ImGui.BeginTable("MasterTables", allLists.Length, ImGuiTableFlags.Borders);

            for (int t = 0; t < allLists.Length; t++)
            {
                ImGui.TableNextColumn();

                // Header for this list
                ImGui.Text(headers[t]);
                ImGui.Separator();

                // Child table for this list
                if (ImGui.BeginTable($"Table{t}", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Rank");
                    ImGui.TableSetupColumn("Name");
                    ImGui.TableHeadersRow();

                    foreach (var master in allLists[t])
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(master.Rank.ToString());

                        ImGui.TableNextColumn();

                        var color = new System.Numerics.Vector4(0f, 1f, 0f, 1f); // Green default
                        if (master.Score < 0)
                            color = new System.Numerics.Vector4(1f, 0f, 0f, 1f); // Red for negative score

                        ImGui.TextColored(color, master.Name);
                    }

                    ImGui.EndTable();
                }



            }
            // for unnalocated
            ImGui.TableNextColumn();

            // Header for this list
            ImGui.Text("Outside");
            ImGui.Separator();

            // Child table for this list
            if (ImGui.BeginTable($"Table", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Action");
                ImGui.TableHeadersRow();

                foreach (var missingmaster in missingNames)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    // find if that master should be on the board

                    var auxText = "";
                    var color = new System.Numerics.Vector4(0f, 1f, 0f, 1f); // Green default
                    if (_mastersToBePresent.Contains(missingmaster))
                    {
                        color = new System.Numerics.Vector4(1f, 0f, 0f, 1f); // Red for master that should be on main board
                        auxText = "Bring in";
                    }


                    ImGui.TextColored(color, missingmaster);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, auxText);

                }

                ImGui.EndTable();
            }

            // for interesting members
            ImGui.TableNextColumn();

            // Header for this list
            ImGui.Text("Interesting");
            ImGui.Separator();

            // Child table for this list
            if (ImGui.BeginTable($"Table", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Job");
                ImGui.TableHeadersRow();

                foreach (var interestingmaster in interestingMasters)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    // find if that master should be on the board



                    var interestingMasterAux = ListBetrayalMasters.FirstOrDefault(master => master.Name == interestingmaster);
                    var interestingMasterScore = interestingMasterAux?.Score ?? 0;
                    var interestingMasterJob = interestingMasterAux?.Job ?? "Outside!";
                    //var interestingMasterScore =  0;
                    //  var interestingMasterJob = "Job";

                    var color = new System.Numerics.Vector4(0f, 1f, 0f, 1f); // Green default
                    if (interestingMasterScore < 30)
                    {
                        color = new System.Numerics.Vector4(1f, 0f, 0f, 1f); // Red for master that should be on main board

                    }


                    ImGui.TextColored(color, interestingmaster);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, interestingMasterJob);

                }

                ImGui.EndTable();
            }

            ImGui.EndTable();
            ImGui.Button("Close");

        }

        private List<BetrayalMaster> BetrayalEventMembers()
        {
            List<BetrayalMaster> masters = new List<BetrayalMaster>();

            var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.BetrayalChoice];

            foreach (var entity in entities)
            {
                var auxmaster = entity.GetComponent<StateMachine>().States.FirstOrDefault(state => state.Name=="NPCIndex");
                var npcindex = (int)auxmaster.Value;
                var aux = GameController.Files.BetrayalTargets.EntriesList[npcindex];
                var aux2 = ListBetrayalMasters.FirstOrDefault(master => master.Name == aux.Name);
                if (aux2 != null) 
                    masters.Add(aux2);
            }


            return masters;
        }
        private void updateSyndicateInfo()
        {
            var ingameState = GameController.Game.IngameState;
            var syndicatePanel = ingameState.IngameUi.SyndicatePanel;

            ListBetrayalMasters.Clear();

            //foreach (var singleState in syndicateStates)

            // check if betrayal event is on

            var syndicateStates = syndicatePanel.SyndicateStates;

            for (int i = 0; i < syndicateStates.Count; i++)
            {
                var singleState = syndicateStates[i];
                var masterTest = singleState.UIElement.GetChildFromIndices(3, 0);
                var rank = singleState.Rank.RankInt;



                string[] parts = singleState.ToString().Split(',');
                var name = parts[0].Trim();
                var jobDescription = parts[1].Trim();
                var job = parts[2].Trim();
                

                var auxRelationships = singleState.Relations;
                auxRelationships.RemoveAll(p => p.Job == null);
                List<string> stateNames = auxRelationships.Select(state => state.ToString().Split(',')[0]).ToList();

                bool isLeader = i < 4;
                var auxRank = isLeader ? rank + 1 : rank;
                int score = GetScore(name, job, auxRank);
                BetrayalMaster betrayalMaster = new BetrayalMaster(name, job, rank, isLeader, stateNames, score);

                if (Settings.MegaDebugMode)
                    LogMessage(betrayalMaster.ToString(), 5, Color.Red);
                ListBetrayalMasters.Add(betrayalMaster);


            }


            List<string> missingMastersFromBoard = allMasters.Except(ListBetrayalMasters.Select(b => b.Name)).ToList();

            _missingMastersFromBoard = allMasters
              .Except(ListBetrayalMasters.Select(b => b.Name))
              .ToList();

            _mastersToBePresent = _masterWeights
                .Where(mw => mw.Score > 1)
                .Select(mw => mw.Name)
                .Distinct()
                .ToList();

        }

        private void DrawSyndicateBoard(SyndicatePanel syndicatePanel)
        {

            updateSyndicateInfo();
            //DrawBottomBoard();
            DrawBottomBoardGui();


            if (syndicatePanel.BetrayalEventData is not null)
            {

                if (syndicatePanel.BetrayalEventData.IsVisible)
                {
                    // we got an event

                    // can be betray

                    // can be swap leader

                    // swap





                    _eventAction = syndicatePanel.BetrayalEventData.Action.Id;
                    _eventText = syndicatePanel.BetrayalEventData.EventText;
                    _jobEvent = syndicatePanel.BetrayalEventData.CurrentEncounterJob.Name;
                    _SpecialButton = syndicatePanel.BetrayalEventData.SpecialButton.GetClientRectCache;
                    _ReleaseButton = syndicatePanel.BetrayalEventData.ReleaseButton.GetClientRectCache;
                    _InterrogateButton = syndicatePanel.BetrayalEventData.InterrogateButton.GetClientRectCache;

                    // children



                    // 4

                    // get the name of the current

                    var masterPanel = syndicatePanel.BetrayalEventData.GetChildAtIndex(4);
                    var masterPanelRect = masterPanel.GetClientRectCache;

                    _masterText = masterPanel.GetChildAtIndex(4).Text;

                    var masterWindow = masterPanel.GetClientRectCache;
                    var newWindow = new RectangleF(masterPanelRect.X, masterPanelRect.Y + 30 + masterPanelRect.Height, masterPanelRect.Width, 60);
                    Vector2 newWindowVector = newWindow.TopLeft;
                    Graphics.DrawBox(newWindow, Color.Black, 5);


                    // draw support text

                    if (Settings.DebugMode)
                    {
                        LogMessage("event  " + _eventText, 5, Color.Yellow);
                        LogMessage("masterText: " + _masterText, 5, Color.Yellow);
                    }


                    // given the name, get him his current rank and job
                 
                    _eventMaster = ListBetrayalMasters.FirstOrDefault(master => master.Name == _masterText);

                    // jobs to gain intelligence
                    _jobsToGainIntelligence = masterWeights
                        .Where(mw => mw.Score > 1)
                        .Select(mw => mw.Job)
                        .Distinct()
                        .ToList();
                    
                    _jobsToNOTGainIntelligence = masterWeights
                        .Where(mw => mw.Score == 1)
                        .Select(mw => mw.Job)
                        .Distinct()
                        .ToList();

                    _mastersToBePresent = masterWeights
                        .Where(mw => mw.Score > 1)
                        .Select(mw => mw.Name)
                        .Distinct()
                        .ToList();

                    if (Settings.DebugMode)
                        LogMessage("Jobs to gain intelligence: " + string.Join(", ", _jobsToGainIntelligence), 5, Color.Green);

                    if (Settings.DebugMode)
                        LogMessage("Masters to be present: " + string.Join(", ", _mastersToBePresent), 5, Color.Green);



                    // CHECK IF BAD JOBS ARE FULL

                    var countBadMastersInBadJobs = ListBetrayalMasters
                        .Count(bm => _jobsToNOTGainIntelligence.Contains(bm.Job) && bm.Score == 1);

                    if (Settings.DebugMode)
                        LogMessage("countBadMastersInBadJobs " + countBadMastersInBadJobs, 5, Color.Coral);

                    bool badJobsAreFull = countBadMastersInBadJobs == 10;

                    // then get where he could be
                    if (Settings.DebugMode)
                        LogMessage("The event master is " + _eventMaster, 5, Color.Yellow);
                    var auxRank = _eventMaster.Rank;
                    if (auxRank == 0)
                        auxRank = 1;
                    if (Settings.DebugMode)
                        LogMessage("The auxrank is " + auxRank.ToString(), 5, Color.Yellow);
                    var filteredMasters = masterWeights.Where(master => master.Name == _eventMaster.Name && master.Rank == auxRank).OrderByDescending(master => master.Score); ;
                    var maxScore = filteredMasters.Max(master => master.Score);
                    // Display the filtered results

                    var auxColorEvent = Color.White;

                    //LogMessage("The event master score is " + _eventMaster.Score.ToString(), 5, Color.Yellow);
                    //LogMessage("maxScore " + maxScore.ToString(), 5, Color.Yellow);


                    // event masters

                    _eventMasters = BetrayalEventMembers();

                    foreach (var eventMasterAux in _eventMasters)
                    {
                        if (Settings.DebugMode)
                            LogMessage("Event Masters !!! " + eventMasterAux.ToString(), 5, Color.DarkGoldenrod);
                    }

                    // draw jobs below event master
                    foreach (var master in filteredMasters)
                    {
                        if (master.Job == _eventMaster.Job && _eventMaster.Score < maxScore)
                        {
                            auxColorEvent = Color.Yellow;
                        }
                        else if (master.Job == _eventMaster.Job && _eventMaster.Score == maxScore)
                        {
                            auxColorEvent = Color.Green;
                        }
                        else
                        {
                            auxColorEvent = Color.White;
                        }
                        drawTextVector3 = Graphics.DrawText(master.ToStringJobScore(), newWindowVector, auxColorEvent);
                        newWindowVector.Y += 10;
                    }


                    if (Settings.DebugMode)
                        LogMessage("The event is " + _eventAction.ToString(), 5, Color.Yellow);


                    var highestScoringEntry = masterWeights
                        .Where(mw => mw.Name == _eventMaster.Name && mw.Score > _eventMaster.Score)
                        .OrderByDescending(mw => mw.Score)
                        .FirstOrDefault();

                    //  master needs to leave

                    var _eventMasterNeedsToLeaveCurrentJob = false;
                    var isJobOfInterest = masterWeights.Any(mw => mw.Job == _jobEvent && mw.Score > 1);
                    var masterHasInterestOnJob = masterWeights.Any(mw => mw.Job == _jobEvent && mw.Score >= 1 && mw.Name == _eventMaster.Name && mw.Rank == 4);
                    var mastersWithThisJobAsInterest = masterWeights.Where(mw => mw.Job == _jobEvent && mw.Score > 1 && mw.Rank == 4);
                    var numMastersWithThisJobAsInterest = mastersWithThisJobAsInterest.Count();
                    var numInterestMastersCurrentlyOnThisJob = ListBetrayalMasters.Count(mw => mw.Job == _jobEvent && mw.Score > 1);
                    var numMastersCurrentlyOnThisJob = ListBetrayalMasters.Count(mw => mw.Job == _jobEvent);
                    //var noSpaceForThisMaster = (numMastersCurrentlyOnThisJob + (numMastersWithThisJobAsInterest - numInterestMastersCurrentlyOnThisJob) - 2) > 0;

                    var noSpaceForThisMaster = numMastersCurrentlyOnThisJob >= 2 || (numMastersCurrentlyOnThisJob - numInterestMastersCurrentlyOnThisJob) >= 2;




                    if (Settings.DebugMode)
                    {
                        LogMessage($"Masters at {_jobEvent}: {numMastersCurrentlyOnThisJob} | masters correctly at this job : {numInterestMastersCurrentlyOnThisJob} | total masters that should be here {numMastersWithThisJobAsInterest} ", 5, Color.Yellow);
                    }

                    // if master has no interest on this job and this is a job where other masters have interest in
                    // and number of masters that should be here 

                    // needs to leave if there is no space for interest masters
                    // 


                    List<Outcome> _eventOutcomes = new List<Outcome>();

                    // handle interrogation outcomes


                    _eventOutcomes.Add(new Outcome(OutcomeType.Nothing, ActionType.Release, 0, $"Release"));

                    if (!masterHasInterestOnJob && isJobOfInterest && noSpaceForThisMaster && _jobEvent == _eventMaster.Job && _eventMaster.Job != "None")

                    {
                        _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromInterestingJobAndNeedSpace, ActionType.Interrogate, 50, $"{_eventMaster.Name} is going to get demoted "));
                                              

                        //Graphics.DrawFrame(_InterrogateButton, Color.Green, 6);
                        if (Settings.DebugMode)
                        {
                            LogMessage("This master has no interest on this job and space is needed for others" + string.Join(", ", mastersWithThisJobAsInterest.Select(mw => mw.Name)), 5, Color.Yellow);
                        }
                    }
                    else if (_eventMaster.Job != "None")
                    {
                        if (_eventMaster.Score >= 1)
                        {
                            _eventOutcomes.Add(new Outcome(OutcomeType.RemoveGoodMemberFromBadJob, ActionType.Interrogate, -50, $"{_eventMaster.Name} is bad and is on a bad job, going to get demoted/removed"));

                        }
                        else
                        {
                            _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Interrogate, 50, $"{_eventMaster.Name} is good but is on a bad job, going to get demoted/removed"));

                        }
                    }
                    else
                    {
                        _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Interrogate, -50, $"{_eventMaster.Name} he has no job so there is no point interrogating him"));

                    }
                    //else
                    if (true)
                    {


 



                        switch (_eventAction)
                        {
                            case "StealIntelligence":

                                _eventOutcomes.AddRange(StealEventRelationship());

                                ;


                                break;
                            case "PromoteNPC":


                                _eventOutcomes.AddRange(PromoteNPC());


                                break;
                            case "Execute":

                                Match matchExecute = Regex.Match(_eventText, @"moves to (\w+)");
                                string jobToMove = matchExecute.Success ? matchExecute.Groups[1].Value : null;
                                var shouldNotMove = false;
                                if (Settings.DebugMode)
                                    LogMessage("Event text is   " + _eventText, 5, Color.Yellow);
                                if (jobToMove is not null)
                                {

                                    var numMastersCurrentlyOnThisMoveJob = ListBetrayalMasters.Count(mw => mw.Job == jobToMove);
                                    var mastersWithThisMoveJobAsInterest = masterWeights.Where(mw => mw.Job == jobToMove && mw.Score > 1 && mw.Rank == 4);
                                    var numMastersWithThisMoveJobAsInterest = mastersWithThisMoveJobAsInterest.Count();
                                    var numInterestMastersCurrentlyOnThisMoveJob = ListBetrayalMasters.Count(mw => mw.Job == jobToMove && mw.Score > 1);
                                    //var noMoveSpaceForThisMaster = (numMastersCurrentlyOnThisMoveJob + (numMastersWithThisMoveJobAsInterest - numInterestMastersCurrentlyOnThisMoveJob) - 2) > 0;

                                    var noMoveSpaceForThisMaster = numMastersCurrentlyOnThisMoveJob >= 2 || (numMastersCurrentlyOnThisMoveJob - numInterestMastersCurrentlyOnThisMoveJob) >= 2;
                                    /// 2 -2 + 2
                                    if (Settings.DebugMode)
                                    {
                                        LogMessage($"Masters at {jobToMove}: {numMastersCurrentlyOnThisMoveJob} | masters correctly at this job : {numInterestMastersCurrentlyOnThisMoveJob} | total masters that should be here {numMastersWithThisMoveJobAsInterest} ", 5, Color.Yellow);
                                        LogMessage($"{numMastersCurrentlyOnThisMoveJob} | {numMastersWithThisMoveJobAsInterest} | {numInterestMastersCurrentlyOnThisMoveJob} ", 5, Color.Aqua);
                                        LogMessage($" masterHasInterestOnJob: {masterHasInterestOnJob} isJobOfInterest: {isJobOfInterest} noMoveSpaceForThisMaster: {noMoveSpaceForThisMaster} ", 5, Color.Aqua);
                                    }
                                    if (!masterHasInterestOnJob && isJobOfInterest && noMoveSpaceForThisMaster)
                                    {
                                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -100, $"{_eventMaster.Name} who is bad, gets moved to interesting job "));

                                        shouldNotMove = true;
                                        if (Settings.DebugMode)
                                        {
                                            LogMessage("Move - This master has no interest on this job and space is needed for others" + string.Join(", ", mastersWithThisMoveJobAsInterest.Select(mw => mw.Name)), 5, Color.Yellow);
                                        }
                                    } else if (masterHasInterestOnJob && isJobOfInterest)
                                    {
                                        shouldNotMove = true;
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 100, $"{_eventMaster.Name} who is GOOD, gets moved to interesting job "));


                                    }
                                }


                                if (shouldNotMove)
                                {
                                    // was already interrogated 
                                }


                                // if he can move to correct safehouse


                                // bad member moving to bad safehouse

                                // good member moving to good safehouse

                                

                                //else if (IsOnTheWrongSafeHouse(highestScoringEntry, _eventMaster, _InterrogateButton, _SpecialButton, _ReleaseButton, jobToMove, _eventText))
                                else if (_eventMaster.Score == 0 )// he is unnalocated
                                {
                                    var potentialScore = GetScore(_eventMaster.Name, jobToMove, 1);
                                    if (potentialScore == 1)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 100, $"{_eventMaster.Name} who is bad gets added to bad job "));

                                    }
                                    else if (potentialScore >= 30)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddGoodMemberToBadJob, ActionType.Special, 100, $"{_eventMaster.Name} who is GOOD gets added to INTERESTING job "));

                                    }

                                }
                                else

                                 if (_eventMaster.Score >= 30)
                                {
                                    _eventOutcomes.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, 50, $"{_eventMaster.Name} who is interesting and on the correct job gets promoted "));


                                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                                }

                                else if (_eventMaster.Score == 1)
                                {
                                    //Graphics.DrawFrame(_ReleaseButton, Color.Yellow, 6);
                                    if (isJobOfInterest)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInInterestingJob, ActionType.Special, -100, $"{_eventMaster.Name} who is bad and is on interesting job gets promoted "));

                                    }
                                    else
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInBadJob, ActionType.Special, -50, $"{_eventMaster.Name} who is bad and is on bad job gets promoted "));
                                    }
                                    

                                }
                                else


                                {
                                    _eventOutcomes.Add(new Outcome(OutcomeType.PromoteGoodMemberInBadJob, ActionType.Special, -100, $"{_eventMaster.Name} who is good and is on bad job gets promoted "));

                                }





                                break;
                            case "SwapNPCJob":
                                // example  It That Fled moves to Intervention. Haku moves to Research
                                _eventOutcomes.AddRange(EventSwap(syndicatePanel));
                                break;
                            case "SwapLeader":
                                _eventOutcomes.AddRange(SwapLeaderRelationship());
                                break;
                            case "RemoveNPCFromOrg":
                                _eventOutcomes.AddRange(RemoveNPCFromOrg());
                                break;
                            case "NPCBefriendsAnother":
                                _eventOutcomes.AddRange(EventRelationship());
                               


                                break;
                            case "GainIntelligenceLarge":
                                _eventOutcomes.AddRange(IntelligenceAction());
                                break;
                            case "GainIntelligence":

                                _eventOutcomes.AddRange(IntelligenceAction());
                                break;
                            case "NPCLeavesOrg":
                                //Console.WriteLine("Number is 1");

                                _eventOutcomes.AddRange(NPCLeavesOrg(_SpecialButton, _ReleaseButton, _mastersToBePresent, _eventText, _missingMastersFromBoard, _eventAction));

                                break;
                            case "StealRanks":
                                _eventOutcomes.AddRange(StealRanks());
                                break;
                            case "RemoveAllFromPrison":

                                //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                                break;

                                // esta logica era se não quisessemos libertar, mas se eles já estão presos, já vão ganhar intelligence, logo agilizamos
                                var pattern = @"\+\d+\s+(\w+)\s+Intelligence";
                                var matches = Regex.Matches(_eventText, pattern);
                                var elements = new List<string>();
                                foreach (Match match in matches)
                                {
                                    if (match.Groups.Count > 1)
                                    {
                                        elements.Add(match.Groups[1].Value);
                                    }
                                }




                                if (elements != null)
                                {


                                    var invalidJobs = elements.Where(element => !_jobsToGainIntelligence.Contains(element)).ToList();
                                    if (invalidJobs is null)
                                    {
                                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                                    }
                                    else
                                    {
                                        if (Settings.DebugMode)
                                            LogMessage(" We dont want to gain intelligence for these  " + string.Join(", ", invalidJobs), 5, Color.Red);
                                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                    }

                                }
                                break;
                            case "DestroyAllItemsInDivision":


                

                                _eventOutcomes.Add(new Outcome(OutcomeType.DestroyEquipment, ActionType.Special, 10, $"Destroy division items"));
                                


                                break;
                            case "RemoveAllRivalries":
                                //Console.WriteLine("Number is 1");
                                //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                _eventOutcomes.Add(new Outcome(OutcomeType.RemoveRivalries, ActionType.Special, -50, $"Remove all rivalries"));
                                

                                break;
                            case "RemoveAllRivalriesInDivision":
                                //Console.WriteLine("Number is 1");
                                //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);

                                // only remove if there are bad rivalries of any member on this division

                                _eventOutcomes.Add(new Outcome(OutcomeType.RemoveRivalriesInDivision, ActionType.Special, -50, $"Remove all rivalries in division"));
                                break;
                            case "DestroyAllItemsOfRivalDivision":
                                // se o event master for alguém com mais de 20 de score -> do it

                                // betray member 1
                                // betray member 2
                                string DestroyPattern = @"Destroy all Equipment of (Research|Intervention|Transportation|Fortification) members";
                                Match DestroyMatch = Regex.Match(_eventText, DestroyPattern);
                                if (DestroyMatch.Success)
                                {
                                    var capturedDivision = DestroyMatch.Groups[1].Value;
                                    LogMessage($"Captured division: {capturedDivision}", 5, Color.DarkGoldenrod);


                                    // betray member1 is eventMaster

                                    // betray member 2 is the one from the eventMasters that isnt eventmaster

                                    var potentialBetrayedMembers = _eventMasters.Where(master => master.Name != _eventMaster.Name && master.Job == capturedDivision);
                                    // if potentialBetrayedMembers == 1, we're sure it is him
                                    if (potentialBetrayedMembers.Count() == 1)
                                    {
                                        var betrayedMember = potentialBetrayedMembers.FirstOrDefault();
                                        var betrayedMemberDream = masterWeights
                                          .Where(master => master.Name == betrayedMember.Name)
                                          .OrderByDescending(master => master.Score)
                                          .FirstOrDefault();

                                        if (_eventMaster.Score >= 30 && betrayedMember.Score >= 1) // if both are on correct placement and at least one of them is interesting
                                        { 
                                            _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"(INTERESTING){_eventMaster.Name} {betrayedMember.Name} Become Rivals"));
                                        }
                                        else if (_eventMaster.Score >= 1 && betrayedMember.Score >= 30) // if both are on correct placement and at least one of them is interesting
                                        {
                                            _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{_eventMaster.Name} (INTERESTING){betrayedMember.Name} Become Rivals"));
                                        }
                                        else if (_eventMaster.Score >= 30 && betrayedMemberDream.Score >= 30) // if one is correct and the other is incorrect but correct placement is the same job as the one that is correct and either score is >= 30 (either the one that is correct or the potential placement) 

                                        {
                                            _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{_eventMaster.Name} {betrayedMember.Name} Become Rivals and {betrayedMember.Name} gets closer to dream job "));
                                        }
                                    }
                                    else
                                    {


                                    }
                                    // else


                                    

                                }

                                
                                // if both are on correct placement and at least one of them is interesting



                                
                 
                                
                                
                                break;
                            case "DownrankRivalsUprankMyDivision":
                                //Console.WriteLine("Number is 1");

                                // get encounter target _eventMaster
                                // get upwards division

                                // get downwards divison
                                Match matchDownrankRivalsUpgradeMyDivison = Regex.Match(_eventText, @"\+1 rank to (\w+) members\. -1 rank to (\w+) members");

                                if (matchDownrankRivalsUpgradeMyDivison.Success)
                                {
                                    string divisionToGainRank = matchDownrankRivalsUpgradeMyDivison.Groups[1].Value;
                                    string divisionToLoseRank = matchDownrankRivalsUpgradeMyDivison.Groups[2].Value;
                                    if (Settings.DebugMode)
                                    {
                                        LogMessage($"Division to up {divisionToGainRank} division to down {divisionToLoseRank}", 5, Color.Yellow);
                                        LogMessage($"_eventMasterScore {_eventMaster.Score} jobsToGainContainsUpDivision {_jobsToGainIntelligence.Contains(divisionToGainRank)}" +
                                            $"jobsToGainDontContainsDownDivision {!_jobsToGainIntelligence.Contains(divisionToLoseRank)} " +
                                            $"Any master to lose rank that is currently below 2 {ListBetrayalMasters.Any(master => master.Job == divisionToLoseRank && master.Rank < 2)} " +
                                            $"", 5, Color.Yellow);
                                    }
                                    if (_eventMaster.Score >= 30
                                        && _jobsToGainIntelligence.Contains(divisionToGainRank)
                                        && !_jobsToGainIntelligence.Contains(divisionToLoseRank)
                                        && !ListBetrayalMasters.Any(master => master.Job == divisionToLoseRank && master.Rank < 2) // no member will be removed from the division because their rank will be at least 1 after
                                        )
                                    {
                                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);

                                        _eventOutcomes.Add(new Outcome(OutcomeType.UprankMyDivisionDownwardsOther, ActionType.Special, 100, $"Uprank my division, downward other"));
                                        _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 10, $" {_eventMaster.Name} is interesting and rivals someone"));
                                    }
                                    else
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.UprankMyDivisionDownwardsOther, ActionType.Special, -50, $"Uprank my division, downward other"));
                                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                    }
                                }
                                else
                                {
                                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                }



                                // if _eventMaster is person of interest score >= 30 && upwards division = division of interest && downwards divison != division of interest
                                // and all rmembers in downward division are score > 1 i.e. não existe nenhum na downward division com rank < 2







                                break;
                            case "GainItemAnyUnique":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainUniqueItem, ActionType.Special, 10, $"Gain unique"));
                                break;
                            case "GainItemCurrency":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainCurrencyItem, ActionType.Special, 10, $"Gain currency"));
                                break;
                            case "GainItemVeiledItem":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainVeiledItem, ActionType.Special, 10, $"Gain Veiled Item"));
                                break;
                            case "GainItemMap":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainMap, ActionType.Special, 10, $"Gain Map"));
                                break;
                            case "GainItemScarab":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainScarab, ActionType.Special, 10, $"Gain Scarab"));
                                break;
                            default:
                                ////Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                                if (Settings.DebugMode)
                                    LogMessage("Default  ", 5, Color.Red);
                                break;
                        }
                    }

                    foreach(var eventOutcome in _eventOutcomes)
                    {
                        if (Settings.DebugMode)
                        {
                            LogMessage(eventOutcome.ToString(), 5, Color.Aqua);
                        }
                    }

                    PrintHighestScoringActionType(_eventOutcomes);  





                }

            }

        }
        private List<Outcome> NPCLeavesOrg(RectangleF _SpecialButton, RectangleF _ReleaseButton, List<string> mastersToBePresent, string _eventText, List<string> missingMastersFromBoard, string _eventAction)
        {

            string jobToCheck;
            List<Outcome> eventOutcome = new List<Outcome>();
            if (Settings.DebugMode)
                LogMessage("Event is   " + _eventText, 5, Color.Yellow);


            // bool any masters to be present are unnalocated

            bool missingImportantFromBoard = missingMastersFromBoard.Any(name => mastersToBePresent.Contains(name));

            if (Settings.DebugMode)
            {
                LogMessage("Unnalocated names: " + string.Join(", ", missingMastersFromBoard));
                LogMessage("Masters to be present: " + string.Join(", ", mastersToBePresent));
                LogMessage("Are we missing important from board " + missingImportantFromBoard.ToString(), 5, Color.Yellow);

            }
            // bool person removed isnt master to be present



            // get the master that is being removed




            // when it is NPCLeavesOrg

            if (_eventAction == "NPCLeavesOrg")
            {

                Match match = Regex.Match(_eventText, @"(.+?)\s+removed from the Immortal Syndicate");

                if (match.Success)
                {
                    var nameToBeRemoved = match.Groups[1].Value; // Extracted word
                    if (Settings.DebugMode)
                        LogMessage("Element to be removed is  " + nameToBeRemoved, 5, Color.Blue);
                        LogMessage("Len of Element to be removed is  " + nameToBeRemoved.Length.ToString(), 5, Color.Blue);

                    // if the master is not important remove them 
                    eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromOrg, ActionType.Special, -10, $"{nameToBeRemoved} is not an interesting member and is going to be removed "));

                    if (!mastersToBePresent.Contains(nameToBeRemoved) && missingImportantFromBoard)
                    {
                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);

                        // if job is interesting
                       // if (_jobsToGainIntelligence.Contains())

                        //else


                        eventOutcome.Add(new Outcome(OutcomeType.BringNewMemberInOrgGoodMemberOutsideForInterestingJob,ActionType.Special, 50, $"{nameToBeRemoved} is not an interesting member and is going to be removed AND WE MIGHT BRING A GOOD IN"));
                    }
                    else
                    {
                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    }

                }
            }

            //foreach (var a in eventOutcome)
            //{
            //    if (Settings.DebugMode)
            //    {
            //        LogMessage("Inside event code " + a.ToString(), 5, Color.Aqua);
            //    }
            //}


            return eventOutcome;


        }
        private List<Outcome> RemoveNPCFromOrg()
        {
            
            string jobToCheck;
            if (Settings.DebugMode)
                LogMessage("Event is   " + _eventText, 5, Color.Yellow);


            // bool any masters to be present are unnalocated

            bool missingImportantFromBoard = _missingMastersFromBoard.Any(name => _mastersToBePresent.Contains(name));
            if (Settings.DebugMode)
            {
                LogMessage("Event is   " + _eventText, 5, Color.AntiqueWhite);
                LogMessage("Unnalocated names: " + string.Join(", ", _missingMastersFromBoard), 5, Color.AntiqueWhite);
                LogMessage("Masters to be present: " + string.Join(", ", _mastersToBePresent), 5, Color.AntiqueWhite);
                LogMessage("Are we missing important from board " + missingImportantFromBoard.ToString(), 5, Color.AntiqueWhite);
            }

            List<Outcome> eventOutcome = new List<Outcome>();



            // when it is NPCLeavesOrg

            if (_eventAction == "RemoveNPCFromOrg")
            {

                Match match = Regex.Match(_eventText, @"(.+?)\s+removed from the Immortal Syndicate");

                if (match.Success)
                {
                    var nameToBeRemoved = match.Groups[1].Value; // Extracted word
                    if (Settings.DebugMode)
                        LogMessage("Element to be removed is  " + nameToBeRemoved, 5, Color.Blue);

                   

                    // if the master is not important remove them 

                    eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromOrg,ActionType.Special, -10, $"{nameToBeRemoved} is not an interesting member and is going to be removed"));
                    if (!_mastersToBePresent.Contains(nameToBeRemoved) && missingImportantFromBoard)
                    {
                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                        
                        eventOutcome.Add(new Outcome(OutcomeType.BringNewMemberInOrgGoodMemberOutsideForInterestingJob, ActionType.Special, 50, $"{nameToBeRemoved} is not an interesting member and is going to be removed"));
                        
                    }
                    else
                    {
                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    }

                }
            }


            return eventOutcome;

        }

        private List<Outcome> IsOnTheWrongSafeHouse(MasterWeight highestScoringEntry, string jobToMove)
        {


            List<Outcome> eventOutcome = new List<Outcome>();
            Match matchExecute2 = Regex.Match(_eventText, @"\.\s*(.+?)\s+moves to (\w+)");
            var name = "";
            var destination = "";
            bool okToExecute = false;
            if (matchExecute2.Success)
            {
                name = matchExecute2.Groups[1].Value;        // "Cameria" or "It That Fled"
                destination = matchExecute2.Groups[2].Value; // "Intervention"
                                                             //LogMessage("Proposed job" + destination + " for " + name, 5, Color.Yellow);

                if (Settings.DebugMode)
                    LogMessage("parsed name  " + name, 5, Color.Yellow);
                var proposedJob = masterWeights.FirstOrDefault(master => master.Name.Contains(name));


                if (proposedJob != null)
                {
                    if (proposedJob.Score >= highestScoringEntry.Score)
                    {
                        okToExecute = true;
                    }

                }
                else
                {
                    if (Settings.DebugMode)
                        LogMessage("proposed job null", 5, Color.Yellow);
                }

            }


            if (highestScoringEntry != null)
            {
                if (Settings.DebugMode)
                {
                    LogMessage("This master could go to  " + highestScoringEntry.Job + " with a score of " + highestScoringEntry.Score, 5, Color.Yellow);
                }
                if (highestScoringEntry.Score == _eventMaster.Score)
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 100, $"{_eventMaster.Name} who is bad gets moved to bad job "));

                }
                else if (highestScoringEntry.Score > _eventMaster.Score && _eventMaster.Rank == 0 && jobToMove == highestScoringEntry.Job)
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                }
                else if (_eventMaster.Rank > 0)
                {
                    //Graphics.DrawFrame(_InterrogateButton, Color.Green, 6);
                }
                else if (okToExecute)
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                }
                else
                {


                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                }
                //return true;
            }
            //return false;


            return eventOutcome;
        }
        private List<Outcome> IntelligenceAction()
        {
            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, @"\d+\s+(\w+)\s+\w+");
            string jobToCheck;
            if (Settings.DebugMode)
                LogMessage("Event is   " + _eventText, 5, Color.Yellow);
            if (match.Success)
            {
                jobToCheck = match.Groups[1].Value; // Extracted word
                if (Settings.DebugMode)
                    LogMessage("Event Job is   " + jobToCheck, 5, Color.Blue);

                if (_jobsToGainIntelligence.Contains(jobToCheck))
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, 40, $" Gain intelligence to {jobToCheck} "));


                }
                else
                {
                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, -100, $" Gain intelligence to {jobToCheck} "));

                }


            }
            return eventOutcome;
        }
        private List<Outcome> EventRelationship()
        {
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become (\w+)";

            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string firstName = match.Groups[1].Value;
                string secondName = match.Groups[2].Value;
                string type = match.Groups[3].Value;
                var member1 = ListBetrayalMasters
                   .Where(master => master.Name == firstName)
                   .OrderByDescending(master => master.Score)
                   .FirstOrDefault();

                var member2 = ListBetrayalMasters
                    .Where(master => master.Name == secondName)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();
                var wannabemember1 = masterWeights
                   .Where(master => master.Name == firstName)
                   .OrderByDescending(master => master.Score)
                   .FirstOrDefault();

                var wannabemember2 = masterWeights
                    .Where(master => master.Name == secondName)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                if (Settings.DebugMode)
                {
                    LogMessage($"relation type {type}", 5, Color.Yellow);
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                }
                if (member1 != null && member2 != null)
                {
                    if (((member1.Score + member2.Score) < 20) && (type == "Neutral")) // they are both bad and the event is to make them neutral
                    {
                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                        if (Settings.DebugMode)
                            LogMessage("they are both bad and the event is to make them neutral " + member2, 5, Color.Green);

                        eventOutcome.Add(new Outcome(OutcomeType.NeutralBetweenBadMembers, ActionType.Special, 50, $"{member1} {member2} become neutral "));

                    } 
                    else if (((member1.Score + member2.Score) > 30) && (type != "Neutral")) // at least one of them is good, so we want a relationship
                    {
                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                        if (Settings.DebugMode)
                            LogMessage("at least one of them is good, so we want a relationship " + member2, 5, Color.Green);
                        if(type == "Trusted")
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.FriendlyBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1.Name} {member2.Name} become friends "));
                        }
                        else if (type == "Rivals")
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1.Name} {member2.Name} become rivals "));
                        }
                        
                    }
                    else
                    // if member is on the wrong place


                    {
                        if (type == "Trusted")
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.FriendlyBetweenBadMembers, ActionType.Special, -50, $"{member1.Name} {member2.Name} become friends "));
                        }
                        else if (type == "Rivals")
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenBadMembers, ActionType.Special, -50, $"{member1.Name} {member2.Name} become rivals "));
                        }
                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    }

                }
            }

            return eventOutcome;
        }

        private List<Outcome> StealEventRelationship()
        {

            string pattern = @"\+\d+ (\w+) Intelligence\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";

            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string encounterType = match.Groups[1].Value;
                string member1name = match.Groups[2].Value;
                string member2name = match.Groups[3].Value; ;
                var member1 = ListBetrayalMasters
                   .Where(master => master.Name == member1name)
                   .OrderByDescending(master => master.Score)
                   .FirstOrDefault();

                var member2 = ListBetrayalMasters
                    .Where(master => master.Name == member2name)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                if (Settings.DebugMode)
                {
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                    LogMessage("Safehouse intelligence is   " + encounterType, 5, Color.Blue);

                }





                if (((member1.Score + member2.Score) > 30) && _jobsToGainIntelligence.Contains(encounterType)) // at least one of them is good, so we want a relationship
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    if (Settings.DebugMode)
                        LogMessage("at least one of them is good, so we want a relationship ALSO GOOD SAFEHOUSE " + member2, 5, Color.Green);

                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special,100, $"{member1.Name} {member2.Name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, 100, $" Gain intelligence to {encounterType} "));

                    


                }
                else if (((member1.Score + member2.Score) > 30)) // at least one of them is good, so we want a relationship
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Yellow, 6);
                    if (Settings.DebugMode)
                        LogMessage("at least one of them is good, so we want a relationship " + member2, 5, Color.Yellow);

                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1.Name} {member2.Name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, -50, $" Gain intelligence to {encounterType} "));

                }
                else
                // if member is on the wrong place


                {
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenBadMembers, ActionType.Special, -100, $"{member1.Name} {member2.Name} become rivals "));

                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                }


            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }

            return eventOutcome;
        }
        private List<Outcome> StealRanks()
        {

            List<Outcome> eventOutcome = new List<Outcome>();
            string pattern = @"\+\d+ rank\(s\) to ([A-Za-z]+(?: [A-Za-z]+)*)\. ([A-Za-z]+(?: [A-Za-z]+)*) loses all ranks\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";





            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string gainRankMembername = match.Groups[1].Value;
                string loseRankMembername = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                var member1 = masterWeights
                      .Where(master => master.Name == member1name)
                      .OrderByDescending(master => master.Score)
                      .FirstOrDefault();

                var member2 = masterWeights
                    .Where(master => master.Name == member2name)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                var gainRankMember = ListBetrayalMasters.FirstOrDefault(master => master.Name == gainRankMembername);
                var loseRankMember = ListBetrayalMasters.FirstOrDefault(master => master.Name == loseRankMembername);

                var loseRankMemberBestJob = masterWeights.Where(mw => mw.Score >= 30 && mw.Name == loseRankMember.Name)
                                            .Select(mw => mw.Job)
                                            .Distinct()
                                            .FirstOrDefault();

                if (Settings.DebugMode)
                {
                    LogMessage("loseRankMemberBestJob  " + loseRankMemberBestJob, 5, Color.Yellow);
       


                }
                
                if (gainRankMember.Score >= 30 && loseRankMember.Score >= 30 && badJobsAreFull) // they are both interesting jobs with good members but bad jobs are full so its safe
                {
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1name} {member2name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1name} {member2name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, 100, $"{gainRankMember.Name} Gains one rank"));
                } else 


                if (gainRankMember.Score >= 30 && loseRankMember.Score == -1 && loseRankMemberBestJob == gainRankMember.Job)
                {
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1name} {member2name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.RemoveGoodMemberFromBadJob, ActionType.Special, 100, $"{loseRankMember.Name} Loses all ranks from his bad job"));
                    eventOutcome.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, 100, $"{gainRankMember.Name} Gains one rank"));

                }
                // I only want to do if gain rank score is > 30 and lose score is <1 bestjob for lose rank == gainrank job  

                // gain good and lose bad

                // gain bad and lose good


                // gain good and lose good

                // gain bad and lose bad

                if (Settings.DebugMode)
                {
                    LogMessage("gainRankMember 1  " + gainRankMember, 5, Color.Yellow);
                    LogMessage("loseRankMember " + loseRankMember, 5, Color.Yellow);


                }






                if (((gainRankMember.Score + loseRankMember.Score) > 30) && loseRankMember.Score <= -1) // at least one of them is good, so we want a relationship
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    if (Settings.DebugMode)
                        LogMessage("at least one of them is good, so we want a relationship " + member2, 5, Color.Green);
                }
                else
                // if member is on the wrong place


                {
                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                }


            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }

            return eventOutcome;
        }
        private List<Outcome> PromoteNPC()
        {
            List<Outcome> eventOutcome = new List<Outcome>();
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*) moves to ([A-Za-z]+(?: [A-Za-z]+)*)\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become (\w+)";




            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {

                string movingMemberName = match.Groups[1].Value;
                string destination = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                string relationshipType = match.Groups[5].Value;
                var member1 = ListBetrayalMasters
                      .Where(master => master.Name == member1name)
                      .OrderByDescending(master => master.Score)
                      .FirstOrDefault();

                var member2 = ListBetrayalMasters
                    .Where(master => master.Name == member2name)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                var movingmember = ListBetrayalMasters.FirstOrDefault(master => master.Name == movingMemberName);
                

                var matchingMember = masterWeights.FirstOrDefault(w => w.Job == destination && w.Name == movingMemberName);

                





                if (Settings.DebugMode)
                {
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                    LogMessage("moving member " + movingmember, 5, Color.Yellow);


                }



                if (matchingMember is not null)
                {

                    if(matchingMember.Score >= 30)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 100, $"{matchingMember.Name} Moves to {destination} "));

                    }
                    else if (matchingMember.Score >=1)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 50, $"{matchingMember.Name} Moves to {destination} "));

                    }else if (matchingMember.Score == -1)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -100, $"{matchingMember.Name} Moves to {destination} "));

                    }

                    if (member1.Score >= 30 && member2.Score >= 30)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.FriendlyBetweenGoodInterestingMember, ActionType.Special, 200, $"{member1.Name} {member2.Name} become friends "));

                    }
                    else if ((member1.Score >= 30 && member2.Score >= 1 )|| (member1.Score >= 1 && member2.Score >= 30))
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.FriendlyBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1.Name} {member2.Name} become friends "));

                    }
                    else if ((member1.Score >= 30 && member2.Score >= -1 && GetScore(member2.Name,member1.Job,1) >=1) || (member1.Score >= -1 && member2.Score >= 30 && GetScore(member1.Name, member2.Job, 1) >= 1))
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.FriendlyBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1.Name} {member2.Name} become friends "));

                    }
                    else
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.FriendlyBetweenGoodInterestingMember, ActionType.Special, -100, $"{member1.Name} {member2.Name} become friends "));

                    }

                   
                }






            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }

            return eventOutcome;
        }
        private List<Outcome> SwapLeaderRelationship()
        {

            List<Outcome> eventOutcome = new List<Outcome>();
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*) becomes leader of ([A-Za-z]+(?: [A-Za-z]+)*)\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";


            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string newLeaderName = match.Groups[1].Value;
                string safehouse = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                var member1 = masterWeights
                      .Where(master => master.Name == member1name)
                      .OrderByDescending(master => master.Score)
                      .FirstOrDefault();

                var member2 = masterWeights
                    .Where(master => master.Name == member2name)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                var newLeader = ListBetrayalMasters.FirstOrDefault(master => master.Name == newLeaderName);

                if (Settings.DebugMode)
                {
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                    LogMessage("Safehouse is   " + safehouse, 5, Color.Blue);
                    LogMessage("New leader is  is   " + newLeader, 5, Color.Blue);

                }


                // 
                if ( (member1.Score >= 30 && member2.Score >= 1) || (member1.Score >= 1 && member2.Score >= 30)) // if both are on good positions and one of them is interesting
                {
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1name} {member2name} become rivals "));

                }
                else
                {

                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenBadMembers, ActionType.Special, -100, $"{member1name} {member2name} become rivals "));


                }



            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }
            return eventOutcome;
        }
        private List<Outcome> EventSwap(SyndicatePanel syndicatePanel)
        {
            var _eventText = syndicatePanel.BetrayalEventData.EventText;
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*)\s+moves\s+to\s+(\w+)\.\s+([A-Za-z]+(?: [A-Za-z]+)*)\s+moves\s+to\s+(\w+)";
            Match match = Regex.Match(_eventText, pattern);
            List<Outcome> eventOutcome = new List<Outcome>();
            if (match.Success) // swap
            {
                string firstPerson = match.Groups[1].Value;
                string firstDestination = match.Groups[2].Value;
                string secondPerson = match.Groups[3].Value;
                string secondDestination = match.Groups[4].Value;
                if (Settings.DebugMode)
                {
                    LogMessage($"First Person: {firstPerson}", 5, Color.Blue);
                    LogMessage($"First Destination: {firstDestination}", 5, Color.Blue);
                    LogMessage($"Second Person: {secondDestination}", 5, Color.Blue);
                    LogMessage($"Second Destination: {secondPerson}", 5, Color.Blue);
                }
                List<string> _eventMasters = new List<string>();
                _eventMasters.Add(firstPerson);
                _eventMasters.Add(secondPerson);

                //foreach (var _eventMaster in _eventMasters)
                //{
                //    LogMessage($"event masters: {_eventMaster}", 5, Color.Purple);
                //}

                var filteredMasters = ListBetrayalMasters.Where(master => _eventMasters.Contains(master.Name)).ToList();

                //foreach(var filteredMaster in filteredMasters)
                //{
                //    LogMessage($"masters: {filteredMaster}", 5, Color.Blue);
                //}


                List<BetrayalMaster> NewMasters = new List<BetrayalMaster>();
                NewMasters.Add(
                new BetrayalMaster(filteredMasters[0].Name, filteredMasters[1].Job, filteredMasters[0].Rank, filteredMasters[0].IsLeader, filteredMasters[0].Relationships, GetScore(filteredMasters[0].Name, filteredMasters[1].Job, filteredMasters[0].IsLeader ? filteredMasters[0].Rank + 1 : filteredMasters[0].Rank))
                );
                NewMasters.Add(
                new BetrayalMaster(filteredMasters[1].Name, filteredMasters[0].Job, filteredMasters[1].Rank, filteredMasters[1].IsLeader, filteredMasters[1].Relationships, GetScore(filteredMasters[1].Name, filteredMasters[0].Job, filteredMasters[1].IsLeader ? filteredMasters[1].Rank + 1 : filteredMasters[1].Rank))
                );








                int oldScore = filteredMasters.Sum(master => master.Score);
                int newScore = NewMasters.Sum(master => master.Score);
                var _SpecialButton = syndicatePanel.BetrayalEventData.SpecialButton.GetClientRectCache;
                var _ReleaseButton = syndicatePanel.BetrayalEventData.ReleaseButton.GetClientRectCache;
  

                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    // good swap
                    

                    // the ones that leave
                    foreach (var memberFiltered in filteredMasters)
                    {
                        if (Settings.DebugMode)
                            LogMessage(memberFiltered.ToString(), 5, Color.Red);

                        if(memberFiltered.Score >= 30)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RemoveGoodMemberFromInterestingJob, ActionType.Special, -100, $"{memberFiltered.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else  if (memberFiltered.Score >= 1)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Special, 0, $"{memberFiltered.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else 
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromInterestingJob, ActionType.Special, 50, $"{memberFiltered.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }

                    }
                    // the ones that join
                    foreach (var newMember in NewMasters)
                    {
                        if (Settings.DebugMode)
                            LogMessage(newMember.ToString(), 5, Color.Green);

                        if (newMember.Score >= 30)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 100, $"{newMember.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else if (newMember.Score >= 1)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 0, $"{newMember.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -60, $"{newMember.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                    }
                
               





            }
            return eventOutcome;
        }



        public int GetScore(string name, string job, int rank)
        {
            // Assuming masterWeights is a list containing MasterWeight objects
            // Iterate through the list to find the matching MasterWeight object
            foreach (var masterWeight in masterWeights)
            {
                if (masterWeight.Name == name && masterWeight.Job == job && masterWeight.Rank == rank)
                {
                    return masterWeight.Score;
                }
            }

            // If no matching MasterWeight object found, return a default score
            return 0; // Or any other default value as per your requirement
        }



        public void PrintHighestScoringActionType(List<Outcome> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                LogMessage("No outcomes provided." ,5,Color.BlueViolet);
                return;
            }

            // Group by ActionType and calculate total scores
            var topGroup = outcomes
                .GroupBy(o => o.Action)
                .Select(g => new
                {
                    ActionType = g.Key,
                    TotalScore = g.Sum(o => o.Score),
                    Outcomes = g.ToList()
                })
                .OrderByDescending(g => g.TotalScore)
                .FirstOrDefault();

            if (topGroup == null)
            {
 
                LogMessage("No valid groups found", 5, Color.BlueViolet);
                return;
            }

            LogMessage($"ActionType with highest score: {topGroup.ActionType}, Total Score: {topGroup.TotalScore}", 5, Color.BlueViolet);
            LogMessage("Matching Outcomes:", 5, Color.BlueViolet);

            //foreach (var outcome in topGroup.Outcomes)
            for (int i = 0; i < topGroup.Outcomes.Count; i++)
            {
                LogMessage(i + ": " + topGroup.Outcomes[i].ToString(), 5, Color.BlueViolet); // Ensure Outcome overrides ToString()
            }

            if (topGroup.ActionType == ActionType.Special)
            {
                Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
            }
            else if (topGroup.ActionType == ActionType.Interrogate)
            {
                Graphics.DrawFrame(_InterrogateButton, Color.Green, 6);
            }
            else
            {
                Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
            }
        }




    }
}