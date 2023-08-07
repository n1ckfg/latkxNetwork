using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using NativeWebSocket;
using System.IO;

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
    private List<int> oldIds = new List<int>();
    private int maxIds = 10;

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
            if (doDebug) {
                Debug.Log("Received (" + bytes.Length + " bytes) " + message);
            }

            JSONNode data = JSON.Parse(message);


            //string eventName = data["eventname"];

            //if (doDebug) {
            //Debug.Log(DateTime.Now + " - " + "Local Socket Event Name: " + eventName);
            //}


            //armRecordToLatk = true;

            Color color = getColorFromJson(data["colors"]);
            List<Vector3> points = getPointsFromJson(data["points"], scaler);
            //latkd.makeCurve(points, latk.killStrokes, latk.strokeLife);
            //latk.inputInstantiateStroke(color, points);

            int index = data["index"].AsInt;

            if (points.Count > 1) {
                StartCoroutine(doInstantiateStroke(index, color, points));
            }
        };

        await socketManager.Connect();
    }

    private IEnumerator doInstantiateStroke(int index, Color color, List<Vector3> points) {
        bool newStroke = true;
        for (int i = 0; i < oldIds.Count; i++) {
            if (index == oldIds[i]) {
                newStroke = false;
                break;
            }
        }

        if (newStroke) {
            latkd.color = color;
            latkd.makeCurve(points, latk.killStrokes, latk.strokeLife);
            //latk.inputInstantiateStroke(color, points);
            oldIds.Add(index);
            if (oldIds.Count > maxIds) oldIds.RemoveAt(0);
        }

        //armReceiver = false;
        yield return null;
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

    public Color getColorFromJson(JSONNode colorJson) {
        return bytesToColor(Convert.FromBase64String(colorJson));
    }

    public List<Vector3> getPointsFromJson(JSONNode pointsJson, float scaler) {
        return bytesToVec3s(Convert.FromBase64String(pointsJson));
    }

    Color bytesToColor(byte[] bytes) {
        MemoryStream stream = new MemoryStream(bytes);
        BinaryReader br = new BinaryReader(stream);
        Vector3 v = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) / 255f;

        return new Color(v.x, v.y, v.z);
    }

    List<Vector3> bytesToVec3s(byte[] bytes) {
        List<Vector3> returns = new List<Vector3>();

        MemoryStream stream = new MemoryStream(bytes);
        BinaryReader br = new BinaryReader(stream);
        int len = (int)(bytes.Length / 12);
        for (int i = 0; i < len; i++) {
            Vector3 v = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) / 500f;
            returns.Add(new Vector3(v.x, v.y, v.z));
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
