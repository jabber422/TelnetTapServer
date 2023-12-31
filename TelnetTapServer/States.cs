using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace TelnetTapServer
{
    internal abstract class NewSessionState
    {
        internal abstract void AskClientSomething(TcpClient client);        

        internal abstract NewSessionState HandleResponse(ref byte[] buffer, ref int offset, int length);
    }

    internal class StateStart : NewSessionState
    {
        int iac_count = 0;
        bool _asked = false;

        internal override NewSessionState HandleResponse(ref byte[] buffer, ref int offset, int length)
        {
            int i = 0;
            for (i=0; i < length; )
            {
                if (buffer[i] == 13) { i++; continue; }
                if (buffer[i] == 255) {
                    this.iac_count++;
                    Console.WriteLine($"IAC count = {iac_count}");
                    i += 3; }
            }

            if(i != length)
            {

            }

            if (this.iac_count == 4)
            {
                Console.WriteLine("4 IAC's, changing State to StateScript");
                return new StateScript();
            }


            return this;
        }

        internal override void AskClientSomething(TcpClient client)
        {
            if (this._asked) {
                Console.WriteLine("Already asked the new client the initial question");
                return; }
            byte[] foo = new byte[] {255,
                251,
                3,
                255,
                251,
                1,
                255,
                251,
                0,
                255,
                253,
                0 };
            {
                //write the telnet server iac's to the client on connect
                NetworkStream ns = client.GetStream();
                lock(ns)
                {
                    ns.Write(foo, 0, foo.Length);
                    ns.Flush();
                }
                Console.WriteLine("Asked the new client the initial question");
                this._asked = true;
            }
        }
    }

    internal class StateScript : NewSessionState
    {
        internal override void AskClientSomething(TcpClient client)
        {
            NetworkStream ns = client.GetStream();
            lock(ns)
            {
                var writer = new StreamWriter(ns) { AutoFlush = true };
                writer.Write("ID?");
            }
        }

        internal override NewSessionState HandleResponse(ref byte[] buffer, ref int offset, int length)
        {
            string response = Encoding.ASCII.GetString(buffer, offset, length);
            Match match = Regex.Match(response, "ID,(\\d),(\\d+)");
            if (!match.Success) {
                Console.WriteLine($"ID Response is wrong!: - {response}");
                offset = length;
                return this;
            }

            buffer = new byte[1024];
            offset = 0;

            int type, id;
            int.TryParse(match.Groups[1].Value, out type);
            int.TryParse(match.Groups[2].Value, out id);

            if (type == 2) //listener
            {               
                return new StateEndNewTap(id);
            }
            else if (type == 1)
            {
                return new StateScriptNewSession(id);
            }
            Console.WriteLine("Invalid type?");
            return this;
        }
    }

    internal class StateEndNewTap : NewSessionState
    {
        public int id;

        public StateEndNewTap(int id)
        {
            this.id = id;
        }

        internal override void AskClientSomething(TcpClient client)
        {
        }

        internal override NewSessionState HandleResponse(ref byte[] buffer, ref int offset, int length)
        {
            return this;
        }
    }

    internal class StateScriptNewSession : NewSessionState
    {
        private int id;

        public StateScriptNewSession(int id)
        {
            this.id = id;
        }

        internal override void AskClientSomething(TcpClient client)
        {
            NetworkStream ns = client.GetStream();
            lock(ns)
            {
                var writer = new StreamWriter(ns) { AutoFlush = true };
                writer.Write("CON?");
            }
        }

        internal override NewSessionState HandleResponse(ref byte[] buffer, ref int offset, int length)
        {
            String a = Encoding.ASCII.GetString(buffer, offset, length);
            Match match = Regex.Match(a, "CON,(\\S+),(\\S+)");
            if (!match.Success) return new StateScript();

            string ip = match.Groups[1].Value;
            int port = int.Parse(match.Groups[2].Value);

            try
            {
                
    
                System.Net.IPHostEntry entry = System.Net.Dns.GetHostEntry(ip);
                if (entry.AddressList.Length == 0)
                {
                    return new StateScript();
                }
                ip = entry.AddressList[0].ToString();
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to parse ip or port from StartScript");
                return new StateScript();
            }
            return new StateEndNewSession(id, ip, port);
        }
    }
    internal class StateEndNewSession : NewSessionState
    {
        public int id;public string ip;
        public int port;

        public StateEndNewSession(int id, string ip, int port)
        {
            this.id = id;
            this.ip = ip;
            this.port = port;

        }

        internal override void AskClientSomething(TcpClient client)
        {
        }

        internal override NewSessionState HandleResponse(ref byte[] buffer, ref int offset, int length)
        {
            return this;
        }
    }
}
