using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using BestHTTP.SocketIO;

public class LatkNetwork : MonoBehaviour {

    [System.Serializable]
    public struct WsPoint {
        public float[] co;
    }

    [System.Serializable]
    public struct WsStroke {
        public long timestamp;
        public int index;
        public float[] color;
        public WsPoint[] points;
    }


    public LightningArtist latk;
    public LatkDrawing latkd;
    public enum ProtocolMode { HTTP, HTTPS };
    public ProtocolMode protocolMode = ProtocolMode.HTTP;
    public string serverAddress = "vr.fox-gieg.com";
    public int serverPort = 8080;
    public bool doDebug = true;
    public float scaler = 1f;
    public bool streamToLatk = false;
    public bool armRecordToLatk = false;

    private SocketManager socketManager;
    private string socketAddress;
    private bool connected = false;

    public bool getConnectionStatus() {
        return connected;
    }

	private void Start() {
        if (protocolMode == ProtocolMode.HTTPS) {
            socketAddress = "https://";
        } else {
            socketAddress = "http://";
        }
        socketAddress += serverAddress + ":" + serverPort + "/socket.io/:8443";
        initSocketManager(socketAddress);
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

    private void initSocketManager(string uri) {
        socketManager = new SocketManager(new Uri(uri));
        socketManager.Socket.AutoDecodePayload = false;
        socketManager.Socket.On("error", socketError);
        socketManager.Socket.On("connect", socketConnected);
        socketManager.Socket.On("reconnect", socketConnected);

        socketManager.Socket.On("newFrameFromServer", receivedLocalSocketMessage);
    }

    void socketConnected(Socket socket, Packet packet, params object[] args) {
        connected = true;

        if (doDebug) {
            Debug.Log(DateTime.Now + " Connected to server.");
        }
    }

    void socketError(Socket socket, Packet packet, params object[] args) {
        connected = false;
        if (doDebug) {
            Debug.LogError(DateTime.Now + " Failed to connect to server.");

            if (args.Length > 0) {
                Error error = args[0] as Error;
                if (error != null) {
                    switch (error.Code) {
                        case SocketIOErrors.User:
                            Debug.LogError("Socket Error Type: Exception in an event handler.");
                            break;
                        case SocketIOErrors.Internal:
                            Debug.LogError("Socket Error Type: Internal error.");
                            break;
                        default:
                            Debug.LogError("Socket Error Type: Server error.");
                            break;
                    }
                    Debug.LogError(error.ToString());
                    return;
                }
            }
            Debug.LogError("Could not parse error.");
        }
    }

    void receivedLocalSocketMessage(Socket socket, Packet packet, params object[] args) {
        armRecordToLatk = true;
        string eventName = "data";
        string jsonString;
        if (packet.SocketIOEvent == SocketIOEventTypes.Event) {
            eventName = packet.DecodeEventName();
            jsonString = packet.RemoveEventName(true);
        } else if (packet.SocketIOEvent == SocketIOEventTypes.Ack) {
            jsonString = packet.ToString();
            jsonString = jsonString.Substring(1, jsonString.Length-2);
        } else {
            jsonString = packet.ToString();
        }

        if (doDebug) {
            Debug.Log(DateTime.Now + " - " + "Local Socket Event Name: " + eventName + " - Message: " + jsonString);
        }

        switch (eventName) {
            case "newFrameFromServer":
                JSONNode data = JSON.Parse(jsonString);
                if (doDebug) Debug.Log("Receiving new frame " + data[0]["index"] + " with " + data.Count + " strokes.");

                for (var i = 0; i < data.Count; i++) {
                    List<Vector3> points = getPointsFromJson(data[i]["points"], scaler);
                    latkd.makeCurve(points, latk.killStrokes, latk.strokeLife);
                }

                int index = data[0]["index"].AsInt;
                break;
        }
    }

    public void sendStrokeData(List<Vector3> data) {
		if (!blockSendStroke) StartCoroutine(doSendStrokeData(data));
	}

    private bool blockSendStroke = false;

	private IEnumerator doSendStrokeData(List<Vector3> data) {
        blockSendStroke = true;
		string s = setJsonFromPoints(data);
		socketManager.Socket.Emit("clientStrokeToServer", s);
		Debug.Log(s);
		yield return new WaitForSeconds(latk.frameInterval);
        blockSendStroke = false;
	}

    private void OnApplicationQuit() {
        socketManager.Close();
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
