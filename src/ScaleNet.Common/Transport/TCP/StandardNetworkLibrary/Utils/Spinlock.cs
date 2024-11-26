using System.Runtime.CompilerServices;
using System.Threading;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Utils
{
    public class Spinlock
    {
        private int _lockValue;
        private SpinWait _spinWait;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Take()
        {
            if (Interlocked.CompareExchange(ref _lockValue, 1, 0) != 0)
            {
                int spinCount = 0;

                while (Interlocked.CompareExchange(ref _lockValue, 1, 0) != 0)
                {
                    if (spinCount < 22)
                        spinCount++;

                    else
                        _spinWait.SpinOnce();
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTaken() => Interlocked.CompareExchange(ref _lockValue, 1, 1) == 1;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            Interlocked.Exchange(ref _lockValue, 0);
        }


        internal bool TryTake() => Interlocked.CompareExchange(ref _lockValue, 1, 0) == 0;
    }
}