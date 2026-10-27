using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class UndetectableObjectTests {

    private static readonly CoreObjectInfo Undetectable = new() { m_zoneTag = "WC-ST03-AkilliesNPC", m_bUndetectable = true };
    private static readonly CoreObjectInfo Plain = new() { m_zoneTag = "WC-ST03-KID01 instance", m_bUndetectable = false };

    [Fact]
    public void AnUndetectableObject_IsHidden_WithoutAShowingMod() {
        Assert.True(RenderComponent.IsHiddenUntilShown(Undetectable, []));
        Assert.True(RenderComponent.IsHiddenUntilShown(Undetectable, [new Dynamod { ClientTag = "WC-ST03-AkilliesNPC", ModState = "" }]));
    }

    [Fact]
    public void AnUndetectableObject_IsShown_ByTheSpawnState() {
        Assert.False(RenderComponent.IsHiddenUntilShown(Undetectable, [new Dynamod { ClientTag = "WC-ST03-AkilliesNPC", ModState = "on" }]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APlainObject_IsNeverHiddenByTheFlag(bool withShowingMod) {
        var mods = withShowingMod ? new[] { new Dynamod { ClientTag = "WC-ST03-KID01 instance", ModState = "On" } } : [];

        Assert.False(RenderComponent.IsHiddenUntilShown(Plain, mods));
    }

    [Fact]
    public void ASpawnedCreature_HasNoInfo_AndIsNotHidden() {
        Assert.False(RenderComponent.IsHiddenUntilShown(null, []));
    }

}
