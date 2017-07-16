using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization.Formatters.Soap;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.Concurrent;

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

        static void ServerPingFiled(TCPCilent client)
        {
            Console.WriteLine(String.Format("Pingの送信に失敗 切断を確認  {0} : {1}", client.Ip, client.Port));
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
            if (str.Equals("Client"))
            {
                TCPClientProc();
            }
            else if(str.Equals("Server"))
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

    interface IDataConverter<T>
    {
        T convert(byte[] data);
        byte[] convert(T data);
    }

    class StringConverter : IDataConverter<string>
    {
        private Encoding encode;
        public StringConverter(Encoding enc)
        {
            encode = enc;
        }
        public string convert(byte[] data)
        {
            return encode.GetString(data);
        }
        public byte[] convert(string data)
        {
            return encode.GetBytes(data);
        }
    }

    class SoapConverter : IDataConverter<NetMsg>
    {
        public byte[] convert(NetMsg data)
        {
            MemoryStream ms = new MemoryStream();
            SoapFormatter formatter = new SoapFormatter();
            formatter.Serialize(ms, data);
            return ms.ToArray();
        }

        public NetMsg convert(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            //Console.WriteLine(Encoding.UTF8.GetString(ms.ToArray()));
            SoapFormatter formatter = new SoapFormatter();
            return (NetMsg)formatter.Deserialize(ms);
        }
    }

    namespace UDP
    {
        class UDPClient
        {
            //デリゲート定義
            public delegate void _DataReceived(byte[] data, IPEndPoint ep);

            ManualResetEvent StopEvent;

            //パラメータ
            private UdpClient Udp;
            private IPEndPoint Remote;
            private IPEndPoint Local;

            //非同期ロック
            private object locker;

            //非同期フラグ
            private bool EndReceiveFlag;

            private IAsyncResult AsyncResult;

            //受信デリゲート華憐
            public _DataReceived DataReceived;

            public UDPClient(string ipAdd, int port)
            {
                locker = new object();

                IPAddress ip = System.Net.IPAddress.Parse(ipAdd);
                Remote = new IPEndPoint(ip, port);
                Local = new IPEndPoint(IPAddress.Any, port);
                Udp = new UdpClient(Local);

                Udp.Connect(Remote);
            }

            public UDPClient(string ipAdd, int localPort, int remotePort)
            {
                locker = new object();

                IPAddress ip = System.Net.IPAddress.Parse(ipAdd);
                Remote = new IPEndPoint(ip, remotePort);
                Local = new IPEndPoint(IPAddress.Any, localPort);
                Udp = new UdpClient(Local);

                Udp.Connect(Remote);
            }

            public void Send(byte[] data)
            {
                Udp.Send(data, data.Length);
            }

            public void StartReceive()
            {
                StopEvent = new ManualResetEvent(false);
                AsyncResult = Udp.BeginReceive(ReceiveCallback, StopEvent);
            }

            private void ReceiveCallback(IAsyncResult ar)
            {
                byte[] data;

                lock (locker)
                {
                    try
                    {
                        data = Udp.EndReceive(ar, ref Remote);
                    }
                    catch (SocketException e)
                    {
                        StopEvent.Set();
                        return;
                    }
                    catch (ObjectDisposedException e)
                    {
                        StopEvent.Set();
                        return;
                    }
                }
                if (DataReceived != null)
                {
                    DataReceived.Invoke(data, Remote);
                }
                lock (locker)
                {
                    Udp.BeginReceive(ReceiveCallback, null);
                }
            }

            public void StopReceive()
            {
                lock (locker)
                {
                    Udp.Close();
                }
                StopEvent.WaitOne();
            }

            public void Close()
            {
                StopReceive();
                Udp.Close();
            }
        }
    }
    namespace TCP
    {
        public delegate void _ReceiveData(byte[] data, TcpClient remote);

        public delegate void _OnConnectSuccess(IPEndPoint remote);
        public delegate void _OnDisconnect(IPEndPoint remote);
        public delegate void _OnConnectFailed(string ip,int port,int errorCode);

        public class TCPBase
        {
            public _OnConnectSuccess OnConnectSuccess;
            public _OnDisconnect OnDisconnect;
            public _OnConnectFailed OnConnectFailed;

            protected int ReadTimeOut = -1;
            protected int WriteTimeOut = -1;
        }

        public class TCPServer : TCPBase
        {
            public _ReceiveData DataReceiveCallback;
            public bool ServerEnabled { get; private set; }
            public bool ListenEnabled { get; private set; }

            private ManualResetEvent ListenStoper;
            private ManualResetEvent ReceiveStoper;

            private IPAddress ListenAddress;
            private int Port;
            private TcpListener Listener;

            private ConcurrentDictionary<string, TCPCilent> Clients;

            private object locker;

            public TCPServer(string ip,int port)
            {
                ListenStoper = new ManualResetEvent(false);
                ReceiveStoper = new ManualResetEvent(false);

                ListenAddress = IPAddress.Parse(ip);
                Port = port;
                Listener = new TcpListener(ListenAddress, Port);
                Clients = new ConcurrentDictionary<string, TCPCilent>();

                locker = new object();

                ServerEnabled = false;
                ListenEnabled = false;
            }

            public void StartListener()
            {
                if (ListenEnabled || ServerEnabled)
                    return;
                ListenEnabled = true;
                ServerEnabled = true;
                ListenStoper.Reset();
                if (Listener == null)
                    return;
                Listener.Start();
                Listener.BeginAcceptTcpClient(ListenCallback, Listener);
            }

            public void StopListener()
            {
                if (!ListenEnabled)
                    return;
                ListenEnabled = false;
                if (Listener == null)
                    return;
                Listener.Stop();
                ListenStoper.WaitOne();
            }

            public void StopServer()
            {
                if (!ServerEnabled)
                    return;
                ServerEnabled = false;
                StopListener();
                lock(locker)
                {
                    foreach(TCPCilent proc in Clients.Values)
                    {
                        proc.Close();
                    }
                    Clients.Clear();
                }
            }

            private void ListenCallback(IAsyncResult ar)
            {
                TcpClient client;
                TcpListener listener = (TcpListener)ar.AsyncState;
                lock (locker)
                {
                    try {
                        client = listener.EndAcceptTcpClient(ar);
                    }catch(ObjectDisposedException e)
                    {
                        ListenStoper.Set();
                        return;
                    }catch(SocketException e)
                    {
                        ListenStoper.Set();
                        return;
                    }
                }

                if (OnConnectSuccess != null)
                    OnConnectSuccess.Invoke((IPEndPoint)client.Client.RemoteEndPoint);

                IPEndPoint endPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                string key = endPoint.Address.ToString() + endPoint.Port.ToString();

                ReceiveProc proc = new ReceiveProc(ReadTimeOut, WriteTimeOut, client, 1024);

                TCPCilent _client = new TCPCilent(proc);

                Clients.TryAdd(key, _client);
                proc.ReceiveDataCallback = DataReceiveCallback;
                proc.OnDisconnect = OnDisconnect;
                proc.ReceiveStart();

                Listener.BeginAcceptTcpClient(ListenCallback, listener);
            }

            public bool Send(string ip,int port,byte[] data)
            {
                ip += port;
                bool result = false;
                lock (locker)
                {
                    if (Clients.ContainsKey(ip))
                    {
                        result = Clients[ip].Send(data);
                        if (!result)
                        {
                            TCPCilent proc;
                            if(!Clients.TryRemove(ip,out proc))
                            {
                                Console.WriteLine(ip + "の削除に失敗");
                            }
                        }
                    }
                }
                return result;
            }

            public void SendAll(byte[] data)
            {
                List<TCPCilent> list = new List<TCPCilent>();
                lock (locker)
                {
                    foreach (TCPCilent proc in Clients.Values)
                    {
                        if (!proc.Send(data))
                        {
                            list.Add(proc);
                        }
                    }
                    for(int n = 0;n < list.Count;n++)
                    {
                        TCPCilent p;
                        if(!Clients.TryRemove(list[n].getKey(),out p))
                        {
                            Console.WriteLine(list[n].getKey() + "の削除に失敗");
                        }
                    }
                }
            }

            public ICollection<TCPCilent> getClients()
            {
                return Clients.Values;
            }
        }

        public class TCPCilent : TCPBase
        {
            private ReceiveProc RecvProc;
            public string Ip { get; private set; }
            public int Port
            {
                get; private set;
            }
            private _ReceiveData ReceiveCallback;
            private int BufferSize;
            
            public bool Connected
            {
                get
                {
                    if (RecvProc == null)
                        return false;
                    return RecvProc.Connected;
                }
            }

            public TCPCilent(string ip,int port,int buffer,_ReceiveData receiveCallback):this(ip,port,buffer)
            {
                ReceiveCallback = receiveCallback;
            }
            
            protected TCPCilent(string ip,int port,int buffer)
            {
                BufferSize = buffer;
                Ip = ip;
                Port = port;
            }

            internal TCPCilent(ReceiveProc proc)
            {
                RecvProc = proc;
                Ip = proc.Remote.Address.ToString();
                Port = proc.Remote.Port;
                ReceiveCallback = proc.ReceiveDataCallback;
                BufferSize = proc.BufferSize;
            }

            public void Connect()
            {
                if (RecvProc != null)
                    return;
                TcpClient client;
                try
                {
                    client = new TcpClient(Ip,Port);
                }
                catch (SocketException e)
                {
                    if (OnConnectFailed != null)
                        OnConnectFailed.Invoke(Ip, Port, e.ErrorCode);
                    return;
                }
                RecvProc = new ReceiveProc(ReadTimeOut, WriteTimeOut, client, 1024);
                RecvProc.ReceiveDataCallback = ReceiveCallback;
                RecvProc.OnDisconnect = OnDisconnect;
                if (OnConnectSuccess != null)
                    OnConnectSuccess.Invoke((IPEndPoint)RecvProc.getRemote().Client.RemoteEndPoint);
            }

            public string getKey()
            {
                if (RecvProc == null)
                    return null;
                return RecvProc.getKey();
            }

            public virtual bool Send(byte[] data)
            {
                if (RecvProc == null)
                    return false; ;
                NetMsg msg = new NetMsg(data);
                bool result = RecvProc.Send(msg);
                return result;
            }
            public virtual void ReceiveStart()
            {
                if (RecvProc == null)
                    return;
                RecvProc.ReceiveStart();
            }
            public virtual void ReceiveEnd()
            {
                RecvProc.ReceiveEnd();
                RecvProc = null;
            }
            
            public void Close()
            {
                RecvProc.Close();
                RecvProc = null;
            }

            protected void setReceveCallback(_ReceiveData callback)
            {
                RecvProc.ReceiveDataCallback = callback;
            }

            protected void addReceiveCallback(_ReceiveData callback)
            {
                RecvProc.ReceiveDataCallback += callback;
            }

            protected ReceiveProc getReceiveProc()
            {
                return RecvProc;
            }
        }

        public class ReceiveProc
        {
            public IPEndPoint Remote { get; private set; }
            public int BufferSize
            {
                get
                {
                    if (Buffer == null)
                        return 0;
                    return Buffer.Length;
                }
            }

            private ManualResetEvent StopEvent;
            private MemoryStream MemoryStream;
            private TcpClient Client;
            private byte[] Buffer;
            public bool Connected { get; private set; }
            public _ReceiveData ReceiveDataCallback;
            public _OnDisconnect OnDisconnect;

            public ReceiveProc(int readTimeout, int writeTimeout, TcpClient client, int buffer)
            {
                Client = client;
                Remote = (IPEndPoint)Client.Client.RemoteEndPoint;
                Client.GetStream().ReadTimeout = readTimeout;
                Client.GetStream().WriteTimeout = writeTimeout;

                this.MemoryStream = new MemoryStream();

                Buffer = new byte[buffer];
                Connected = true;

            }
            public TcpClient getRemote()
            {
                return Client;
            }
            public bool Send(NetMsg msg)
            {
                byte[] data = msg.getBytes();
                try {
                    Client.GetStream().Write(data, 0, data.Length);
                }catch(IOException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    return false;
                }
                catch (SocketException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    return false;
                }
                catch (InvalidOperationException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    return false;
                }
                return true;
            }
            public void ReceiveStart()
            {
                StopEvent = new ManualResetEvent(false);
                Client.GetStream().BeginRead(Buffer, 0, Buffer.Length, ReceiveCallBack, null);
            }
            public void ReceiveEnd()
            {
                try
                {
                    Client.GetStream().Dispose();
                }
                catch (InvalidOperationException)
                {

                }

                StopEvent.WaitOne();
            }
            public void Close()
            {
                ReceiveEnd();
                Client.Close();
            }

            public string getKey()
            {
                IPEndPoint ip = (IPEndPoint)Client.Client.RemoteEndPoint;
                return ip.Address.ToString() + ip.Port;
            }

            private void ReceiveCallBack(IAsyncResult ar)
            {
                int byteCount = 0;
                try
                {
                    byteCount = Client.GetStream().EndRead(ar);
                }
                catch (ObjectDisposedException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                    return;
                }
                catch (SocketException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                    return;

                }
                catch(InvalidOperationException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                    return;
                }
                catch (IOException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                    return;
                }

                MemoryStream.Write(Buffer, 0, byteCount);

                while (true)
                {
                    if (MemoryStream.Length <= 0)
                        break;

                    byte[] data = MemoryStream.GetBuffer();
                    int dataLength = BitConverter.ToInt32(data, 0);

                    if ((dataLength + 4) <= data.Length)
                    {
                        byte[] splitData = new byte[dataLength];
                        //Writeしてポジションが変わっているのでヘッダを飛ばした場所へシーク
                        MemoryStream.Seek(4, SeekOrigin.Begin);
                        //読み込み)
                        MemoryStream.Read(splitData, 0, dataLength);
                        if (ReceiveDataCallback != null)
                        {
                            ReceiveDataCallback.Invoke(splitData,Client);
                        }
                        //MemoryStreamってReadしてもバッファは消えないから
                        //新しいMemoryStreamを作って読み込んだ分は消す
                        MemoryStream ms = new MemoryStream();
                        long buffLength = MemoryStream.Length - MemoryStream.Position;
                        
                        if (buffLength <= 0)
                        {
                            //バッファがちょうど末尾までだったらコピーせず新しく作る
                            ms = new MemoryStream();
                        }
                        else {
                            //現在の読み込み位置から末尾までを新しいバッファにコピーS
                            byte[] buff = new byte[buffLength];

                            MemoryStream.Read(buff, 0, buff.Length);
                            ms.Write(buff, 0, buff.Length);
                        }
                        //バッファ更新
                        MemoryStream = ms;
                    }
                    else
                    {
                        break;
                    }
                }
                try
                {
                    Client.GetStream().BeginRead(Buffer, 0, Buffer.Length, ReceiveCallBack, null);
                }
                catch (ObjectDisposedException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                }
                catch (SocketException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                }
                catch(IOException)
                {
                    Connected = false;
                    if (OnDisconnect != null)
                        OnDisconnect.Invoke(Remote);
                    StopEvent.Set();
                }
            }
        }
    }

    namespace Protocol
    {
        public abstract class NetProtocol
        {
            public const int ProtocolNum = 0;
            private _ReceiveData ReceiveCallback;
            private BinaryFormatter formatter;
            public NetProtocol()
            {
                formatter = new BinaryFormatter();
            }
            public abstract void Reveive(byte[] data,TcpClient client);
            public byte[] getBytes(object obj)
            {
                MemoryStream ms = new MemoryStream();
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
            public abstract byte[] getBytes(byte[] data);
            public abstract int getProtocolNum();
        }

        public class ApplicaionProtocol : NetProtocol
        {
            public delegate void _ReceiveCallback(AppMsg msg);

            public new const int ProtocolNum = 1; 
            private BinaryFormatter formatter;

            public _ReceiveCallback ReceiveCallback;

            public ApplicaionProtocol()
            {
                formatter = new BinaryFormatter();
            }
            public override byte[] getBytes(byte[] data)
            {
                AppMsg msg = new AppMsg();
                msg.data = data;

                return getBytes(msg);
            }

            public override int getProtocolNum()
            {
                return ProtocolNum;
            }

            public override void Reveive(byte[] data, TcpClient client)
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);
                ms.Seek(0, SeekOrigin.Begin);
                AppMsg msg = (AppMsg)formatter.Deserialize(ms);
                if(ReceiveCallback != null)
                {
                    ReceiveCallback.Invoke(msg);
                }
            }

            [Serializable]
            public class AppMsg
            {
                public byte[] data;
            }
        }

        public class PingerProtocol : NetProtocol
        {
            public new const int ProtocolNum = -1;
            private BinaryFormatter formatter;
            public PingerProtocol()
            {
                formatter = new BinaryFormatter();
            }
            public override int getProtocolNum()
            {
                return ProtocolNum;
            }
            public override void Reveive(byte[] data,TcpClient client)
            {
                Console.WriteLine("Ping受信 : "+client.Client.RemoteEndPoint.ToString());
            }
            public override byte[] getBytes(byte[] data)
            {
                PingerMsg ping = new PingerMsg();
                return getBytes(ping);
            }

            [Serializable]
            public class PingerMsg
            {

            }
        }

        public class ProtocolManager
        {
            public delegate void _SendFailed(TCPCilent client);
            private ConcurrentDictionary<int, NetProtocol> Protocols;
            public ProtocolManager()
            {
                Protocols = new ConcurrentDictionary<int, NetProtocol>();
            }
            public void AddProtocol(NetProtocol pro)
            {
                Protocols.TryAdd(pro.getProtocolNum(), pro);
            }
            public bool Send(object obj, int protocolNum, TCPCilent client)
            {
                byte[] protNum = BitConverter.GetBytes(protocolNum);
                byte[] data = Protocols[protocolNum].getBytes(obj);

                MemoryStream ms = new MemoryStream();

                ms.Write(protNum, 0, protNum.Length);
                
                ms.Write(data, 0, data.Length);

                return client.Send(ms.ToArray());
            }

            public void SendAll(object obj,int protocolNum,ICollection<TCPCilent> clients,_SendFailed callback)
            {
                foreach (TCPCilent client in clients) {
                    if(!Send(obj, protocolNum, client))
                    {
                        if(callback != null)
                            callback.Invoke(client);
                    }
                }
            }

            public void Send(object obj, int protocolNum, TCPServer server)
            {
                byte[] protNum = BitConverter.GetBytes(protocolNum);
                byte[] data = Protocols[protocolNum].getBytes(obj);

                MemoryStream ms = new MemoryStream();

                ms.Write(protNum, 0, protNum.Length);

                ms.Write(data, 0, data.Length);

                server.SendAll(ms.ToArray());
            }

            public void divide(byte[] data, TcpClient client)
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);

                //先頭4バイトからプロトコル番号を取得
                int prtNum = BitConverter.ToInt32(ms.GetBuffer(), 0);
                ms.Seek(4, SeekOrigin.Begin);

                //データを分離
                byte[] prtData = new byte[ms.Length - ms.Position];
                ms.Read(prtData, 0, prtData.Length);

                if (!Protocols.ContainsKey(prtNum))
                {
                    return;
                }

                //データをプロトコルで解析、各プロトコルのコールバックが呼ばれる
                Protocols[prtNum].Reveive(prtData,client);
            }

            class ProtocolMsg
            {
                public int ProtocolNum;
                public byte[] data;
            }
        }
    }

    public class NetMsg
    {
        private Int32 DataLength;
        private byte[] Data;
        private MemoryStream MemoryStream;
        public NetMsg(byte[] data)
        {
            DataLength = data.Length;
            Data = data;
            MemoryStream = new MemoryStream();
            byte[] temp = BitConverter.GetBytes(DataLength);
            MemoryStream.Write(temp, 0, temp.Length);
            MemoryStream.Write(Data, 0, Data.Length);
        }
        public byte[] getBytes()
        {
            return MemoryStream.ToArray();
        }
    }

}
