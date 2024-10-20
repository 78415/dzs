
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ZEGO;

using UnityEngine.XR;
using Unity.XR.PXR;
using System.Net;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt.Utility;
using uPLibrary.Networking.M2Mqtt.Exceptions;

using System.IO.Ports;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;

public class VideoTalk_Step2 : MonoBehaviour
{
    //串口通信c语言函数库
    #region

    [DllImport("serialport")]
    private static extern bool Serial_Open();
    [DllImport("serialport")]
    private static extern void Serial_Close();
    [DllImport("serialport")]
    private static extern int Serial_SendData(byte[] com_data, int size);
    [DllImport("serialport")]
    private static extern int Serial_RecvData(byte[] com_data, int size);
    #endregion

    //音视频定义值
    #region
    ZegoExpressEngine engine;
    string roomID = "0001";
    string playStreamID = "";
    string playUserID = "";
    private ArrayList roomStreamList = new ArrayList();
    RawImageVideoSurface localVideoSurface = null;
    RawImageVideoSurface remoteVideoSurface = null;
    VideoTalk notDestroyObj;
    #endregion

    //MQTT定义值
    #region
    private MqttClient client;
    public PxrSensorState2 SensorState = new PxrSensorState2();
    string mqttMessage;
    public Text head, hand;
    string[] sArray;
    #endregion

    //串口通信定义值
    #region
    Thread portRev, portDeal;
    ArrayList recvBuf;
    public string my_msg;
    //int numi = 1;
    //头部加臂全电机

    byte[] handAndHeadBytes = new byte[38]
       {0x55, 0x55, 0x24,0x03,
        0x10, 0x32, 0x00,
        0x00, 0xA0, 0x05,
        0x01, 0xA0, 0x05,
        0x02, 0x07, 0x02,
        0x03, 0xC4, 0x09,//左大盘 2500
        0x04, 0xA0, 0x06,
        0x05, 0xA0, 0x05,
        0x06, 0xA0, 0x09,
        0x07, 0xF4, 0x01,//右大盘 500
        0x08, 0xDC, 0x05,
        0x09, 0xDC, 0x05, 0x00};//
/*
    byte[] handAndHeadBytes = new byte[22]
       {0x55, 0x55, 0x24,0x03,
        0x10, 0x32, 0x00,
        0x00, 0xA0, 0x05,
        0x01, 0xA0, 0x05,
        0x02, 0x07, 0x02,
        0x03, 0xC4, 0x09,//左大盘 2500
        0x04, 0xA0, 0x06};
*/
    int headIntX, headIntY, Rseta3, Lseta3, buttonR, buttonL, Rseta2, Lseta2;
    float headFX, headFY, handFR, handFL, buttonFR, buttonFL;
    int headIntX2, headIntY2, Rseta3_2, Lseta3_2, Rseta2_2, Lseta2_2, carSingle;
    string strX, strY, StrRseta3, StrLseta3, StrButtonR, StrButtonL, strRseta2, strLseta2, strCarSingle;

    int RcontrastSeta3=0, RcontrastSeta2=0, LcontrastSeta3=0, LcontrastSeta2=0;

    int i = 0;
    byte car;

    #endregion
#if UNITY_ANDROID || UNITY_IPHONE
    DeviceOrientation preOrientation = DeviceOrientation.Unknown;
#endif

