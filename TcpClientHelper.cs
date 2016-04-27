using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Util
{
    /// <summary>
    /// 异步TCP客户端
    /// </summary>
    public class TcpClientHelper : IDisposable
    {
        #region Fields

        private TcpClient tcpClient;
        private bool disposed;
        private int retries;

        private bool IsTcpChange = false;

        #endregion
        #region Event
        public delegate bool DataArrivedDelegate(string ip,int port,string msg);
        public event DataArrivedDelegate DataArrivedEvent;

        public delegate bool ConnectedDelegate(string ip,int port);
        public event ConnectedDelegate ConnectEvent;

        public delegate bool ReConnectDelegate();

        public event ReConnectDelegate ReConnectEvent;

        #endregion

        #region 网络监测
        [DllImport("sensapi.dll")]
        private extern static bool IsNetworkAlive(out int connectionDescription);

        private bool threadRun = true;
        private Thread tcpCheckThread;
        #endregion

        #region Ctors

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteEP">远端服务器终结点</param>
        public TcpClientHelper(IPEndPoint remoteEP)
          : this(new[] { remoteEP.Address }, remoteEP.Port)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteEP">远端服务器终结点</param>
        /// <param name="localEP">本地客户端终结点</param>
        public TcpClientHelper(IPEndPoint remoteEP, IPEndPoint localEP)
          : this(new[] { remoteEP.Address }, remoteEP.Port, localEP)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddress">远端服务器IP地址</param>
        /// <param name="remotePort">远端服务器端口</param>
        public TcpClientHelper(IPAddress remoteIPAddress, int remotePort)
          : this(new[] { remoteIPAddress }, remotePort)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddress">远端服务器IP地址</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public TcpClientHelper(
          IPAddress remoteIPAddress, int remotePort, IPEndPoint localEP)
          : this(new[] { remoteIPAddress }, remotePort, localEP)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteHostName">远端服务器主机名</param>
        /// <param name="remotePort">远端服务器端口</param>
        public TcpClientHelper(string remoteHostName, int remotePort)
          : this(Dns.GetHostAddresses(remoteHostName), remotePort)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteHostName">远端服务器主机名</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public TcpClientHelper(
          string remoteHostName, int remotePort, IPEndPoint localEP)
          : this(Dns.GetHostAddresses(remoteHostName), remotePort, localEP)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddresses">远端服务器IP地址列表</param>
        /// <param name="remotePort">远端服务器端口</param>
        public TcpClientHelper(IPAddress[] remoteIPAddresses, int remotePort)
          : this(remoteIPAddresses, remotePort, null)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddresses">远端服务器IP地址列表</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public TcpClientHelper(
          IPAddress[] remoteIPAddresses, int remotePort, IPEndPoint localEP)
        {
            Addresses = remoteIPAddresses;
            Port = remotePort;
            LocalIPEndPoint = localEP;
            Encoding = Encoding.Default;

            if (LocalIPEndPoint != null)
            {
                tcpClient = new TcpClient(LocalIPEndPoint);
            }
            else
            {
                tcpClient = new TcpClient();
            }
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes((uint)15000).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
            BitConverter.GetBytes((uint)15000).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
            tcpClient.Client.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);

            Retries = 3;
            RetryInterval = 5;
            tcpCheckThread = new Thread(NetCheckThread);
            tcpCheckThread.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// 是否已与服务器建立连接
        /// </summary>
        public bool Connected { get { return tcpClient.Client.Connected; } }
        /// <summary>
        /// 远端服务器的IP地址列表
        /// </summary>
        public IPAddress[] Addresses { get; private set; }
        /// <summary>
        /// 远端服务器的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 连接重试次数
        /// </summary>
        public int Retries { get; set; }
        /// <summary>
        /// 连接重试间隔
        /// </summary>
        public int RetryInterval { get; set; }
        /// <summary>
        /// 远端服务器终结点
        /// </summary>
        public IPEndPoint RemoteIPEndPoint
        {
            get { return new IPEndPoint(Addresses[0], Port); }
        }
        /// <summary>
        /// 本地客户端终结点
        /// </summary>
        protected IPEndPoint LocalIPEndPoint { get; private set; }
        /// <summary>
        /// 通信所使用的编码
        /// </summary>
        public Encoding Encoding { get; set; }

        #endregion

        #region Connect

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <returns>异步TCP客户端</returns>
        public TcpClientHelper Connect()
        {
            if (Connected) return this;
            // start the async connect operation
            try
            {
                LogHelper.Log("test","消息","connect");
                tcpClient.BeginConnect(
                    Addresses, Port, HandleTcpServerConnected, tcpClient);
            }
            catch (Exception ex)
            {
                LogHelper.Log(GetType().Name,"服务器连接异常","详情"+ex.Message);
            }

            return this;
        }

        /// <summary>
        /// 关闭与服务器的连接
        /// </summary>
        /// <returns>异步TCP客户端</returns>
        public TcpClientHelper Close()
        {
            if (Connected)
            {
                retries = 0;
                tcpClient.Close();
                threadRun = false;
                //断开服务器连接
                LogHelper.Log(GetType().Name, "系统消息", "断开服务器连接");
            }

            return this;
        }

        #endregion

        #region Receive
        /// <summary>
        /// 连接上服务器触发事件
        /// </summary>
        /// <param name="ar"></param>
        private void HandleTcpServerConnected(IAsyncResult ar)
        {
            try
            {
                tcpClient.EndConnect(ar);
                ConnectEvent?.Invoke(Addresses[0].ToString(), Port);
                retries = 0;
            }
            catch (Exception ex)
            {
                //连接异常处理
                if (retries > 0)
                {
                    LogHelper.Log(GetType().Name,"连接异常", "开始重新连接" + retries);
                }

                retries++;
                if (retries > Retries)
                {
                    //尝试次数超出
                    LogHelper.Log(GetType().Name,"连接失败", "已尝试连接"+retries+"次");
                    return;
                }
                Thread.Sleep(TimeSpan.FromSeconds(RetryInterval));
                Connect();
                return;
            }

            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            tcpClient.GetStream().BeginRead(
              buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
        }

        /// <summary>
        /// 服务器接收数据处理
        /// </summary>
        /// <param name="ar"></param>
        private void HandleDatagramReceived(IAsyncResult ar)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();

                int numberOfReadBytes = 0;
                try
                {
                    numberOfReadBytes = stream.EndRead(ar);
                }
                catch
                {
                    numberOfReadBytes = 0;
                }

                if (numberOfReadBytes == 0)
                {
                    LogHelper.Log("test","关闭点1", "关闭点1");
                    Close();
                    return;
                }

                byte[] buffer = (byte[])ar.AsyncState;
                byte[] receivedBytes = new byte[numberOfReadBytes];
                Buffer.BlockCopy(buffer, 0, receivedBytes, 0, numberOfReadBytes);
                IPEndPoint ip = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
                string str = Encoding.Default.GetString(receivedBytes);
                LogHelper.Log(GetType().Name, "服务端消息", str);
                DataArrivedEvent?.Invoke(ip.Address.ToString(), ip.Port, str);
                stream.BeginRead(
                  buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
            }
            catch (Exception ex)
            {
                LogHelper.Log(GetType().Name,"系统异常",ex.Message);
            }
            
        }

        private void NetCheckThread()
        {
            while (threadRun)
            {
                int flags;//上网方式          
                bool mBOnline = IsNetworkAlive(out flags);
                if (mBOnline)
                {
                    if (IsTcpChange)
                    {
                        IsTcpChange = false;
                        ReConnectEvent?.Invoke();
                        LogHelper.Log("test","信息","断网重连 "+Connected);
                    }
                }
                else
                {
                    IsTcpChange = true;
                }
                Thread.Sleep(1000);
            }
        }

        #endregion

        #region Send

        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(byte[] datagram)
        {
            if (datagram == null)
                throw new ArgumentNullException("datagram");

            if (!Connected)
            {
                //服务器断开连接
                LogHelper.Log(GetType().Name,"系统异常", "服务器连接已断开");
            }

            tcpClient.GetStream().BeginWrite(
              datagram, 0, datagram.Length, HandleDatagramWritten, tcpClient);
        }

        private void HandleDatagramWritten(IAsyncResult ar)
        {
            ((TcpClient)ar.AsyncState).GetStream().EndWrite(ar);
        }

        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(string datagram)
        {
            Send(Encoding.GetBytes(datagram));
        }
        /// <summary>
        /// 客户端关注
        /// </summary>
        /// <param name="topic"></param>
        public void Subscribe(string topic)
        {
            string msg = "subscribe#" + topic;
            Send(msg);
        }
        /// <summary>
        /// 客户端发布消息
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="msg"></param>
        public void Publish(string topic,string msg)
        {
            string content = "publish#" + topic+"#"+msg;
            Send(content);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed 
        /// and unmanaged resources; <c>false</c> 
        /// to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try
                    {
                        LogHelper.Log("test","关闭点2","关闭点2");
                        Close();

                        if (tcpClient != null)
                        {
                            tcpClient = null;
                        }
                    }
                    catch (SocketException)
                    {
                        
                    }
                }

                disposed = true;
            }
        }

        #endregion
    }
}
