// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Security;
using System.Net.Test.Common;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;

namespace System.Net.Http.Functional.Tests
{
    using Configuration = System.Net.Test.Common.Configuration;

    public class HttpClientHandler_DangerousAcceptAllCertificatesValidator_Test
    {
        // TODO: https://github.com/dotnet/corefx/issues/7812
        private static bool ClientSupportsDHECipherSuites => (!PlatformDetection.IsWindows || PlatformDetection.IsWindows10Version1607OrGreater);

        [Fact]
        public void SingletonReturnsTrue()
        {
            Assert.NotNull(HttpClientHandler.DangerousAcceptAnyServerCertificateValidator);
            Assert.Same(HttpClientHandler.DangerousAcceptAnyServerCertificateValidator, HttpClientHandler.DangerousAcceptAnyServerCertificateValidator);
            Assert.True(HttpClientHandler.DangerousAcceptAnyServerCertificateValidator(null, null, null, SslPolicyErrors.None));
        }

        [Theory]
        [InlineData(SslProtocols.Tls, false)] // try various protocols to ensure we correctly set versions even when accepting all certs
        [InlineData(SslProtocols.Tls, true)]
        [InlineData(SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false)]
        [InlineData(SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, true)]
        [InlineData(SslProtocols.None, false)]
        [InlineData(SslProtocols.None, true)]
        public async Task SetDelegate_ConnectionSucceeds(SslProtocols acceptedProtocol, bool requestOnlyThisProtocol)
        {
            using (var handler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator })
            using (var client = new HttpClient(handler))
            {
                if (requestOnlyThisProtocol)
                {
                    handler.SslProtocols = acceptedProtocol;
                }

                var options = new LoopbackServer.Options { UseSsl = true, SslProtocols = acceptedProtocol };
                await LoopbackServer.CreateServerAsync(async (server, url) =>
                {
                    await TestHelper.WhenAllCompletedOrAnyFailed(
                        LoopbackServer.ReadRequestAndSendResponseAsync(server, options: options),
                        client.GetAsync(url));
                }, options);
            }
        }

        public static readonly object[][] InvalidCertificateServers =
        {
            new object[] { Configuration.Http.ExpiredCertRemoteServer },
            new object[] { Configuration.Http.SelfSignedCertRemoteServer },
            new object[] { Configuration.Http.WrongHostNameCertRemoteServer },
        };

        [OuterLoop] // TODO: Issue #11345
        [ConditionalTheory(nameof(ClientSupportsDHECipherSuites))]
        [MemberData(nameof(InvalidCertificateServers))]
        public async Task InvalidCertificateServers_CertificateValidationDisabled_Succeeds(string url)
        {
            using (var client = new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }))
            {
                (await client.GetAsync(url)).Dispose();
            }
        }
    }
}