    void Start()
    {
        //Time.timeScale = 0.5f;//设置update更新速率
        //音视频初始化
        notDestroyObj = GameObject.Find("DoNotDestroyOnLoad").GetComponent<VideoTalk>();
        engine = notDestroyObj.GetEngine();
        InitAll();
        BindEventHandler();
        LoginRoom();
        StartPreview();
        StartPublishingStream();

        //==================================================================
        //mqtt发布连接
        string username = "vrrobot";
        string password = "2345678910";
        client = new MqttClient(IPAddress.Parse("47.97.104.11"), 1883, false, null);
        // register to message received 
        client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
        string clientId = "OriengePI_receive";
        client.Connect(clientId, username, password);
        client.Subscribe(new string[] { "pico1" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

        //========================================================================
        //串口初始化
        Serial_Open();
        SendData( handAndHeadBytes );

    }

    void Update()
    {
#if UNITY_ANDROID||  UNITY_IPHONE
        if (engine != null)
        {
            if (preOrientation != Input.deviceOrientation)
            {

                if (Input.deviceOrientation == DeviceOrientation.Portrait)
                {
                    engine.SetAppOrientation(ZegoOrientation.ZegoOrientation_0);
                }
                else if (Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown)
                {
                    engine.SetAppOrientation(ZegoOrientation.ZegoOrientation_180);
                }
                else if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft)
                {
                    engine.SetAppOrientation(ZegoOrientation.ZegoOrientation_90);
                }
                else if (Input.deviceOrientation == DeviceOrientation.LandscapeRight)
                {
                    engine.SetAppOrientation(ZegoOrientation.ZegoOrientation_270);
                }
                preOrientation = Input.deviceOrientation;

            }
        }
        
//处理传过来的MQTT字符串
        translateFunc();
//串口发送数据
        SendData( handAndHeadBytes );
        
#endif
    }

    //音视频函数、方法
    #region
    void InitAll()
    {
        ZegoUtilHelper.InitLogView();

        GameObject.Find("Text_RoomID").GetComponent<Text>().text = notDestroyObj.roomID;
        GameObject.Find("Text_RoomState").GetComponent<Text>().text = "Connected";
            
        GameObject previewObj = GameObject.Find("RawImage_Preview");
        if(previewObj != null)
        {
            localVideoSurface = previewObj.AddComponent<RawImageVideoSurface>();
            localVideoSurface.SetCaptureVideoInfo();
            localVideoSurface.SetVideoSource(engine);
        }

        GameObject remoteVideoPlane = GameObject.Find("RawImage_Play");
        if (remoteVideoPlane != null)
        {
            if (remoteVideoSurface == null)//Avoid repeated Add Component causing strange problems such as video freeze
            {
                remoteVideoSurface = remoteVideoPlane.AddComponent<RawImageVideoSurface>();
                remoteVideoSurface.SetPlayVideoInfo(playStreamID);
                remoteVideoSurface.SetVideoSource(engine);
            }
        }
    }

    void BindEventHandler()
    {
        engine.onRoomStateChanged = OnRoomStateChanged;
        engine.onRoomStreamUpdate = OnRoomStreamUpdate;
        engine.onRoomUserUpdate = OnRoomUserUpdate;
        engine.onPublisherStateUpdate = OnPublisherStateUpdate;
        engine.onPlayerStateUpdate = OnPlayerStateUpdate;
        engine.onDebugError = OnDebugError;
    }

    void OnRoomStateChanged(string roomID, ZegoRoomStateChangedReason reason, int errorCode, string extendedData)
    {
        if(roomID == this.roomID)
        {
            // error, return to login page
            if(errorCode != 0)
            {
                SceneManager.LoadScene("VideoTalk_step1");
                return;
            }
        }
    }

    void OnRoomStreamUpdate(string roomID, ZegoUpdateType updateType, List<ZegoStream> streamList, string extendedData)
    {
        streamList.ForEach((stream) =>
        {
            bool isFind = false;
            foreach(ZegoStream stream1 in roomStreamList)
            {
                if(stream.streamID == stream1.streamID)
                {
                    if(updateType == ZegoUpdateType.Delete)
                    {
                        roomStreamList.Remove(stream1);
                    }
                    else
                    {

                    }
                    isFind = true;
                    break;
                }
            }
            if(isFind == false)
            {
                roomStreamList.Add(stream);
            }
        });

        if(playStreamID != "")
        {
            StopPlayingStream(playStreamID);
            playStreamID = "";
        }

        if(roomStreamList.Count > 0)
        {
            playStreamID = ((ZegoStream)roomStreamList[roomStreamList.Count - 1]).streamID;
            playUserID = ((ZegoStream)roomStreamList[roomStreamList.Count - 1]).user.userID;

            GameObject.Find("Text_PlayUserID").GetComponent<Text>().text = playUserID;
            GameObject.Find("Text_PlayStreamID").GetComponent<Text>().text = playStreamID;
            StartPlayingStream(playStreamID);
        }
    }

    void OnRoomUserUpdate(string roomID, ZegoUpdateType updateType, List<ZegoUser> userList, uint userCount)
    {
        if(updateType == ZegoUpdateType.Add)
        {
            userList.ForEach((user)=>{
                ZegoUtilHelper.PrintLogToView(string.Format("user {0} enter room {1}", user.userID, roomID));
            });
        }
        else
        {
            userList.ForEach((user)=>{
                ZegoUtilHelper.PrintLogToView(string.Format("user {0} exit room {1}", user.userID, roomID));
            });
        }
    }

    void OnPublisherStateUpdate(string streamID, ZegoPublisherState state, int errorCode, string extendedData)
    {
        ZegoUtilHelper.PrintLogToView(string.Format("OnPublisherStateUpdate, streamID:{0}, state:{1}, errorCode:{2}, extendedData:{3}", streamID, state, errorCode, extendedData));
    }

    void OnPlayerStateUpdate(string streamID, ZegoPlayerState state, int errorCode, string extendedData)
    {
        ZegoUtilHelper.PrintLogToView(string.Format("OnPlayerStateUpdate, streamID:{0}, state:{1}, errorCode:{2}, extendedData:{3}", streamID, state, errorCode, extendedData));
    }

    void OnDebugError(int errorCode, string funcName, string info)
    {
        ZegoUtilHelper.PrintLogToView(string.Format("OnDebugError, funcName:{0}, info:{1}", errorCode, funcName, info));
    }

    public void LoginRoom()
    {
        ZegoRoomConfig config = new ZegoRoomConfig();
        config.isUserStatusNotify = true;

        ZegoUtilHelper.PrintLogToView(string.Format("LoginRoom, roomID:{0}, userID:{1}, userName:{2}, token:{3}", notDestroyObj.roomID, notDestroyObj.user.userID, notDestroyObj.user.userName, config.token));
        engine.LoginRoom(notDestroyObj.roomID, notDestroyObj.user, config);
    }

    public void StartPreview()
    {
        ZegoUtilHelper.PrintLogToView("StartPreview");
        engine.StartPreview();
    }

    public void StopPreview()
    {
        ZegoUtilHelper.PrintLogToView("StopPreview");
        engine.StopPreview();
    }

    public void StartPublishingStream()
    {
        ZegoUtilHelper.PrintLogToView(string.Format("StartPublishingStream, streamID:{0}", notDestroyObj.publishStreamID));
        engine.StartPublishingStream(notDestroyObj.publishStreamID);
    }

    public void StopPublishingStream()
    {
        ZegoUtilHelper.PrintLogToView(string.Format("StopPublishingStream"));
        engine.StopPublishingStream();
    }

    public void StartPlayingStream(string streamID)
    {
        if (remoteVideoSurface != null)
        {
            remoteVideoSurface.SetPlayVideoInfo(streamID);//Set the pull stream ID you want to display to the current control
        }
        
        ZegoUtilHelper.PrintLogToView(string.Format("StartPlayingStream, streamID:{0}", streamID));
        engine.StartPlayingStream(streamID);
    }

    public void StopPlayingStream(string streamID)
    {
        ZegoUtilHelper.PrintLogToView(string.Format("StopPlayingStream, streamID:{0}", streamID));
        engine.StopPlayingStream(streamID);
    }

    void LoadSceneStep1()
    {
        // load scene video talk room
        SceneManager.LoadScene("VideoTalk_Step1");
    }
    public void OnButtonStop()
    {
        // destroy engine and return to login page
        notDestroyObj.DestroyEngine();
        LoadSceneStep1();
    }
    #endregion


    //定义一个MQTT调用方法
    void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        mqttMessage = Encoding.UTF8.GetString(e.Message) ;
    }

