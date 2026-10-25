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
 * CERTIFICATE POLICY TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the player database certificate validation and the certificate file check.
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
 * Last Updated: 10/25/2026
 */

using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Imlight.Common;
using Imlight.CoreLib.WizardData;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class CertificatePolicyTests {

    [Fact]
    public void NoPin_DefaultErrorsRejected() {
        using var cert = CreateSelfSigned();

        Assert.False(CertificatePolicy.Accept(cert, SslPolicyErrors.RemoteCertificateChainErrors, null));
    }

    [Fact]
    public void NoPin_NoErrorsAccepted() {
        using var cert = CreateSelfSigned();

        Assert.True(CertificatePolicy.Accept(cert, SslPolicyErrors.None, ""));
    }

    [Fact]
    public void Pin_MatchAccepted() {
        using var cert = CreateSelfSigned();

        Assert.True(CertificatePolicy.Accept(cert, SslPolicyErrors.RemoteCertificateChainErrors, cert.Thumbprint));
    }

    [Fact]
    public void Pin_MismatchRejected() {
        using var cert = CreateSelfSigned();
        using var other = CreateSelfSigned();

        Assert.False(CertificatePolicy.Accept(cert, SslPolicyErrors.None, other.Thumbprint));
    }

    [Fact]
    public void Pin_FormatInsensitive() {
        using var cert = CreateSelfSigned();
        var pin = string.Join(':', cert.Thumbprint.ToLowerInvariant().Chunk(2).Select(pair => new string(pair)));

        Assert.True(CertificatePolicy.Accept(cert, SslPolicyErrors.RemoteCertificateChainErrors, pin));
    }

    [Fact]
    public void MissingCertFile_Throws() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Database]\n");
            ConfigurationManager.Initialize(iniFilePath);
            var database = new MissingCertificateDatabase();

            var error = Assert.Throws<FileNotFoundException>(database.LoadCertificate);

            Assert.Contains(database.MissingPath, error.Message);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    private static X509Certificate2 CreateSelfSigned() {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=imlight-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class MissingCertificateDatabase : RavenDatabaseSingleton<MissingCertificateDatabase> {

        public string MissingPath { get; } = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");

        protected override string DatabaseName => "Test";
        protected override string Url => "https://127.0.0.1:1";
        protected override string CertificatePath => MissingPath;
        protected override string CertificateThumbprint => "";

        public X509Certificate2 LoadCertificate() => GetCertificate();

    }

}
