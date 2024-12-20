using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ScaleNet.Client;
using ScaleNet.Client.LowLevel;
using ScaleNet.Client.LowLevel.Transport.WebSocket;
using ScaleNet.Common;
using Shared.Authentication;
using NetMessages = Shared.NetMessages;
using SharedConstants = Shared.SharedConstants;

namespace Client;

public sealed class ChatClient : IDisposable
{
    private readonly Authenticator _authenticator;
    private readonly ConcurrentQueue<string> _chatMsgQueue = new();
    private readonly CancellationTokenSource _ctSource = new();

    private bool _isAuthenticated;
    private bool _serverAllowsRegistration;
    
    public readonly ClientNetworkManager NetManager;


    public ChatClient(ClientSslContext context, string address, ushort port)
    {
        // IMPORTANT: Initialize the ScaleNetManager before creating any network-related objects.
        ScaleNetManager.Initialize();

        WebSocketClientTransport transport = new(address, port, context);
        NetManager = new ClientNetworkManager(transport);
        _authenticator = new Authenticator(this);
        
        // Called when authentication information is received from the server
        NetManager.RegisterMessageHandler<NetMessages.AuthenticationInfoMessage>(msg =>
        {
            _serverAllowsRegistration = msg.RegistrationAllowed;
            Task.Run(RegisterOrLogin, _ctSource.Token);
        });
        
        // Called when a chat message is received from the server
        NetManager.RegisterMessageHandler<NetMessages.ChatMessageNotification>(msg =>
        {
            ScaleNetManager.Logger.LogInfo($"[Chat] {msg.User}: {msg.Message}");
        });
        
        // Called when the server has determined the authentication (login) result
        _authenticator.AuthenticationResultReceived += result =>
        {
            if (result == AuthenticationResult.Success)
            {
                ScaleNetManager.Logger.LogInfo("Authenticated successfully");
                Task.Run(ChatLoop, _ctSource.Token);
            }
            else
            {
                ScaleNetManager.Logger.LogError("Failed to authenticate");
                Task.Run(RegisterOrLogin, _ctSource.Token);
            }
        };
        
        // Called when the server has determined the account creation (register) result
        _authenticator.AccountCreationResultReceived += result =>
        {
            if (result == AccountCreationResult.Success)
                ScaleNetManager.Logger.LogInfo("Account created successfully. You can now login.");
            else
                ScaleNetManager.Logger.LogError("Failed to create account");

            Task.Run(RegisterOrLogin, _ctSource.Token);
        };
    }


    public void Dispose()
    {
        NetManager.Dispose();
    }


    public void Run()
    {
        NetManager.Connect();
            
        ClientLoop();

        NetManager.Disconnect();
    }


    private void ClientLoop()
    {
        while (!_ctSource.Token.IsCancellationRequested)
        {
            NetManager.Update();
            
            while (_chatMsgQueue.TryDequeue(out string? line))
            {
                if (line == "!")
                {
                    _ctSource.Cancel();
                    break;
                }

                if (!_isAuthenticated)
                    continue;
                
                ClearPreviousConsoleLine();
                NetManager.SendMessageToServer(new NetMessages.ChatMessage(line));
            }
        }
        
        NetManager.Disconnect();
        _ctSource.Cancel();
    }


    public void SetAuthenticated(AccountUID accountUid)
    {
        ScaleNetManager.Logger.LogDebug($"Local client is now authenticated with account UID: {accountUid}");

        _isAuthenticated = true;
    }


#region Threaded input loops

    // Input loops are threaded to not block the main loop,
    // since the NetManager.Update() method must be called frequently.
    
    private void ChatLoop()
    {
        ScaleNetManager.Logger.LogInfo("'!' to exit");

        while (NetManager.IsConnected && !_ctSource.Token.IsCancellationRequested)
        {
            string? line = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            _chatMsgQueue.Enqueue(line);
        }
    }

#endregion


#region Prompting stuff

