﻿using Egelke.EHealth.Client.Pki;
using Egelke.EHealth.Client.Pki.DSS;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Tsp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Egelke.EHealth.Client.Pki.Test
{
    [TestClass]
    public class TimestampProviderTests
    {
        public static byte[] msg;
        public static byte[] hash;

        [ClassInitialize]
        public static void Setup(TestContext ctx)
        {
            msg = new byte[2048];

            var rand = new Random();
            rand.NextBytes(msg);

            SHA256 sha = SHA256.Create();
            hash = sha.ComputeHash(msg);
        }

        [TestMethod]
        public void NewTsViaFedict()
        {
            var provider = new Rfc3161TimestampProvider();

            byte[] tsBytes = provider.GetTimestampFromDocumentHash(hash, "http://www.w3.org/2001/04/xmlenc#sha256");

            TimeStampToken tst = tsBytes.ToTimeStampToken();

            Assert.IsTrue(tst.IsMatch(new MemoryStream(msg)));

            //Validate
            Timestamp ts;
            IList<CertificateList> crls = new List<CertificateList>();
            IList<BasicOcspResponse> ocps = new List<BasicOcspResponse>();
            ts = tst.Validate(crls, ocps);
            Assert.IsTrue(Math.Abs((DateTime.UtcNow - ts.Time).TotalSeconds) < 60);
            if (ts.CertificateChain.ChainElements.Count == 2)
            {
                Assert.AreEqual(new DateTime(2022, 2, 28, 10, 0, 0), ts.RenewalTime);
                Assert.AreEqual(0, ocps.Count);
                Assert.AreEqual(1, crls.Count);
            }
            else if (ts.CertificateChain.ChainElements.Count == 3)
            {
                Assert.AreEqual(new DateTime(2021, 12, 15, 8, 0, 0), ts.RenewalTime);
                Assert.AreEqual(0, ocps.Count);
                Assert.AreEqual(2, crls.Count);
            }
            else
            {
                Assert.Fail("The chain should be 3 (win) or 2 (linux) long");
            }
            Assert.AreEqual(0, ts.TimestampStatus.Count(x => x.Status != X509ChainStatusFlags.NoError));
            Assert.AreEqual(0, ts.CertificateChain.ChainStatus.Count(x => x.Status != X509ChainStatusFlags.NoError));

            ts = tst.Validate(crls, ocps, DateTime.UtcNow); //check clock skewness
            Assert.IsTrue(Math.Abs((DateTime.UtcNow - ts.Time).TotalSeconds) < 60);
            //Assert.AreEqual(new DateTime(2022, 2, 28, 10, 0, 0), ts.RenewalTime);
            Assert.AreEqual(0, ts.TimestampStatus.Count(x => x.Status != X509ChainStatusFlags.NoError));
            Assert.AreEqual(0, ts.CertificateChain.ChainStatus.Count(x => x.Status != X509ChainStatusFlags.NoError));
        }

        
    }
}
