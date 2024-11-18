using System.Diagnostics.CodeAnalysis;
using ScaleNet.Networking;

namespace ScaleNet.Server.Database;

/// <summary>
/// Represents a database access object that can be used to interact with the database.
/// </summary>
public interface IDatabaseAccess
{
#region Account operations

    /// <summary>
    /// Attempts to create a new account with the given username and password.
    /// </summary>
    /// <param name="username">The username to create the account with.</param>
    /// <param name="password">The password to create the account with.</param>
    /// <returns>The result of the account creation operation.</returns>
    public AccountCreationResult CreateAccount(string username, string password);
    
    /// <summary>
    /// Tries to authenticate the given username and password.
    /// </summary>
    /// <param name="username">The username to authenticate with.</param>
    /// <param name="password">The password to authenticate with.</param>
    /// <param name="accountUid">The client UID of the authenticated account, if successful.</param>
    /// <returns>The result of the authentication operation.</returns>
    public AuthenticationResult TryAuthenticate(string username, string password, out AccountUID accountUid);
    
    /// <summary>
    /// Tries to get the player data for the given account.
    /// </summary>
    /// <param name="accountUid">The account to get the player data for.</param>
    /// <param name="playerData">The player data for the account, if found.</param>
    /// <returns>True if the player data was found, false otherwise.</returns>
    public bool TryGetPlayerData(AccountUID accountUid, [NotNullWhen(true)]out PlayerData? playerData);

#endregion
}