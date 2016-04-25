/**
程序接收pub消息格式 -pub|channel|msg
程序接收sub消息格式 -sub|channel
**/
package main

import (
	"bufio"
	"container/list"
	"fmt"
	"net"
	"strings"
	"sync/atomic"
	"time"
)

var globalSessionID uint64

const(
	bodySplit = "-"
	contentSplit = "|"
)

// Session is client struct
type Session struct {
	ID          uint64
	Conn        net.Conn
	SubList     *list.List
	RequestMsg  chan string
	ResponseMsg chan string
	LeaveMsg    chan int
}

var sessionList *list.List
var subMap map[string]*list.List

func main() {
	sessionList = list.New()
	subMap = make(map[string]*list.List)
	serverAddr, err := net.ResolveTCPAddr("tcp4", "127.0.0.1:8085")
	checkError(err, "初始化失败")
	listener, err := net.ListenTCP("tcp4", serverAddr)
	defer listener.Close()
	checkError(err, "监听失败")
	fmt.Println("服务器已正常启动")
	for {
		conn, err := listener.Accept()
		checkError(err, "连接接收失败")
		go handleLink(conn)
	}
}

func checkError(error error, info string) {
	if error != nil {
		panic("error: " + info + " " + error.Error())
	}
}

//handleLink 处理连接
func handleLink(conn net.Conn) {
	id := atomic.AddUint64(&globalSessionID, 1)
	session := Session{ID: id, Conn: conn, SubList: list.New(), RequestMsg: make(chan string), ResponseMsg: make(chan string), LeaveMsg: make(chan int)}
	sessionList.PushBack(session)
	log(session, "connected")
	go tcpRequest(session)
	go tcpResponse(session)
	for {
		select {
		case v, ok := <-session.RequestMsg:
			if ok {
				processRequests(session, v)
			}
		case _, ok := <-session.LeaveMsg:
			if ok {
				tcpLeave(session)
			}
		}
	}
}

//tcpRequest 接收tcp数据
func tcpRequest(session Session) {
	input := bufio.NewScanner(session.Conn)
	for input.Scan() {
		data := input.Text()
		session.RequestMsg <- data
	}
	err := session.Conn.Close()
	if err == nil {
		session.LeaveMsg <- 1
	}
}

//tcpResponse 响应tcp请求
func tcpResponse(session Session) {
	for {
		msg := <-session.ResponseMsg
		session.Conn.Write([]byte(msg))
	}
}

//tcpLeave 断开连接
func tcpLeave(session Session) {
	session.Conn.Close()
	listRemove(sessionList, session)
	for e := session.SubList.Front(); e != nil; e = e.Next() {
		listRemove(subMap[e.Value.(string)], session)
	}
	log(session, "leave")
}

//listRemove 删除列表中元素
func listRemove(list *list.List, session Session) {
	if list == nil || list.Len() == 0 {
		return
	}
	for e := list.Front(); e != nil; e = e.Next() {
		if e.Value.(Session).ID == session.ID {
			list.Remove(e)
		}
	}
}

//processRequests 防止粘包
func processRequests(session Session, str string) {
	for _, v := range strings.Split(str, bodySplit) {
		if v != "" {
			processRequest(session, v)
		}
	}
}

//processRequest 处理请求
func processRequest(session Session, str string) {
	arr := strings.Split(str, contentSplit)
	log(session, str)
	if len(arr) < 2 {
		return
	}
	cmd := arr[0]
	key := arr[1]
	if len(arr) == 2 && cmd == "sub" {
		addToSubList(key, session)
	} else if len(arr) == 3 && cmd == "pub" {
		//向客户端管道发送数据
		sendToSubList(key, arr[2])
	} else if len(arr) == 2 && cmd == "unsub" {
		if subMap[key] != nil {
			listRemove(subMap[key], session)
		}
	}
}

//makeMsg 构造响应客户端的消息结构
func makeMsg(str string) string {
	return "-pub|" + str + "\n"
}

//currTime 当前时间
func currTime() string {
	return time.Now().Format("2006-01-02 15:04:05")
}

//sendToSubList 给用户队列发送消息
func sendToSubList(key string, msg string) {
	if subMap[key] != nil {
		for e := subMap[key].Front(); e != nil; e = e.Next() {
			fmt.Println("send data = ", msg)
			e.Value.(Session).ResponseMsg <- makeMsg(msg)
		}
	}
}

//addToSubList 用户添加关注
func addToSubList(key string, session Session) {
	if subMap[key] == nil {
		subMap[key] = list.New()
	}
	subMap[key].PushBack(session)
	session.SubList.PushBack(key)
}

func log(session Session, msg string) {
	fmt.Println("[", session.Conn.RemoteAddr().String(), "] ", currTime(), "\n", msg)
}
