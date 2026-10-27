using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class GoalRevealTests {

    private const string Zone = "WizardCity/WC_Streets/WC_Triton";
    private const string Tag = "WC-TRITON-NPC02 instance";

    private static PersonaGoalTemplate GoalRevealing(string tag, bool shows = true, bool remove = false)
        => new() {
            m_goalName = "Goal 1",
            m_activateResults = new ResultList {
                m_results = [new ResAddDynaMod { m_dynaModClientTag = tag, m_zoneName = Zone, m_bShowsObject = shows, m_dynaModRemove = remove }]
            }
        };

    [Fact]
    public void AGoalsReveal_IsMissing_WhenTheWizardHoldsNoMod() {
        var missing = QuestService.MissingReveals([GoalRevealing(Tag)], []);

        var reveal = Assert.Single(missing);
        Assert.Equal(Tag, reveal.m_dynaModClientTag.ToString());
    }

    [Fact]
    public void AGoalsReveal_IsHeld_WhenTheWizardHasTheSpawnMod() {
        var mods = new[] { new Dynamod { ClientTag = Tag, ZoneName = Zone, ModState = "On" } };

        Assert.Empty(QuestService.MissingReveals([GoalRevealing(Tag)], mods));
    }

    [Fact]
    public void AHidingMod_DoesNotCountAsHeld() {
        var mods = new[] { new Dynamod { ClientTag = Tag, ZoneName = Zone, ModState = "" } };

        Assert.Single(QuestService.MissingReveals([GoalRevealing(Tag)], mods));
    }

    [Fact]
    public void OnlyShowingResults_Count_OnceEach() {
        var goals = new[] { GoalRevealing(Tag), GoalRevealing(Tag), GoalRevealing("Other", shows: false), GoalRevealing("Gone", remove: true) };

        Assert.Single(QuestService.MissingReveals(goals, []));
    }

}
