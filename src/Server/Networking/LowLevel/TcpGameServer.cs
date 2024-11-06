using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using Shared.Utils;

namespace Server.Networking.LowLevel;

internal class TcpGameServer(IPAddress address, int port) : TcpServer(address, port)
{
    private bool _rejectNewPackets;

    public event Action<ServerStateArgs>? ServerStateChanged;
    public event Action<ClientStateArgs>? ClientStateChanged;

    public bool RejectNewPackets
    {
        get => _rejectNewPackets;
        set
        {
            _rejectNewPackets = value;
            
            foreach (TcpSession session in Sessions.Values)
                ((ClientConnection)session).RejectNewPackets = _rejectNewPackets;
        }
    }

    public bool RejectNewConnections { get; set; }


#region Session management

    protected override TcpSession CreateSession() => new ClientConnection(this);

    protected override void OnConnecting(TcpSession session)
    {
        if (RejectNewConnections)
            session.Disconnect();
        
        ClientStateChanged?.Invoke(new ClientStateArgs((ClientConnection)session, ClientState.Connecting));
    }
    

    protected override void OnConnected(TcpSession session)
    {
        ClientStateChanged?.Invoke(new ClientStateArgs((ClientConnection)session, ClientState.Connecting));
    }


    protected override void OnDisconnecting(TcpSession session)
    {
        ClientStateChanged?.Invoke(new ClientStateArgs((ClientConnection)session, ClientState.Disconnecting));
    }


    protected override void OnDisconnected(TcpSession session)
    {
        ClientStateChanged?.Invoke(new ClientStateArgs((ClientConnection)session, ClientState.Disconnected));
    }

#endregion


#region Starting

    protected override void OnStarting()
    {
        ServerStateChanged?.Invoke(new ServerStateArgs(ServerState.Starting));
    }


    protected override void OnStarted()
    {
        ServerStateChanged?.Invoke(new ServerStateArgs(ServerState.Started));
    }

#endregion


#region Stopping


    protected override void OnStopping()
    {
        ServerStateChanged?.Invoke(new ServerStateArgs(ServerState.Stopping));
    }


    protected override void OnStopped()
    {
        ServerStateChanged?.Invoke(new ServerStateArgs(ServerState.Stopped));
    }

#endregion


#region Error handling

    protected override void OnError(SocketError error)
    {
        Logger.LogError($"Chat TCP server caught an error with code {error}");
    }

#endregion
}