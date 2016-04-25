package main

import(
    "net"
    "bufio"
    "fmt"
    "time"
)

func main()  {
    conn,_ := net.Dial("tcp","127.0.0.1:8085")
    defer conn.Close()
    sendData(conn)
    go readData(conn)
    time.Sleep(time.Second*5)
    
}

func readData(conn net.Conn){
    input := bufio.NewScanner(conn)
    fmt.Println("开始接收数据")
    for input.Scan(){
        data := input.Text()
        fmt.Println(data)
    }
    fmt.Println("断开连接")
}

func sendData(conn net.Conn){
    strs:=[]string{"-sub|p1\n","-sub|p2\n","-pub|p1|welcome\n","-pub|p2|china\n","-pub|p1|wolege\n","-pub|p2|hehe\n"}
    for _,v:=range strs{
        _,err:=conn.Write([]byte(v))
        if err==nil{
            fmt.Println(v)
        }
    }
}