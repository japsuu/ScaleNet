using System;

namespace ScaleNet.Common.Transport.Components.Crypto.Certificate.Native
{
    public abstract class CryptKey : DisposeableObject
    {
        private readonly CryptContext ctx;


        internal CryptKey(CryptContext ctx, IntPtr handle)
        {
            this.ctx = ctx;
            Handle = handle;
        }


        internal IntPtr Handle { get; }

        public abstract KeyType Type { get; }


        protected override void CleanUp(bool viaDispose)
        {
            if (viaDispose)
                ctx.DestroyKey(this);
        }
    }
}