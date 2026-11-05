using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    [Collection("SignRecoverableBackendMutation")]
    public class EthECKeyRecoveryOutcomeTests
    {
        [Fact]
        public void Given_signatures_whose_x_coordinate_is_not_on_the_curve_When_recovered_natively_Then_null_is_returned_and_no_NullReferenceException_occurs()
        {
            EthECKey.SignRecoverable = true;
            var hash = new byte[32];
            hash[31] = 1;
            var results = new List<EthECKey>();
            var raised = new List<Exception>();
            var thread = Environment.CurrentManagedThreadId;
            EventHandler<FirstChanceExceptionEventArgs> observe = (_, e) =>
            {
                if (Environment.CurrentManagedThreadId == thread) raised.Add(e.Exception);
            };
            AppDomain.CurrentDomain.FirstChanceException += observe;
            try
            {
                for (byte r = 1; r <= 64; r++)
                {
                    var rBytes = new byte[32];
                    rBytes[31] = r;
                    var sBytes = new byte[32];
                    sBytes[31] = 1;
                    var signature = EthECDSASignatureFactory.FromComponents(rBytes, sBytes, new byte[] { 27 });
                    results.Add(EthECKey.RecoverFromSignature(signature, hash));
                }
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= observe;
            }

            Assert.Contains(results, k => k == null);
            Assert.DoesNotContain(raised, e => e is NullReferenceException);
        }
    }
}