    private void RegisterOrLogin()
    {
        bool register = false;
        if (_serverAllowsRegistration)
        {
            Console.WriteLine("Do you want to register a new account? (y/n)");
            string? input = Console.ReadLine();
            register = input?.ToLower() == "y";
        }
        
        if (_serverAllowsRegistration && register)
        {
            Register();
        }
        else
        {
            Login();
        }
    }


    private void Login()
    {
        (string username, string password) = GetCredentials();
        RequestLogin(username, password);
    }


    private void Register()
    {
        (string username, string password) = GetCredentials();
        RequestRegister(username, password);
    }


    private void RequestLogin(string username, string password)
    {
        if (!NetManager.IsConnected)
        {
            ScaleNetManager.Logger.LogError("Local connection is not started, cannot request login.");
            return;
        }

        if (_isAuthenticated)
        {
            ScaleNetManager.Logger.LogError("Local client is already authenticated.");
            return;
        }
            
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ScaleNetManager.Logger.LogError("Username and password cannot be empty.");
            return;
        }
            
        if (username.Length < SharedConstants.MIN_USERNAME_LENGTH || username.Length > SharedConstants.MAX_USERNAME_LENGTH)
        {
            ScaleNetManager.Logger.LogError($"Username must be between {SharedConstants.MIN_USERNAME_LENGTH} and {SharedConstants.MAX_USERNAME_LENGTH} characters.");
            return;
        }
        
        if (password.Length < SharedConstants.MIN_PASSWORD_LENGTH || password.Length > SharedConstants.MAX_PASSWORD_LENGTH)
        {
            ScaleNetManager.Logger.LogError($"Password must be between {SharedConstants.MIN_PASSWORD_LENGTH} and {SharedConstants.MAX_PASSWORD_LENGTH} characters.");
            return;
        }
        
        _authenticator.Login(username, password);
    }


    private void RequestRegister(string username, string password)
    {
        if (!NetManager.IsConnected)
        {
            ScaleNetManager.Logger.LogError("Local connection is not started, cannot request registration.");
            return;
        }

        if (_isAuthenticated)
        {
            ScaleNetManager.Logger.LogError("Local client is already authenticated.");
            return;
        }

        if (!_serverAllowsRegistration)
        {
            ScaleNetManager.Logger.LogInfo("Registration is disabled by server. You can currently only login.");
            return;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ScaleNetManager.Logger.LogError("Username and password cannot be empty.");
            return;
        }

        if (username.Length < SharedConstants.MIN_USERNAME_LENGTH || username.Length > SharedConstants.MAX_USERNAME_LENGTH)
        {
            ScaleNetManager.Logger.LogError(
                $"Username must be between {SharedConstants.MIN_USERNAME_LENGTH} and {SharedConstants.MAX_USERNAME_LENGTH} characters.");
            return;
        }

        if (password.Length < SharedConstants.MIN_PASSWORD_LENGTH || password.Length > SharedConstants.MAX_PASSWORD_LENGTH)
        {
            ScaleNetManager.Logger.LogError(
                $"Password must be between {SharedConstants.MIN_PASSWORD_LENGTH} and {SharedConstants.MAX_PASSWORD_LENGTH} characters.");
            return;
        }

        _authenticator.Register(username, password);
    }


    private static (string, string) GetCredentials()
    {
        Console.WriteLine("Enter your username:");
        string username = Console.ReadLine() ?? "user";

        Console.WriteLine("Enter your password:");
        string password = Console.ReadLine() ?? "password";

        return (username, password);
    }


    private static void ClearPreviousConsoleLine()
    {
        int currentLineCursor = Console.CursorTop - 1;
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        for (int i = 0; i < Console.WindowWidth; i++)
            Console.Write(" ");
        Console.SetCursorPosition(0, currentLineCursor);
    }

#endregion
}