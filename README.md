#TMQ简单的消息转发工具  
我们在日常网站开发工作中总是要遇到一些网站本身处理不了，或者不应该由网站
来处理的工作，比如网站下单，小票机跟着进行打单，web本身是做不了的，因此我们需要
网站能够把信息转发出去，由其他的程序进行后续的处理，可以使一些容易堆积起来的地方
能够并行起来

###使用说明
1. 接收pub的数据格式 -pub|channel1|msg1
2. 接收sub的数据格式 -sub|channel1
  
###使用示例
1. 使用php来发布一个消息

        $ip = "127.0.0.1";
        $port = "3888";
        $socket = socket_create(AF_INET, SOCK_STREAM, SOL_TCP);
        $result = socket_connect($socket, $ip, $port);
        $in = "-pub|channel1|msg1";
        socket_write($socket, $in, strlen($in));
        socket_close($socket);

2. 使用.Net接收某个通道的消息  
下面的示例基于一个c#的异步客户端类，在项目的源码里有TcpClientHelper.cs
在调用的时候
        _client = new TcpClientHelper(QueueIp, QueuePort);
        _client.DataArrivedEvent += DataArrivedEvent;
        _client.ConnectEvent += ConnectEvent;
        _client.ReConnectEvent += ReConnectEvent;
        _client.Connect();
        //关注某个消息通道
        public bool ConnectEvent(string ip,int port)
        {
            try
            {
                 _client.Subscribe("channel1");
            }
            catch (Exception ex)
            {
                LogHelper.Log("sfclient", "服务器连接异常", "详情：" + ex.Message);
            }
            
            return true;
        }
       
        //然后在实现的DataArrivedEvent中处理服务器消息
        public bool DataArrivedEvent(string ip,int port,string msg)
        {
            try
            {
                //处理消息函数，自己实现
                ProcessMsg(msg);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Log("sfclient", "监听异常","详情："+ex.Message);
            }
            return false;
        }
       
3. 使用golang 做客户端收发示例，源码中client.go中有示例
***
###其他语言
只要基于tcp通信，按照对应的格式发送接收数据，应该都没有问题        
    

