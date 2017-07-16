using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;


using Network.TCP;
using Network.UDP;
using Network.Protocol;

namespace Network
{
    class Program
    {
        private static ProtocolManager prtMgr;
        private static TCPServer server;
        private static TCPCilent client;
        private static Timer PingTimer;

        static void PingSend(object state)
        {
            if (server != null)
            {
                PingerProtocol.PingerMsg msg = new PingerProtocol.PingerMsg();
                prtMgr.Send(msg, PingerProtocol.ProtocolNum, server);
            }else if(client != null)
            {
                PingerProtocol.PingerMsg msg = new PingerProtocol.PingerMsg();
                prtMgr.Send(msg, PingerProtocol.ProtocolNum, client);
            }

        }

        static void Main(string[] args)
        {
            prtMgr = new ProtocolManager();
            PingerProtocol pingPro = new PingerProtocol();
            ApplicaionProtocol appPro = new ApplicaionProtocol();
            appPro.ReceiveCallback += AppCallback;

            prtMgr.AddProtocol(pingPro);
            prtMgr.AddProtocol(appPro);

            string str = Console.ReadLine();
            if (str.Equals("client"))
            {
                TCPClientProc();
            }
            else if(str.Equals("server"))
            {
                TCPServerProc();
            }

            Console.WriteLine("END");
            return;
        }

        public static void AppCallback(ApplicaionProtocol.AppMsg msg)
        {
            Console.WriteLine("AppMsgを受信 : " + Encoding.UTF8.GetString(msg.data));
        }

        public static void TCPClientProc()
        {
            Console.Write("ServerIP : ");
            string ip = Console.ReadLine();
            Console.Write("ServerPort : ");
            string port = Console.ReadLine();

            _ReceiveData callback = TCPClientCallback;

            client = new TCPCilent(ip, int.Parse(port),1024,callback);
            client.OnConnectSuccess += remote =>
            {
                Console.WriteLine("接続しました。");
            };
            client.OnConnectFailed += (_ip, _port, _error) =>
            {
                Console.WriteLine(String.Format("接続が失敗しました。 IP:{0} PORT:{1} ERROR:{2}", _ip, _port, _error));
            };
            client.OnDisconnect += remote =>
            {
                Console.WriteLine("切断されました。");
                PingTimer.Dispose();
            };

            client.Connect();

            client.ReceiveStart();

            PingTimer = new Timer(PingSend,null,0,1000);

            while (true)
            {
                string str = Console.ReadLine();
                if (str.Equals("exit"))
                {
                    break;
                }
                ApplicaionProtocol.AppMsg msg = new ApplicaionProtocol.AppMsg();
                msg.data = Encoding.UTF8.GetBytes(str);
                prtMgr.Send(msg, ApplicaionProtocol.ProtocolNum, client);
            }

            Console.WriteLine("END...");
            PingTimer.Dispose();
            client.Close();
            Console.WriteLine("END");
            Console.ReadLine();

        }

        public static void TCPServerProc()
        {
            Console.Write("ServerIP : ");
            string ip = Console.ReadLine();
            Console.Write("Port : ");
            string port = Console.ReadLine();

            _ReceiveData callback = TCPClientCallback;

            server = new TCPServer(ip, int.Parse(port));
            server.OnConnectSuccess += remote =>
            {
                IPEndPoint endPint = remote;
                Console.WriteLine(String.Format("接続を確認 {0} : {1}", endPint.Address, endPint.Port));
            };
            server.OnDisconnect += remote =>
            {
                IPEndPoint endPint = remote;
                Console.WriteLine(String.Format("切断を確認 {0} : {1}", endPint.Address, endPint.Port));
            };
            server.DataReceiveCallback += TCPClientCallback;
            server.StartListener();

            PingTimer = new Timer(PingSend, null, 0, 1000);

            Console.WriteLine("鯖開始");

            Console.ReadLine();
            PingTimer.Dispose();
            server.StopServer();
            Console.WriteLine("鯖終了");
            Console.ReadLine();
        }

        public static void UDPProc()
        {
            Console.WriteLine("UDP NetworkTest");

            Console.Write("IpAddress : ");
            string ipAdd = Console.ReadLine();

            Console.Write("LocalPort : ");
            int localPort = int.Parse(Console.ReadLine());
            Console.Write("RemotePort : ");
            int remotePort = int.Parse(Console.ReadLine());

            Console.WriteLine("IP:" + ipAdd);
            Console.WriteLine("LocalPort : " + localPort);
            Console.WriteLine("RemotePort : " + remotePort);


            //StringConverter converter = new StringConverter(Encoding.UTF8);
            SoapConverter converter = new SoapConverter();

            UDPClient udp = new UDPClient(ipAdd, localPort, remotePort);
            udp.DataReceived += (data, ep) =>
            {
                byte[] _data = data;
                NetMsg msg = converter.convert(_data);
                Console.WriteLine(msg.ToString());
            };

            Console.WriteLine("Start Receive");
            udp.StartReceive();

            int sequence = 0;
            do
            {
                string str = Console.ReadLine();
                if (str.Equals("exit"))
                {
                    Console.WriteLine("End...");
                    break;
                }

                Console.WriteLine("Send Msg : " + str);
                //NetMsg msg = new NetMsg(sequence, str);
                //udp.Send(converter.convert(msg));

                sequence++;
            } while (true);

            udp.Close();
        }

        public static void TCPClientCallback(byte[] data, TcpClient client)
        {
            prtMgr.divide(data, client);
        }
    }



}
