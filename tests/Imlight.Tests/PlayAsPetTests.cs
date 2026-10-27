/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * PLAY AS PET TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for switching to and from the equipped pet (MSG_SWITCHTOPET),
 * the pet-only quest marker and the pet-only goal gate.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Behaviors;
using Xunit;

namespace Imlight.Tests;

public sealed class PlayAsPetTests {

    private const uint EquippedPetTemplateId = 251478;
    private const byte Entering = 1;
    private const byte Leaving = 0;

    [Fact]
    public void SwitchToPet_EquippedPet_PlaysAsPet() {
        var owner = new ServerPetOwnerBehavior { EquippedPetTemplateId = EquippedPetTemplateId };

        Assert.True(owner.SwitchToPet(Entering, EquippedPetTemplateId));
        Assert.True(owner.PlayingAsPet);
        Assert.True(owner.GetClientBehaviorInstance().m_playingAsPet);
    }

    [Theory]
    [InlineData(0u, EquippedPetTemplateId)]
    [InlineData(EquippedPetTemplateId, 77u)]
    public void SwitchToPet_PetNotEquipped_Refused(uint equippedTemplateId, uint requestedTemplateId) {
        var owner = new ServerPetOwnerBehavior { EquippedPetTemplateId = equippedTemplateId };

        Assert.False(owner.SwitchToPet(Entering, requestedTemplateId));
        Assert.False(owner.PlayingAsPet);
    }

    [Fact]
    public void SwitchToPet_Leaving_StopsPlayingAsPet() {
        var owner = new ServerPetOwnerBehavior { EquippedPetTemplateId = EquippedPetTemplateId };
        owner.SwitchToPet(Entering, EquippedPetTemplateId);

        Assert.True(owner.SwitchToPet(Leaving, 0));
        Assert.False(owner.PlayingAsPet);
    }

    [Fact]
    public void UnequipPet_StopsPlayingAsPet() {
        var owner = new ServerPetOwnerBehavior { EquippedPetTemplateId = EquippedPetTemplateId };
        owner.SwitchToPet(Entering, EquippedPetTemplateId);

        owner.UnequipPet();

        Assert.False(owner.PlayingAsPet);
    }

    [Fact]
    public void IsPetOnlyQuest_PlayAsYourPetQuest_True() {
        var quest = new QuestTemplate { m_playAsYourPetNPC = true, m_goals = [new WaypointGoalTemplate()] };

        Assert.True(PetOnlyGoals.IsPetOnlyQuest(quest));
    }

    [Fact]
    public void IsPetOnlyQuest_OnePetOnlyGoal_True() {
        var quest = new QuestTemplate {
            m_goals = [new WaypointGoalTemplate(), new ScavengeGoalTemplate { m_petOnlyQuest = true }],
        };

        Assert.True(PetOnlyGoals.IsPetOnlyQuest(quest));
    }

    [Fact]
    public void IsPetOnlyQuest_NoPetOnlyGoal_False() {
        var quest = new QuestTemplate { m_goals = [new WaypointGoalTemplate(), null] };

        Assert.False(PetOnlyGoals.IsPetOnlyQuest(quest));
        Assert.False(PetOnlyGoals.IsPetOnlyQuest(new QuestTemplate()));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void IsPetOnlyGoal_FollowsTheGoalFlag(bool petOnlyGoal, bool expected) {
        Assert.Equal(expected, PetOnlyGoals.IsPetOnlyGoal(new WaypointGoalTemplate { m_petOnlyQuest = petOnlyGoal }));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void NeedsPet_PetOnlyGoal_OnlyWhilePlayingAsWizard(bool petOnlyGoal, bool playingAsPet, bool expected) {
        var goal = new ScavengeGoalTemplate { m_petOnlyQuest = petOnlyGoal };

        Assert.Equal(expected, PetOnlyGoals.NeedsPet(goal, new ServerPetOwnerBehavior { PlayingAsPet = playingAsPet }));
    }

}
