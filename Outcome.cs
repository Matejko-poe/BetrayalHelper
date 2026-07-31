using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetrayalHelper
{
    public enum ActionType
    {
        Interrogate,
        Release,
        Special
    }

    public enum OutcomeType
    {
        // job types
        //      interesting
        //      non interesting

        // member types
        //      Good
        //      bad 

        //combination

        // interesting job + good member = gg
        // interesting job + bad member= bad
        // non interesting job + bad member = good
        // non interesting job + good member = bad
        Nothing,
        // add/remove members to board
        RemoveBadMemberFromOrg, // neutral
        RemoveGoodMemberFromOrg, // bad
        BringNewMemberInOrgOnlyBadMembersOutside, // bad
        BringNewMemberInOrgGoodMemberOutsideForNonInterestingJob, // good
        BringNewMemberInOrgGoodMemberOutsideForInterestingJob, // good

        //remove members job
        RemoveBadMemberFromInterestingJob, //good
        RemoveBadMemberFromInterestingJobAndNeedSpace, //mega good
        RemoveBadMemberFromBadJob, //bad

        RemoveGoodMemberFromInterestingJob, // bad
        RemoveGoodMemberFromBadJob, // good

        // add members job
        AddBadMemberToInterestingJob, //bad
        AddBadMemberToBadJob, //good

        AddGoodMemberToInterestingJob, // good
        AddGoodMemberToBadJob, // bad


        // promote

        PromoteGoodMemberInInterestingJob, //good
        PromoteBadMemberInInterestingJob, //bad

        PromoteGoodMemberInBadJob, //bad
        PromoteBadMemberInBadJob, //bad 
        
        // downrank

        DownrankGoodMemberInInterestingJob, //bad
        DownrankBadMemberInInterestingJob, //good

        DownrankGoodMemberInBadJob, //good
        DownrankBadMemberInBadJob, //bad

        // gain info
        GainInformationToInterestingJob, //good
        GainInformationToBadJob, //bad

        // build relationships
        // friendly
        FriendlyBetweenBadMembers, // bad
        FriendlyBetweenGoodInterestingMember, // good

        // rival
        RivalBetweenBadMembers, // bad
        RivalBetweenGoodInterestingMember, // good

        NeutralBetweenBadMembers,
        NeutralWithGoodMember,
        // swap leader
        BringBetterScoredToLeader,

        //uprank my division

        UprankMyDivisionDownwardsOther,

        // remove rivalries
        RemoveRivalriesInDivision,
        RemoveRivalries,

        // bargains
        DestroyEquipment,
        GainVeiledItem,
        GainUniqueItem,
        GainCurrencyItem,
        GainMap,
        GainScarab,
        // 


        
    }

    public class Outcome
    {
        public OutcomeType Type { get; set; } 
        public ActionType Action { get; set; }
        public int Score { get; set; }
        public string Description { get; set; }

        public Outcome(OutcomeType type, ActionType action, int score, string description)
        {
            Action = action;
            Type = type;
            Score = score;
            Description = description;
        }

        public override string ToString()
        {
            return $"{Action} {Type} (Score: {Score}) - {Description}";
        }
    }
}
