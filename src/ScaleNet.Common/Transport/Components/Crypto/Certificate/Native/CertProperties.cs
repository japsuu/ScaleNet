using System;
using System.Security.Cryptography.X509Certificates;

namespace ScaleNet.Common.Transport.Components.Crypto.Certificate.Native
{
    public class SelfSignedCertProperties
    {
        public SelfSignedCertProperties()
        {
            DateTime today = DateTime.Today;
            ValidFrom = today.AddDays(-1);
            ValidTo = today.AddYears(10);
            Name = new X500DistinguishedName("cn=localhost");
            KeyBitLength = 4096;
        }


        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public X500DistinguishedName Name { get; set; }
        public int KeyBitLength { get; set; }
        public bool IsPrivateKeyExportable { get; set; }
    }
}