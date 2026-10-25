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
 * REQUIREMENT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for requirements evaluated against a wizard through the
 * requirement dispatcher.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * The dispatcher logs, and the logger reads its settings from ConfigurationManager.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class RequirementTests {

    public RequirementTests() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Logging]\nLogPath = ./logs/log.txt\nLogLevel = ERROR\nLogFormat = {Message:lj}{NewLine}\nSeqSinkUrl = http://localhost:5341\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void ReqMagicLevel_HandlerRegistered_EvaluatesLevel(int wizardLevel, bool expected) {
        var wizard = new Wizard { MagicSchoolBehavior = new ServerMagicSchoolBehavior { Level = wizardLevel } };
        var requirements = new RequirementList {
            m_requirements = [
                new ReqMagicLevel { m_numericValue = 10, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ },
            ],
        };

        var met = RequirementDispatcher.EvaluateRequirements(requirements, new GenericRequirementContext(requirements, null, null, wizard));

        Assert.Equal(expected, met);
    }

}
