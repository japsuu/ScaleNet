﻿namespace Server.Networking.Database;

public sealed class PlayerData
{
    public readonly string Username;


    public PlayerData(string username)
    {
        Username = username;
    }
}