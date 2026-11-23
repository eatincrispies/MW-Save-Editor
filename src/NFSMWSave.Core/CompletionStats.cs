namespace NFSMWSave.Core;

public sealed record CompletionResult(int Overall, int Career, int RapSheet, int Challenge, int ChallengeDone, int ChallengeTotal);

public static class CompletionStats
{
    private const int CareerRaces = 169;
    private const int ChallengeGroup = 19;
    private const int RapSheetPoints = 140;
    private const int RankCount = 15;
    private const int LengthRank = 0;
    private const int InfractionRank = 8;
    private const int BountyRank = 9;
    private const string UnusedCareerRace = "4.6.0";
    private const string BurgerKingRace = "19.8.31";
    private const string BlackEditionRace = "19.9.70";

    private static readonly int[][] RapSheetRanks =
    [
        [14400, 13200, 12000, 10800, 9600, 8400, 7200, 6000, 4800, 3600, 3000, 2400, 1800, 1200, 600],
        [1200, 1000, 900, 800, 700, 600, 500, 400, 300, 200, 150, 100, 75, 50, 25],
        [500, 450, 400, 350, 300, 250, 200, 150, 100, 75, 50, 30, 20, 10, 5],
        [250, 225, 200, 175, 150, 125, 100, 75, 50, 25, 20, 15, 10, 8, 5],
        [100, 90, 80, 75, 70, 65, 60, 55, 50, 40, 30, 20, 15, 10, 5],
        [200, 175, 150, 125, 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 5],
        [30, 26, 24, 22, 20, 18, 16, 14, 12, 10, 8, 6, 5, 4, 3],
        [3000000, 2500000, 2000000, 1500000, 1000000, 750000, 500000, 400000, 300000, 200000, 150000, 100000, 75000, 50000, 25000],
        [500, 450, 400, 350, 300, 250, 200, 150, 100, 75, 50, 40, 30, 15, 5],
        [10000000, 8000000, 6000000, 4000000, 3000000, 2000000, 1000000, 750000, 500000, 250000, 150000, 100000, 50000, 10000, 5000],
    ];

    public static CompletionResult? Compute(SaveFile save)
    {
        if (!save.Stats.IsAvailable || !save.Progress.IsReadable)
        {
            return null;
        }
        var careerDone = save.Races.Races.Count(r => r.IsBlacklist && r.Id != UnusedCareerRace && r.Has(RaceFlags.CareerCompleted));
        var hasBlackEdition = save.Map.Platform == SavePlatform.PC || save.Races.Races.Any(r => r.Id == BlackEditionRace && r.Has(RaceFlags.QuickRaceCompleted));
        var series = save.Races.Races.Where(r => r.Group == ChallengeGroup && r.Id != BurgerKingRace && (hasBlackEdition || r.Id != BlackEditionRace)).ToList();
        var challengeDone = series.Count(r => r.Has(RaceFlags.QuickRaceCompleted));

        var points = 0f;
        for (var type = 0; type < RapSheetRanks.Length; type++)
        {
            var movement = RankCount - RankOf(save, type);
            if (movement >= 0)
            {
                points += movement;
            }
        }

        var rapSheet = (int)(points / RapSheetPoints * 100f);
        var career = (int)((careerDone + (float)save.Progress.MilestonesAwarded) / (CareerRaces + (float)save.Progress.MilestoneCount) * 100f);
        var challenge = series.Count == 0 ? 0 : (int)(challengeDone / (float)series.Count * 100f);
        var overall = (int)(career * 0.7f + challenge * 0.2f + rapSheet * 0.1f);
        return new CompletionResult(overall, career, rapSheet, challenge, challengeDone, series.Count);
    }

    private static int RankOf(SaveFile save, int type)
    {
        long value = type switch
        {
            InfractionRank => save.Stable.TotalInfractions,
            BountyRank => save.Stable.TotalBounty,
            _ => save.Stats.CareerValue(type),
        };
        var scale = type == LengthRank ? RapSheet.TicksPerSecond : 1;
        for (var i = 0; i < RankCount; i++)
        {
            if (value >= (long)RapSheetRanks[type][i] * scale)
            {
                return i + 1;
            }
        }
        return RankCount + 1;
    }
}
