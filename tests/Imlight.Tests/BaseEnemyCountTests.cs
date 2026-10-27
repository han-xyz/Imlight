using Imlight.CoreLib.Game.Zone.Components;
using Xunit;

namespace Imlight.Tests;

public sealed class BaseEnemyCountTests {

    [Theory]
    [InlineData(-1, 1122u, 1)]   // Unicorn Way: the beginner marker, one enemy
    [InlineData(-1, 0u, 2)]      // Firecat Alley, Cyclops Lane: two enemies for a lone wizard
    [InlineData(0, 0u, 2)]       // Colossus Boulevard, Celestia: the default
    [InlineData(4, 0u, 4)]       // Sunken City: the sigil says four
    [InlineData(1, 0u, 1)]       // a sigil that says one
    public void ASigilsBaseEnemyCount(int sigilCount, uint beginnerMarker, int expected) {
        Assert.Equal(expected, CombatDuelComponent.BaseEnemyCount(sigilCount, beginnerMarker));
    }

}
