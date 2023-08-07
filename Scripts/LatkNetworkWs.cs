using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using BestHTTP.WebSocket;
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
    
    //public LightningArtist latk;
    public LatkDrawing latkd;
    public enum ProtocolMode { WS, WSS };
    public ProtocolMode protocolMode = ProtocolMode.WS;
    public string serverAddress = "vr.fox-gieg.com";
    public int serverPort = 8080;
    public bool doDebug = true;
    public Vector3 scaler = new Vector3(0.01f, 0.01f, 0.1f);
    public bool killStrokes = true;
    public float strokeLife = 0.1f;
    public int minPoints = 3;
    //public bool streamToLatk = false;
    //public bool armRecordToLatk = false;

    private WebSocket socketMgr;
    private string socketAddress;
    //private bool connected = false;
    private List<int> oldIds = new List<int>();
    private int maxIds = 10;

    //public bool getConnectionStatus() {
        //return connected;
    //}

	private void Start() {
        if (protocolMode == ProtocolMode.WSS) {
            socketAddress = "wss://";
        } else {
            socketAddress = "ws://";
        }
        socketAddress += serverAddress + ":" + serverPort; // + "/socket.io/:8443";

        initSocketManager(socketAddress);
    }

    /*
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
    */

    private void initSocketManager(string url) {
        Debug.Log("Connecting to " + url);

        socketMgr = new WebSocket(new Uri(url));

//#if !UNITY_WEBGL
        //socketMgr.StartPingThread = true;
//#endif

        socketMgr.OnOpen += OnOpen;
        socketMgr.OnMessage += OnMessageReceived;
        socketMgr.OnClosed += OnClosed;
        socketMgr.OnError += OnError;

        socketMgr.Open();
    }

    void OnOpen(WebSocket ws) {
        Debug.Log(string.Format("-WebSocket Open!\n"));
    }

    void OnClosed(WebSocket ws, UInt16 code, string message) {
        Debug.Log(string.Format("-WebSocket closed! Code: {0} Message: {1}\n", code, message));
        socketMgr = null;
    }

    void OnError(WebSocket ws, Exception ex) {
        string errorMsg = string.Empty;
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws.InternalRequest.Response != null) {
            errorMsg = string.Format("Status Code from Server: {0} and Message: {1}", ws.InternalRequest.Response.StatusCode, ws.InternalRequest.Response.Message);
        }
#endif

        Debug.Log(string.Format("-An error occured: {0}\n", (ex != null ? ex.Message : "Unknown Error " + errorMsg)));

        socketMgr = null;
    }

    void OnMessageReceived(WebSocket ws, string message) {
        //if (!message.StartsWith("{") || !message.EndsWith("}")) {
            //Debug.Log("Corrupted message.");
            //return;
        //}

        JSONNode data = JSON.Parse(message);

        Color color = getColorFromJson(data["colors"]);
        List<Vector3> points = getPointsFromJson(data["points"], scaler);
        int index = data["index"].AsInt;

        //latkd.makeCurve(points, latk.killStrokes, latk.strokeLife);
        //latk.inputInstantiateStroke(color, points);

        if (points.Count >= minPoints) {
            StartCoroutine(doInstantiateStroke(index, color, points));
        }
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
            latkd.makeCurve(points, killStrokes, strokeLife);
            //latk.inputInstantiateStroke(color, points);
            oldIds.Add(index);
            if (oldIds.Count > maxIds) oldIds.RemoveAt(0);
        }

        yield return null;
    }

    /*
    public void sendStrokeData(List<Vector3> data) {
		if (!blockSendStroke) StartCoroutine(doSendStrokeData(data));
	}
    */

    private bool blockSendStroke = false;

	/*
    private IEnumerator doSendStrokeData(List<Vector3> data) {
        blockSendStroke = true;
		string s = setJsonFromPoints(data);
        //socketMgr.Send("clientStrokeToServer", s);
        socketMgr.Send(s);
        Debug.Log(s);
		yield return new WaitForSeconds(latk.frameInterval);
        blockSendStroke = false;
    }
    */

    private void OnDestroy() {
        if (socketMgr != null) {
            socketMgr.Close();
        }
    }

    public Color getColorFromJson(JSONNode colorJson) {
        return bytesToColor(Convert.FromBase64String(colorJson));
    }

    public List<Vector3> getPointsFromJson(JSONNode pointsJson, Vector3 scaler) {
        return bytesToVec3s(Convert.FromBase64String(pointsJson), scaler);
    }

    Color bytesToColor(byte[] bytes) {
        MemoryStream stream = new MemoryStream(bytes);
        BinaryReader br = new BinaryReader(stream);
        Vector3 v = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) / 255f;

        return new Color(v.x, v.y, v.z);
    }

    List<Vector3> bytesToVec3s(byte[] bytes, Vector3 scaler) {
        List<Vector3> returns = new List<Vector3>();

        MemoryStream stream = new MemoryStream(bytes);
        BinaryReader br = new BinaryReader(stream);
        int len = (int)(bytes.Length / 12);
        for (int i = 0; i < len; i++) {
            Vector3 v = Vector3.Scale(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), scaler);
            returns.Add(latkd.transform.TransformPoint(v));
        }
        return returns;
    }

    public long getUnixTime() {
		System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
		return (long)(System.DateTime.UtcNow - epochStart).TotalMilliseconds;
	}

    /*
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
    */

}
