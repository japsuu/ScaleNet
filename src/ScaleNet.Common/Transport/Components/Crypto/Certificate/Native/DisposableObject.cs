using System;
using System.Runtime.InteropServices;

namespace ScaleNet.Common.Transport.Components.Crypto.Certificate.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public abstract class DisposeableObject : IDisposable
    {
        private bool disposed;


        ~DisposeableObject()
        {
            CleanUp(false);
        }


        public void Dispose()
        {
            // note this method does not throw ObjectDisposedException
            if (!disposed)
            {
                CleanUp(true);

                disposed = true;

                GC.SuppressFinalize(this);
            }
        }


        protected abstract void CleanUp(bool viaDispose);


        /// <summary>
        ///     Typical check for derived classes
        /// </summary>
        protected void ThrowIfDisposed()
        {
            ThrowIfDisposed(GetType().FullName);
        }


        /// <summary>
        ///     Typical check for derived classes
        /// </summary>
        protected void ThrowIfDisposed(string objectName)
        {
            if (disposed)
                throw new ObjectDisposedException(objectName);
        }
    }
}