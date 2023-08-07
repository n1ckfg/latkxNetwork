using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using NativeWebSocket;

public class LatkNetworkWs : MonoBehaviour {

    [System.Serializable]
    public struct WsPoint {
        public float[] co;
    }

    [System.Serializable]
    public struct WsStroke {
        public string eventname;
        public long timestamp;
        public int index;
        public float[] color;
        public WsPoint[] points;
    }


    public LightningArtist latk;
    public LatkDrawing latkd;
    public enum ProtocolMode { WS, WSS };
    public ProtocolMode protocolMode = ProtocolMode.WS;
    public string serverAddress = "vr.fox-gieg.com";
    public int serverPort = 8080;
    public bool doDebug = true;
    public float scaler = 1f;
    public bool streamToLatk = false;
    public bool armRecordToLatk = false;

    private WebSocket socketManager;
    private string socketAddress;
    private bool connected = false;

    public bool getConnectionStatus() {
        return connected;
    }

	private void Start() {
        if (protocolMode == ProtocolMode.WSS) {
            socketAddress = "wss://";
        } else {
            socketAddress = "ws://";
        }
        socketAddress += serverAddress + ":" + serverPort; // + "/socket.io/:8443";
        initSocketManager(socketAddress);
    }

    private void Update() {
#if !UNITY_WEBGL || UNITY_EDITOR
        socketManager.DispatchMessageQueue();
#endif
    }

    private void LateUpdate() {
        if (streamToLatk && armRecordToLatk) { 
            latkd.recordToLatk();
            foreach (LatkStroke stroke in latkd.strokes) {
                Destroy(stroke.gameObject);
            }
            latkd.strokes = new List<LatkStroke>();
            armRecordToLatk = false;
        }
    }

    private async void initSocketManager(string url) {
        Debug.Log("Connecting to " + url);

        socketManager = new WebSocket(url);

        socketManager.OnError += (e) => {
            Debug.Log("Error! " + e);
        };

        socketManager.OnOpen += () => {
            Debug.Log("Connection open!");
        };

        socketManager.OnClose += (e) => {
            Debug.Log("Connection closed!");
        };

        //socketManager.Socket.On("newFrameFromServer", receivedLocalSocketMessage);
        socketManager.OnMessage += (bytes) => {
            // Reading a plain text message
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Received OnMessage! (" + bytes.Length + " bytes) " + message);
        };
        /*
        socketManager.OnMessage += (bytes) => {
            Debug.Log("!!!");
            // Reading a plain text message
            var message = System.Text.Encoding.UTF8.GetString(bytes);

            if (doDebug) {
                Debug.Log("Received OnMessage! (" + bytes.Length + " bytes) " + message);
            }

            JSONNode data = JSON.Parse(message);

            string eventName = data["eventname"];

            if (doDebug) {
                Debug.Log(DateTime.Now + " - " + "Local Socket Event Name: " + eventName);
            }

            switch (eventName) {
                case "newFrameFromServer":
                    armRecordToLatk = true;
                    if (doDebug) Debug.Log("Receiving new frame " + data[0]["index"] + " with " + data.Count + " strokes.");

                    for (var i = 0; i < data.Count; i++) {
                        List<Vector3> points = getPointsFromJson(data[i]["points"], scaler);
                        latkd.makeCurve(points, latk.killStrokes, latk.strokeLife);
                    }

                    int index = data[0]["index"].AsInt;
                    break;
            }
        };
        */
        await socketManager.Connect();
    }


    public void sendStrokeData(List<Vector3> data) {
		if (!blockSendStroke) StartCoroutine(doSendStrokeData(data));
	}

    private bool blockSendStroke = false;

	private IEnumerator doSendStrokeData(List<Vector3> data) {
        blockSendStroke = true;
		string s = setJsonFromPoints(data);
        if (socketManager.State == WebSocketState.Open) socketManager.SendText(s);
        //socketManager.SendText("clientStrokeToServer", s);
		Debug.Log(s);
		yield return new WaitForSeconds(latk.frameInterval);
        blockSendStroke = false;
	}

    private async void OnApplicationQuit() {
        await socketManager.Close();
        if (doDebug) {
            Debug.Log("Closed connection");
        }
    }

    public List<Vector3> getPointsFromJson(JSONNode ptJson, float scaler) {
        List<Vector3> returns = new List<Vector3>();
        for (int i = 0; i < ptJson.Count; i++) {
            var co = ptJson[i]["co"];

            returns.Add(new Vector3(co[0].AsFloat, co[1].AsFloat, co[2].AsFloat) * scaler);
        }
        return returns;
    }

	public long getUnixTime() {
		System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
		return (long)(System.DateTime.UtcNow - epochStart).TotalMilliseconds;
	}

    public string setJsonFromPoints(List<Vector3> points) {

        WsStroke stroke = new WsStroke();

        stroke.eventname = "clientStrokeToServer";
        stroke.timestamp = getUnixTime();
        stroke.index = latk.layerList[latk.currentLayer].currentFrame;

        stroke.color = new float[3];
        stroke.color[0] = latk.mainColor[0];
        stroke.color[1] = latk.mainColor[1];
        stroke.color[2] = latk.mainColor[2];

        stroke.points = new WsPoint[points.Count];
        for (int i = 0; i < stroke.points.Length; i++) {
            WsPoint point = new WsPoint();
            point.co = new float[3];
            point.co[0] = points[i].x;
            point.co[1] = points[i].y;
            point.co[2] = points[i].z;
            stroke.points[i] = point;
        }

        return JsonUtility.ToJson(stroke);
    }

}
