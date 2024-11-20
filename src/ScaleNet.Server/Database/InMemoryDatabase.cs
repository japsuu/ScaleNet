using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ScaleNet.Common;

namespace ScaleNet.Server.Database;

public class InMemoryDatabase : IDatabaseAccess
{
    private class AccountData(string username, string password)
    {
        public readonly string Username = username;
        public readonly string Password = password;
    }

    // Username -> AccountData
    // This is a separate DB to easily check if a username is taken, and possibly allow the user to change their username.
    private readonly ConcurrentDictionary<string, AccountUID> _accountUidTable = new();
    // Contains authentication/account data for each registered account.
    private readonly ConcurrentDictionary<AccountUID, AccountData> _accountsTable = new();
    // Contains player data for each client that has logged in at least once.
    private readonly ConcurrentDictionary<AccountUID, PlayerData> _playersTable = new();
    
    private uint _nextClientUid = 1;


    public AccountCreationResult CreateAccount(string username, string password)
    {
        if (username.Length < SharedConstants.MIN_USERNAME_LENGTH || username.Length > SharedConstants.MAX_USERNAME_LENGTH)
            return AccountCreationResult.InvalidUsername;
        
        if (password.Length < SharedConstants.MIN_PASSWORD_LENGTH || password.Length > SharedConstants.MAX_PASSWORD_LENGTH)
            return AccountCreationResult.InvalidPassword;
        
        if (_accountUidTable.ContainsKey(username))
            return AccountCreationResult.UsernameTaken;
        
        AccountUID accountUid = new(_nextClientUid);
        _nextClientUid++;

        // Reserve the username for the new account.
        _accountUidTable.TryAdd(username, accountUid);
        
        // Create the new account.
        AccountData accountData = new(username, password);
        _accountsTable.TryAdd(accountUid, accountData);

        return AccountCreationResult.Success;
    }


    public AuthenticationResult TryAuthenticate(string username, string password, out AccountUID accountUid)
    {
        if (_accountUidTable.TryGetValue(username, out AccountUID uid))
        {
            if (_accountsTable.TryGetValue(uid, out AccountData? accountData))
            {
                if (accountData.Password == password)
                {
                    accountUid = uid;
                    return AuthenticationResult.Success;
                }
                
                // Invalid password.
            }
            else
            {
                // This should never happen.
                Networking.Logger.LogWarning($"Account with username '{username}' has an {nameof(AccountUID)} but no {nameof(AccountData)}.");
            }
        }
        
        // Invalid username or password.

        accountUid = AccountUID.Invalid;
        return AuthenticationResult.InvalidCredentials;
    }


    public bool TryGetPlayerData(AccountUID accountUid, [NotNullWhen(true)] out PlayerData? playerData)
    {
        // If the player has logged in at least once, their data should be in the table.
        if(_playersTable.TryGetValue(accountUid, out playerData))
            return true;
        
        // This is the first time the player has logged in, create a new PlayerData object.
        if (_accountsTable.TryGetValue(accountUid, out AccountData? accountData))
        {
            playerData = new PlayerData(accountData.Username);
            _playersTable.TryAdd(accountUid, playerData);
            return true;
        }
        
        // The client ID is invalid.
        playerData = null;
        return false;
    }
}