namespace ScaleNet
{
    public static class SharedConstants
    {
        // Game
        public const ushort GAME_VERSION = 3;
    
        // Networking
        public const int SERVER_PORT = 11221;
        public const int MAX_PACKET_SIZE_BYTES = 2048;
    
        // Account registration
        public const int MIN_USERNAME_LENGTH = 3;
        public const int MAX_USERNAME_LENGTH = 16;
        public const int MIN_PASSWORD_LENGTH = 6;
        public const int MAX_PASSWORD_LENGTH = 32;
    }
}