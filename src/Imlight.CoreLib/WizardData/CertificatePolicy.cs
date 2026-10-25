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
 * PLAYER DATABASE CERTIFICATE POLICY
 * ========================================================================
 * 
 * PURPOSE:
 * Decides whether a remote RavenDB server certificate is trusted: default validation,
 * or an exact SHA-1 thumbprint match when a pin is configured.
 * 
 * USAGE EXAMPLE:
 * CertificatePolicy.Accept(cert, sslPolicyErrors, "Database.PlayerDatabaseCertificateThumbprint" value)
 * 
 * NOTE:
 * A pin accepts the pinned certificate even when its chain does not validate (self-signed).
 * 
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

#nullable enable

using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Imlight.CoreLib.WizardData;

public static class CertificatePolicy {

    private static readonly char[] s_thumbprintSeparators = [':', '-'];

    /// <summary>
    /// Returns whether <paramref name="cert"/> is trusted: with no pin, only when
    /// <paramref name="errors"/> is <see cref="SslPolicyErrors.None"/>; with a pin, only when the
    /// certificate's thumbprint equals it, compared case-insensitively ignoring ':', '-' and whitespace.
    /// </summary>
    public static bool Accept(X509Certificate2? cert, SslPolicyErrors errors, string? pinnedThumbprint) {
        if (cert is null) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(pinnedThumbprint)) {
            return errors == SslPolicyErrors.None;
        }

        return string.Equals(cert.Thumbprint, NormalizeThumbprint(pinnedThumbprint), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeThumbprint(string thumbprint)
        => new([.. thumbprint.Where(c => !char.IsWhiteSpace(c) && !s_thumbprintSeparators.Contains(c))]);

}
