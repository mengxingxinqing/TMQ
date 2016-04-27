using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Util
{
    public class TcpClientState
    {
        public TcpClient TcpClient { set; get; }

        public byte[] Buffer { set; get; }

        public NetworkStream NetworkStream { set; get; }

        public List<string> SubList;

        public TcpClientState(TcpClient client,byte[] buff)
        {
            Buffer = buff;
            TcpClient = client;
            NetworkStream = TcpClient.GetStream();
            SubList = new List<string>();
        }

        /// <summary>
        /// 添加客户端关注项
        /// </summary>
        /// <param name="key"></param>
        public void Subscribe(string key)
        {
            SubList.Add(key);  
        }
        /// <summary>
        /// 检查客户端是否关注某项
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool CheckSubTopic(string key)
        {
            string str = SubList.FirstOrDefault(p => p == key);
            return !string.IsNullOrEmpty(str);
        }
    }
}
