using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace TelnetTapServer
{
    internal class TelnetServer
    {
        TcpListener _listener = null;
        TapSessions _sessions = null;

        internal TelnetServer(IPAddress hostIp, int hostPort)
        {
            _listener = new TcpListener(hostIp, hostPort);
            _sessions = new TapSessions();
        }

        internal async Task StartAsync()
        {
            try
            {
                this._listener.Start();
                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");

                    // Perform a blocking call to accept requests
                    TcpClient client = await this._listener.AcceptTcpClientAsync();
                    //fire and forget new client connections and the connection script
                    Task.Run(() => HandleConnection(client));

                    // Hand off the connection to a separate method/script
                    //HandleConnection(client);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketException: {e}");
            }
            finally
            {
                // Stop listening for new clients
                this._listener.Stop();
            }
        }

        private async Task HandleConnection(TcpClient client)
        {
            bool success = false;
            NewSessionState _state = new StateStart();
            try
            {
                //When someone connects we send 4 IAC's and wait for them to respond
                _state.AskClientSomething(client);

                StringBuilder receivedData = new StringBuilder();
                byte[] buffer = new byte[1024];
                int byteCount;
                int offset = 0;
                NetworkStream networkStream = client.GetStream();
                while ((byteCount = await networkStream.ReadAsync(buffer, offset, buffer.Length-offset)) != 0)
                {
                    _state = _state.HandleResponse(ref buffer, ref offset, byteCount);
                    if (offset == 0)
                    {
                        _state.AskClientSomething(client);
                    }

                    if(_state is StateEndNewSession)
                    {
                        StateEndNewSession state = (StateEndNewSession)_state;
                        this._sessions.AddNewSession(state.id, state.ip, state.port, client);
                        success = true;
                        return;

                    }else if(_state is StateEndNewTap)
                    {
                        StateEndNewTap state = (StateEndNewTap)_state;
                        this._sessions.AddNewTap(state.id, client);
                        success = true;
                        return;
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error handling connection: {ex.Message}");
            }
            finally
            {
                string lep = client.Client.LocalEndPoint.ToString();
                string rep = client.Client.RemoteEndPoint.ToString();
                string msg = $"Connection Result: {success.ToString()}\r\n\tLocal: {lep}\r\n\tRemote: {rep}";

                StateEndNewTap state = (StateEndNewTap)_state;
                Console.WriteLine(msg);
            }
        }
    }

    //represents all current sessions
    internal class TapSessions
    {
        Dictionary<int, TapSession> _sessions = new Dictionary<int, TapSession> ();

        internal void AddNewSession(int id, string ip, int port, TcpClient client)
        {
            if(_sessions.ContainsKey(id))
            {
                throw new Exception("Can't do this!");
            }

            TapSession new_session = new TapSession();
            
            new_session._client = client;
            new_session._remote = new TcpClient();

            new_session._remote.Connect(ip, port);

            // Forward data from client to server
            Task clientToServerTask = ForwardDataAsync(id, "clientToServer", client.GetStream(), new_session._remote.GetStream());

            // Forward data from server to client
            Task serverToClientTask = ForwardDataAsync(id, "serverToClient", new_session._remote.GetStream(), client.GetStream());

            this._sessions.Add(id, new_session);

        }

        internal void AddNewTap(int id, TcpClient client)
        {
            if(!this._sessions.ContainsKey(id)){
                throw new Exception("Sessions doesn't have an id for that");
            }

            TapSession ongoing_session = this._sessions[id];
            ongoing_session._tap = client;

            //// Forward data from client to server
            Task clientToServerTask = ForwardDataAsync(id, "tapToServer", client.GetStream(), ongoing_session._remote.GetStream());

            //// Forward data from server to client
            //Task serverToClientTask = ForwardDataAsync(id, "serverToTap", ongoing_session._remote.GetStream(), client.GetStream());

        }

        async Task ForwardDataAsync(int id, string token, NetworkStream sourceStream, NetworkStream destinationStream)
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await destinationStream.WriteAsync(buffer, 0, bytesRead);
                    if (token == "serverToClient" && this._sessions[id]._tap != null)
                    {
                        NetworkStream ns = this._sessions[id]._tap.GetStream();
                        await ns.WriteAsync(buffer, 0, bytesRead);
                    }
                    buffer = new byte[1024];
                }

                Console.WriteLine(token + " Disconnect, 0 bytes read!");

                if (token == "clientToServer")
                {
                    //mega just disconnected from the tap server
                    //disconnect the tap server from the mud server
                    this._sessions[id]._remote.Close();
                    this._sessions.Remove(id);
                }
                else if (token == "serverToClient")
                {
                    //mud server just disconnected from the tap server
                    //disconnect mega
                    this._sessions[id]._client.Close();
                    this._sessions.Remove(id);
                }
                else if (token == "tapToServer")
                {
                    //tap client disconnected, we don't care
                    if (this._sessions[id]._tap != null)
                    {
                        this._sessions[id]._tap.Close();
                        this._sessions[id]._tap = null;
                    }
                }
                else if (token == "serverToTap")
                {
                    //ignore this, 99% sure we can
                }
            }catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    //represents a single sessions
    internal class TapSession
    {
        public TcpClient _remote = null;
        public TcpClient _client = null;
        public TcpClient _tap = null;
    }
}


