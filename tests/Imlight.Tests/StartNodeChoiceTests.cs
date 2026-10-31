using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.Zone.Core;
using Xunit;

namespace Imlight.Tests;

public sealed class StartNodeChoiceTests {

    private static NodeObject Node(uint id) => new() { m_id = new GID(id) };

    private static readonly List<NodeObject> Nodes = [Node(1), Node(2), Node(3)];

    [Fact]
    public void RandomUnique_NeverTakesAnOccupiedNode() {
        var rng = new Random(7);
        var occupied = new List<NodeObject> { Nodes[0], Nodes[2] };

        for (var i = 0; i < 20; i++) {
            Assert.Same(Nodes[1], StartNodeChoice.Choose(StartNodeType.SNT_RANDOM_UNIQUE, 0, Nodes, occupied, rng));
        }
    }

    [Fact]
    public void RandomUnique_GivesNothing_WhenEveryNodeIsTaken() {
        Assert.Null(StartNodeChoice.Choose(StartNodeType.SNT_RANDOM_UNIQUE, 0, Nodes, Nodes, new Random(1)));
    }

    [Fact]
    public void Random_MayRepeatANode() {
        var picks = new HashSet<NodeObject>();
        var rng = new Random(3);
        for (var i = 0; i < 30; i++) {
            picks.Add(StartNodeChoice.Choose(StartNodeType.SNT_RANDOM, 0, Nodes, Nodes, rng));
        }

        Assert.True(picks.Count > 1);
    }

    [Fact]
    public void Specific_NamesItsNode_AndFallsBackToTheFirst() {
        Assert.Same(Nodes[2], StartNodeChoice.Choose(StartNodeType.SNT_SPECIFIC, 3, Nodes, [], new Random(1)));
        Assert.Same(Nodes[0], StartNodeChoice.Choose(StartNodeType.SNT_SPECIFIC, 9, Nodes, [], new Random(1)));
    }

    [Fact]
    public void FirstAndLast() {
        Assert.Same(Nodes[0], StartNodeChoice.Choose(StartNodeType.SNT_FIRST, 0, Nodes, [], new Random(1)));
        Assert.Same(Nodes[2], StartNodeChoice.Choose(StartNodeType.SNT_LAST, 0, Nodes, [], new Random(1)));
    }

}
