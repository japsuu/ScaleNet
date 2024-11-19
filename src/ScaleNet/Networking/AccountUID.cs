using System;

namespace ScaleNet.Networking
{
    /// <summary>
    /// A unique identifier for a specific account, assigned on account creation.
    /// </summary>
    public readonly struct AccountUID : IEquatable<AccountUID>
    {
        public AccountUID(uint value)
        {
            Value = value;
        }


        public static readonly AccountUID Invalid = new AccountUID(0);

        public uint Value { get; }


        public override string ToString()
        {
            return Value.ToString();
        }

        public override bool Equals(object? obj)
        {
            return obj is AccountUID sessionId && Value == sessionId.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value);
        }

        public static bool operator ==(AccountUID left, AccountUID right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AccountUID left, AccountUID right)
        {
            return !(left == right);
        }


        public bool Equals(AccountUID other) => Value == other.Value;
    }
}