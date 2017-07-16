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

namespace Network
{
    class Program
    {
        static void Main(string[] args)
        {
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

        public static void TCPClientProc()
        {
            Console.Write("ServerIP : ");
            string ip = Console.ReadLine();
            Console.Write("ServerPort : ");
            string port = Console.ReadLine();

            _ReceiveData callback = TCPClientCallback;

            PingClient client = new PingClient(ip, int.Parse(port),1024,callback,1000);
            client.PingStart(); 
            client.ReceiveStart();

            while (true)
            {
                string str = Console.ReadLine();
                if (str.Equals("exit"))
                {
                    break;
                }
                client.Send(Encoding.UTF8.GetBytes(str));
            }
            Console.WriteLine("END...");
            client.Close();
        }

        public static void TCPClientCallback(byte[] data,TcpClient client)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            PingMsg formatter.Deserialize(ms);
            string str = Encoding.UTF8.GetString(data);
            Console.WriteLine("受信 : "+str);
        }

        public static void TCPServerProc()
        {
            Console.Write("ServerIP : ");
            string ip = Console.ReadLine();
            Console.Write("Port : ");
            string port = Console.ReadLine();

            _ReceiveData callback = TCPClientCallback;

            TCPServer server = new TCPServer(ip, int.Parse(port),callback);
            server.StartListener();
            Console.WriteLine("受付開始");
            Console.ReadLine();
            server.StopListener();
            Console.WriteLine("受付終了");
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

        public class TCPServer
        {
            private ManualResetEvent ListenStoper;
            private ManualResetEvent ReceiveStoper;

            private IPAddress ListenAddress;
            private int Port;
            private TcpListener Listener;
            private _ReceiveData DataReceiveCallback;

            private ConcurrentDictionary<string, ReceiveProc> Clients;

            private object locker;

            private int ReadTimeOut = -1;
            private int WriteTimeOut = -1;

            public TCPServer(string ip,int port,_ReceiveData callback)
            {
                ListenStoper = new ManualResetEvent(false);
                ReceiveStoper = new ManualResetEvent(false);

                ListenAddress = IPAddress.Parse(ip);
                Port = port;
                Listener = new TcpListener(ListenAddress, Port);
                Clients = new ConcurrentDictionary<string, ReceiveProc>();

                DataReceiveCallback = callback;

                locker = new object();
            }

            public void StartListener()
            {
                if (Listener == null)
                    return;
                Listener.Start();
                Listener.BeginAcceptTcpClient(ListenCallback, Listener);
            }

            public void StopListener()
            {
                if (Listener == null)
                    return;
                Listener.Stop();
                ListenStoper.WaitOne();
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

                Console.WriteLine("接続を確認");

                IPEndPoint endPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                string key = endPoint.Address.ToString() + endPoint.Port.ToString();

                ReceiveProc proc = new ReceiveProc(ReadTimeOut, WriteTimeOut, client, 1024);

                Clients.TryAdd(key, proc);
                proc.ReceiveDataCallback = DataReceiveCallback;
                proc.ReceiveStart();

                Listener.BeginAcceptTcpClient(ListenCallback, listener);
            }

            public bool Send(string ip,int port,byte[] data)
            {
                ip += port;
                NetMsg msg = new NetMsg(data);
                bool result = false;
                lock (locker)
                {
                    result = Clients[ip].Send(msg);
                }
                return result;
            }

            public void SendAll(byte[] data)
            {
                NetMsg msg = new NetMsg(data);
                lock (locker)
                {
                    foreach (ReceiveProc proc in Clients.Values)
                    {
                        proc.Send(msg);
                    }
                }
            }
        }

        public class TCPCilent
        {
            private ReceiveProc RecvProc;
            private int ReadTimeOut = -1;
            private int WriteTimeOut = -1;

            public TCPCilent(string ip,int port,int buffer,_ReceiveData ReceiveCallback):this(ip,port,buffer)
            {
                RecvProc.ReceiveDataCallback = ReceiveCallback;
            }
            
            protected TCPCilent(string ip,int port,int buffer)
            {
                TcpClient client;
                try
                {
                    client = new TcpClient(ip, port);
                }
                catch (SocketException e)
                {
                    Console.WriteLine("接続に失敗しました");
                    Console.WriteLine("ErrorCode : " + e.ErrorCode);
                    return;
                }
                Console.WriteLine("接続完了");
                RecvProc = new ReceiveProc(ReadTimeOut, WriteTimeOut, client, 1024);
            }

            public virtual bool Send(byte[] data)
            {
                NetMsg msg = new NetMsg(data);
                return RecvProc.Send(msg);
            }
            public virtual void ReceiveStart()
            {
                RecvProc.ReceiveStart();
            }
            public virtual void ReceiveEnd()
            {
                RecvProc.ReceiveEnd();
            }
            public void Close()
            {
                RecvProc.Close();
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

        /*
        public class PingClient : TCPCilent
        {
            private int PingInterval;
            private _ReceiveData ReceiveCallback;
            private Timer PingTimer;
            private byte[] dummy;
            private BinaryFormatter formatter;

            public PingClient(string ip,int port,int buffer,_ReceiveData ReceiveCallback,int pingInterval) : base(ip, port, buffer)
            {
                setReceveCallback(PingCallback);
                PingInterval = pingInterval;
                this.ReceiveCallback = ReceiveCallback;
                dummy = new byte[0];
                formatter = new BinaryFormatter();
            }
            public void PingStart()
            {
                PingTimer = new Timer(new TimerCallback(PingSendCallback));
                PingTimer.Change(0, PingInterval);
            }
            public void PingStop()
            {
                PingTimer.Dispose();
            }
            public override bool Send(byte[] data)
            {
                PingMsg ping = new PingMsg(data);
                NetMsg msg = new NetMsg(ping.getBytes());
                return getReceiveProc().Send(msg);
            }

            public override void ReceiveEnd()
            {
                PingTimer.Dispose();
                PingTimer = null;
                base.ReceiveEnd();
            }

            private void PingSendCallback(object args)
            {
                PingMsg ping = new PingMsg(dummy);
                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, ping);
                    NetMsg msg = new NetMsg(ms.ToArray());
                    Send(msg.getBytes());
                }
            }

            private void PingCallback(byte[] data,TcpClient remote)
            {
                BinaryFormatter formatter = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);
                PingMsg msg;

                try {
                    msg = (PingMsg)formatter.Deserialize(ms);
                }
                catch (Exception)
                {
                    return;
                }

                Console.WriteLine("PINGを受信");

                if (ReceiveCallback != null)
                {
                    ReceiveCallback.Invoke(msg.getBytes(), remote);
                }
            }

            [System.Serializable]
            class PingMsg
            {
                private byte[] data;
                public PingMsg(byte[] data)
                {
                    this.data = data;
                }
                public byte[] getBytes()
                {
                    return data;
                }
            }
        }
        */
        public abstract class NetProtocol
        {
            private _ReceiveData ReceiveCallback;
            public int ProtocolNum { get; private set; }
            public abstract void Reveive(byte[] data);
            public abstract byte[] getBytes(byte[] data);
        }

        public class ApplicaionProtocol
        {
            
        }

        public class PingerProtocol
        {

        }

        public class ProtocolManager
        {
            private ConcurrentDictionary<int, NetProtocol> Protocols;
            public void AddProtocol(NetProtocol pro)
            {
                Protocols.TryAdd(pro.ProtocolNum, pro);
            }
            public bool Send(byte[] data,int protocolNum,TCPCilent client)
            {
                byte[] protNum = BitConverter.GetBytes(protocolNum);

                MemoryStream ms = new MemoryStream();
                ms.Write(protNum, 0, protNum.Length);
                ms.Write(data, 0, data.Length);
                return client.Send(ms.ToArray());
            }

            private void ReveiceCallback(byte[] data,TcpClient client)
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);

                int prtNum = BitConverter.ToInt32(ms.GetBuffer(),0);
                ms.Seek(4, SeekOrigin.Begin);

                byte[] prtData = new byte[ms.Length - ms.Position];
                ms.Read(prtData, 0, prtData.Length);

                if (!Protocols.ContainsKey(prtNum))
                {
                    return;
                }

                Protocols[prtNum].Reveive(prtData);
            }
            
            class ProtocolMsg
            {
                public int ProtocolNum;
                public byte[] data;
            }
        }

        public class ReceiveProc
        { 

            private ManualResetEvent StopEvent;
            private MemoryStream MemoryStream;
            private TcpClient Client;
            private byte[] Buffer;
            public _ReceiveData ReceiveDataCallback;

            public ReceiveProc(int readTimeout, int writeTimeout, TcpClient client, int buffer)
            {
                Client = client;
                Client.GetStream().ReadTimeout = readTimeout;
                Client.GetStream().WriteTimeout = writeTimeout;

                this.MemoryStream = new MemoryStream();

                Buffer = new byte[buffer];

            }
            public bool Send(NetMsg msg)
            {
                byte[] data = msg.getBytes();
                try {
                    Client.GetStream().Write(data, 0, data.Length);
                }catch(IOException)
                {
                    Console.WriteLine("送信に失敗しました。切断されている可能性があります");
                    return false;
                }
                catch (SocketException)
                {
                    Console.WriteLine("送信に失敗しました。切断されている可能性があります");
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
                Client.GetStream().Dispose();
                StopEvent.WaitOne();
            }
            public void Close()
            {
                ReceiveEnd();
                Client.Close();
            }

            private void ReceiveCallBack(IAsyncResult ar)
            {
                int byteCount = 0;
                try
                {
                    byteCount = Client.GetStream().EndRead(ar);
                }
                catch (ObjectDisposedException e)
                {
                    StopEvent.Set();
                }
                catch (SocketException e)
                {
                    StopEvent.Set();
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
                catch (ObjectDisposedException e)
                {
                    StopEvent.Set();
                }
                catch (SocketException e)
                {
                    StopEvent.Set();
                }
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
