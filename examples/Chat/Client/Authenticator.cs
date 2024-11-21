using System;
using Shared;
using Shared.Authentication;

namespace Client
{
    public class Authenticator
    {
        private readonly ChatClient _client;
    
        /// <summary>
        /// Called when authenticator has received an authentication result.
        /// </summary>
        public event Action<AuthenticationResult> AuthenticationResultReceived;
    
        /// <summary>
        /// Called when authenticator has received a registration result.
        /// </summary>
        public event Action<AccountCreationResult> AccountCreationResultReceived;


        public Authenticator(ChatClient client)
        {
            _client = client;
        
            client.NetManager.RegisterMessageHandler<NetMessages.AuthenticationResponseMessage>(OnReceiveAuthResponse);
            client.NetManager.RegisterMessageHandler<NetMessages.RegisterResponseMessage>(OnReceiveRegisterResponse);
        }


        public void Login(string username, string password)
        {
            _client.NetManager.SendMessageToServer(new NetMessages.AuthenticationRequestMessage(username, password));
        }


        public void Register(string username, string password)
        {
            _client.NetManager.SendMessageToServer(new NetMessages.RegisterRequestMessage(username, password));
        }


        private void OnReceiveAuthResponse(NetMessages.AuthenticationResponseMessage msg)
        {
            if (msg.Result == AuthenticationResult.Success)
                _client.SetAuthenticated(new AccountUID(msg.ClientUid));

            AuthenticationResultReceived?.Invoke(msg.Result);
        }


        private void OnReceiveRegisterResponse(NetMessages.RegisterResponseMessage msg)
        {
            AccountCreationResultReceived?.Invoke(msg.Result);
        }
    }
}