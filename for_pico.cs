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
using UnityEngine.XR.Interaction.Toolkit;

public class VideoTalk_Step3 : MonoBehaviour
{
    public Text R, L;

    ZegoExpressEngine engine;
    string roomID = "0001";
    public string playStreamID = "";
    string playUserID = "";
    private ArrayList roomStreamList = new ArrayList();
    RawImageVideoSurface localVideoSurface = null;
    RawImageVideoSurface remoteVideoSurface = null;
    VideoTalk notDestroyObj;

    //==================================================================
    private MqttClient client;
    // double predictTimeMs = 0;
    public PxrSensorState2 SensorState = new PxrSensorState2();
    int sensorFrameIndex = new int();
    float headAngleX, headAngleY,
          fPRX, fPRY, fPRZ,
          fPLX, fPLY, fPLZ/*,
          fRRX, fRRY, fRRZ, fRRW,
          fRLX, fRLY, fRLZ, fRLW*/
          ;
    public PXR_Input.Controller rightController, leftController;
    public XRController XRController_right, XRController_left;

    int headIntX, headIntY,
        PRX, PRY, PRZ,
        PLX, PLY, PLZ,
        Rrad,Lrad/*,
        RRX, RRY, RRZ, RRW,
        RLX, RLY, RLZ, RLW*/;
    Vector3 handPositionRight = new Vector3(), handPositionLeft = new Vector3();
    // Quaternion handRotationRight = new Quaternion(), handRotationLeft = new Quaternion();
    //===================================================================

    string mqttData;



#if UNITY_ANDROID || UNITY_IPHONE
    DeviceOrientation preOrientation = DeviceOrientation.Unknown;
#endif

    void Start()
    {
        notDestroyObj = GameObject.Find("DoNotDestroyOnLoad").GetComponent<VideoTalk>();
        engine = notDestroyObj.GetEngine();
        InitAll();
        BindEventHandler();
        LoginRoom();
        // Start preview
        StartPreview();
        // Start publish stream
        StartPublishingStream();

        //==================================================================
        //mqttPublish()
        string username = "TT";
        string password = "2345678910";
        client = new MqttClient(IPAddress.Parse("120.55.57.5"), 1883, false, null);
        // register to message received 
        client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
        string clientId = "Pico_VR";
        client.Connect(clientId, username, password);
        // subscribe to the topic "/home/temperature" with QoS 2 
        client.Subscribe(new string[] { "pico1" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        //=======================================================================
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
        //===============================================
        getHeadPosData();
        getHandPosData();
        //getHandRotData();

        mqttData =
              headIntX + "." + headIntY + "." + Rrad + "."+ Lrad;

        client.Publish("pico1", System.Text.Encoding.UTF8.GetBytes(mqttData), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
        //=================================================


#endif
    }

    /// <summary>
    /// 音视频函数
    /// </summary>
    #region
    void InitAll()
    {
        ZegoUtilHelper.InitLogView();

        GameObject.Find("Text_RoomID").GetComponent<Text>().text = notDestroyObj.roomID;
        GameObject.Find("Text_RoomState").GetComponent<Text>().text = "Connected";

        GameObject previewObj = GameObject.Find("RawImage_Preview");
        if (previewObj != null)
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
        if (roomID == this.roomID)
        {
            // error, return to login page
            if (errorCode != 0)
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
            foreach (ZegoStream stream1 in roomStreamList)
            {
                if (stream.streamID == stream1.streamID)
                {
                    if (updateType == ZegoUpdateType.Delete)
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
            if (isFind == false)
            {
                roomStreamList.Add(stream);
            }
        });

        if (playStreamID != "")
        {
            StopPlayingStream(playStreamID);
            playStreamID = "";
        }

        if (roomStreamList.Count > 0)
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
        if (updateType == ZegoUpdateType.Add)
        {
            userList.ForEach((user) => {
                ZegoUtilHelper.PrintLogToView(string.Format("user {0} enter room {1}", user.userID, roomID));
            });
        }
        else
        {
            userList.ForEach((user) => {
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



    //=========================================================
    //定义一个MQTT调用方法
    void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        Debug.Log("Received: " + System.Text.Encoding.UTF8.GetString(e.Message));
    }

    public void getHeadPosData()
    {
        PXR_Plugin.Pxr_GetPredictedMainSensorState2(0, ref SensorState, ref sensorFrameIndex);
        headAngleX += SensorState.angularVelocity.x;
        headAngleY += SensorState.angularVelocity.y;

        headIntX = (int)(headAngleX * 10 / 15.745);
        headIntY = (int)(headAngleY * 10 / 15.745);
    }

    public void getHandPosData()
    {

        //位移信息
        handPositionRight = PXR_Input.GetControllerPredictPosition(rightController, 100);
        fPRX += handPositionRight.x;
        fPRY += handPositionRight.y;
        fPRZ += handPositionRight.z;
        PRX = (int)(fPRX / 1550);
        PRY = (int)(fPRY / 1550);
        PRZ = (int)(fPRZ / 1550)+ 60;
        Rrad = (int)(Mathf.Atan2(PRY, PRZ) * 57.3);
        if (Rrad > 90)
            Rrad = 90;
        else if (Rrad < (-90))
            Rrad = (-90);
        R.text = "右手柄数值" + Rrad + "=arctan(" + PRY + "/" + PRZ + ")";

        handPositionLeft = PXR_Input.GetControllerPredictPosition(leftController, 100);
        fPLX += handPositionLeft.x;
        fPLY += handPositionLeft.y;
        fPLZ += handPositionLeft.z;
        PLX = (int)(fPLX / 1550);
        PLY = (int)(fPLY / 1550);
        PLZ = (int)(fPLZ / 1550)+60;
        Lrad = (int)(Mathf.Atan2(PLY, PLZ) * 57.3);
        if (Lrad > 90)
            Lrad = 90;
        else if (Lrad < (-90))
            Lrad = (-90);
        L.text = "左手柄数值" + Lrad + "=arctan(" + PLY + "/" + PLZ + ")";
    }
/*
    public void getHandRotData()
    {
        //旋转信息
        handRotationRight = PXR_Input.GetControllerPredictRotation(rightController, 100);
        fRRX = handRotationRight.x;
        fRRY = handRotationRight.y;
        fRRZ = handRotationRight.z;
        fRRW = handRotationRight.w;
        RRX = (int)(fRRX / 10);
        RRY = (int)fRRY / 10;
        RRZ = (int)fRRZ / 10;
        RRW = (int)fRRW / 10;

        handRotationLeft = PXR_Input.GetControllerPredictRotation(leftController, 100);
        fRLX = handRotationLeft.x;
        fRLY = handRotationLeft.y;
        fRLZ = handRotationLeft.z;
        fRLW = handRotationLeft.w;
        RLX = (int)fRLX / 10;
        RLY = (int)fRLY / 10;
        RLZ = (int)fRLZ / 10;
        RLW = (int)fRLW / 10;
    }
*/

}