    //串口通信===========================================================================
    //接收函数：
    void PortReceivedThread()
    {
        byte[] buf = new byte[1];
        int n = 0;
        n = Serial_RecvData(buf, 1);
        if (n == 1)
        {
            recvBuf.Add(buf[0]);
        }
        //
        my_msg = recvBuf.Add(buf[0]).ToString();
    }
    void DealDataThread()
    {
    }
    //数据发送：
    void SendData(byte[] msg)
    {
        Serial_SendData(msg, msg.Length);
        head.text = i.ToString();
        i++;
    }
    //关闭串口
    void OnApplicationQuit()
    {
        Debug.Log("loading, 程序退出");
        if (portRev != null)
        {

            if (portRev.IsAlive)
            {
                portRev.Abort();
            }
        }
        if (portDeal != null)
        {
            if (portDeal.IsAlive)
            {
                portDeal.Abort();
            }
        }
        Serial_Close();
    }
    void OnDestroy()
    {
        Debug.Log("scene destroy!");
        if (portRev.IsAlive)
        {
            portRev.Abort();
        }
        if (portDeal.IsAlive)
        {
            portDeal.Abort();
        }
        Serial_Close();
    }
    public byte[] IntToBitConverter(int num)
    {
        handAndHeadBytes = BitConverter.GetBytes(num);
        return handAndHeadBytes;
    }
    public void translateFunc()
    {
        //分割出每个变量
        sArray = mqttMessage.Split('.');
        //head.text = "头显X Y" + sArray[0] + "and" + sArray[1];
        //hand.text = "左右手臂角度" + sArray[2] + "and" + sArray[3];

        //头部=======================================================================
        headIntX = Convert.ToInt32(sArray[0]);
        headIntY = Convert.ToInt32(sArray[1]);
        //限位
        if (headIntX > 90)
        {
            headIntX = 90;
        }
        else if (headIntX < (-90))
        {
            headIntX = (-90);
        }
        if (headIntY > 135)
        {
            headIntY = 135;
        }
        else if (headIntY < (-135))
        {
            headIntY = (-135);
        }
        //转舵机坐标数据
        headIntX2 = (headIntX + 180) * 2000 / 180 - 500;  //X舵机 例如旋转参数int 1500
        headIntY2 = (headIntY + 270) * 2000 / 270 - 500;  //Y舵机
        //转16进制                                                  
        strX = Convert.ToString(headIntX2, 16);
        strY = Convert.ToString(headIntY2, 16);

        //扳机握爪====================================================================
        buttonR = Convert.ToInt32(sArray[5]);
        buttonL = Convert.ToInt32(sArray[4]);
        StrButtonR = Convert.ToString(buttonR, 16);
        StrButtonL = Convert.ToString(buttonL, 16);

        //肩周、大臂==================================================================

        //储存上一帧值
        RcontrastSeta3 = Rseta3_2;
        LcontrastSeta3 = Lseta3_2;
        RcontrastSeta2 = Rseta2_2;
        LcontrastSeta2 = Lseta2_2;

        //这一帧值
        Rseta3 = Convert.ToInt32(sArray[2]);
        Lseta3 = Convert.ToInt32(sArray[3]);
        Rseta2 = Convert.ToInt32(sArray[6]);
        Lseta2 = Convert.ToInt32(sArray[7]);

        //肩盘、大臂数据
        Rseta3_2 = (Rseta3*(-1) + 90) * 2000 / 180 + 500;  //0==>1500 -90==>2500
        Lseta3_2 = (Lseta3 + 90 ) * 2000 / 180 + 500;  //0==>1500  -90==>500

        Rseta2_2 = (Rseta2 + 90) * 2000 / 180 + 1450;//0->2450 -90==> 1450 
        Lseta2_2 = (Lseta2 + 90) * 2000 / 180 - 500;


        if (Math.Abs(RcontrastSeta3 - Rseta3_2) > 700) Rseta3_2 = (RcontrastSeta3 + Rseta3_2) / 2;
        if (Math.Abs(LcontrastSeta3 - Lseta3_2) > 700) Lseta3_2 = (LcontrastSeta3 + Lseta3_2) / 2;
        if (Math.Abs(RcontrastSeta2 - Rseta2_2) > 700) Rseta2_2 = (RcontrastSeta2 + Rseta2_2) / 2;
        if (Math.Abs(LcontrastSeta2 - Lseta2_2) > 700) Lseta2_2 = (LcontrastSeta2 + Lseta2_2) / 2;
        

        StrRseta3 = Convert.ToString(Rseta3_2, 16);
        StrLseta3 = Convert.ToString(Lseta3_2, 16);
        strRseta2 = Convert.ToString(Rseta2_2, 16);
        strLseta2 = Convert.ToString(Lseta2_2, 16);

        //小车移动数据
        carSingle = Convert.ToInt32(sArray[8]);
        // strCarSingle = Convert.ToString(carSingle, 16);
        // car = strToHexByte(strCarSingle)[1];
        // hand.text = strCarSingle;
        //hand.text = car.ToString();

        //帧头
        //handAndHeadBytes[0] = 0x55;
        //handAndHeadBytes[1] = 0x55;

        //数据长度
        //handAndHeadBytes[2] = 0x23;

        //指令 03表示舵机移动指令
        //handAndHeadBytes[3] = 0x03;

        //指令参数
        //控制舵机个数
        //handAndHeadBytes[4] = 0x10;

        //时间低八位、高八位
        handAndHeadBytes[5] = 0x32;
        handAndHeadBytes[6] = 0x00;


        //00 左一舵机手爪
        //handAndHeadBytes[7] = 0x00;
        handAndHeadBytes[8] = strToHexByte(StrButtonL)[0];
        handAndHeadBytes[9] = strToHexByte(StrButtonL)[1];

        /*
                //01 左二舵机小臂
                handAndHeadBytes[10] = 0x01;
                handAndHeadBytes[11] = 0xA0;
                handAndHeadBytes[12] = 0x05;
        */

        //02 左三舵机大臂
        //handAndHeadBytes[13] = 0x02;
        handAndHeadBytes[14] = strToHexByte(strLseta2)[0];
        handAndHeadBytes[15] = strToHexByte(strLseta2)[1];

        // 03 左肩周大盘
        //handAndHeadBytes[16] = 0x03;
        handAndHeadBytes[17] = strToHexByte(StrLseta3)[0];
        handAndHeadBytes[18] = strToHexByte(StrLseta3)[1];

        //04 右一舵机夹子
        //handAndHeadBytes[19] = 0x04;
        handAndHeadBytes[20] = strToHexByte(StrButtonR)[0];
        handAndHeadBytes[21] = strToHexByte(StrButtonR)[1];
        /*
                //05 右二舵机小臂
                handAndHeadBytes[22] = 0x05;
                handAndHeadBytes[23] = 0xA0;
                handAndHeadBytes[24] = 0x05;
        */
        //06 右三舵机大臂
        //handAndHeadBytes[25] = 0x06;
        handAndHeadBytes[26] = strToHexByte(strRseta2)[0];
        handAndHeadBytes[27] = strToHexByte(strRseta2)[1];

        // 07 右肩周大盘
        //handAndHeadBytes[28] = 0x07;
        handAndHeadBytes[29] = strToHexByte(StrRseta3)[0];
        handAndHeadBytes[30] = strToHexByte(StrRseta3)[1];

        // 08 头部1号舵机参数
        //handAndHeadBytes[31] = 0x08;
        handAndHeadBytes[32] = strToHexByte(strX)[0];
        handAndHeadBytes[33] = strToHexByte(strX)[1];

        // 09 头部2号舵机参数
        //handAndHeadBytes[34] = 0x09;
        handAndHeadBytes[35] = strToHexByte(strY)[0];
        handAndHeadBytes[36] = strToHexByte(strY)[1];

        //小车移动
        handAndHeadBytes[37] = (byte)carSingle;
        hand.text = handAndHeadBytes[37].ToString();
    }
    public static byte[] strToHexByte(string hexString)
    {
        if ((hexString.Length % 2) != 0)
        {
            hexString = hexString.Insert(0, 0.ToString()); //如果长度为奇，在开头补零
        }
        byte[] returnBytes = new byte[hexString.Length / 2];//byte[]数组
        byte[] c = new byte[1];
        for (int i = 0; i < returnBytes.Length; i++)
        {
            returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);//每两个字符转byte,放入数组元素中。
        }
        c[0] = returnBytes[0];
        returnBytes[0] = returnBytes[1];
        returnBytes[1] = c[0];
        return returnBytes;
    }

    public void deltaTime2()
    {
        //分割出每个变量
        sArray = mqttMessage.Split('.');
        // head.text = "头显X Y" + sArray[0] + "and" + sArray[1];
        // hand.text = "左右手臂角度" + sArray[2] + "and" + sArray[3];
        //对字符串转换成十进制 int 整数。
        headFX = Convert.ToInt32(sArray[0]) * Time.deltaTime * 50;
        headFY = Convert.ToInt32(sArray[1]) * Time.deltaTime * 50;
        handFR = Convert.ToInt32(sArray[2]) * Time.deltaTime * 50;
        handFL = Convert.ToInt32(sArray[3]) * Time.deltaTime * 50;
        buttonFL = Convert.ToInt32(sArray[4]) * Time.deltaTime * 50;
        buttonFR = Convert.ToInt32(sArray[5]) * Time.deltaTime * 50;

        headIntX = (int)(headFX);
        headIntY = (int)(headFY);
        Rseta3 = (int)(handFR);
        Lseta3 = (int)(handFL);
        buttonL = (int)(buttonFL);
        buttonR = (int)(buttonFR);

    }
}
