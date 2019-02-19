//
//	  UnityOSC - Example of usage for OSC receiver
//
//	  Copyright (c) 2012 Jorge Garcia Martin
//	  Last edit: Gerard Llorach 2nd August 2017
//
// 	  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// 	  documentation files (the "Software"), to deal in the Software without restriction, including without limitation
// 	  the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, 
// 	  and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// 	  The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// 	  of the Software.
//
// 	  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// 	  TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// 	  THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// 	  CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// 	  IN THE SOFTWARE.
//

using UnityEngine;
using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityOSC;

public class ContourOscToLatk : MonoBehaviour
{
	public LightningArtist latk;
	public float scaler = 10f;
	public enum OscMode { SEND, RECEIVE, SEND_RECEIVE };
	public OscMode oscMode = OscMode.RECEIVE;
	public enum MsgMode { P5, OF };
	public MsgMode msgMode = MsgMode.OF;
	public string outIP = "127.0.0.1";
	public int outPort = 9999;
	public int inPort = 9998;
	public int rxBufferSize = 1024000;//1024;

	private OSCServer myServer;
	private int bufferSize = 100; // Buffer size of the application (stores 100 messages from different servers)
	private int sleepMs = 10;

	// Script initialization
	void Start()
	{
		// init OSC
		OSCHandler.Instance.Init();

		// Initialize OSC clients (transmitters)
		if (oscMode == OscMode.SEND || oscMode == OscMode.SEND_RECEIVE) {
			OSCHandler.Instance.CreateClient("myClient", IPAddress.Parse(outIP), outPort);
		}

		if (oscMode == OscMode.RECEIVE || oscMode == OscMode.SEND_RECEIVE) {
			// Initialize OSC servers (listeners)
			myServer = OSCHandler.Instance.CreateServer("myServer", inPort);
			// Set buffer size (bytes) of the server (default 1024)
			myServer.ReceiveBufferSize = rxBufferSize;
			// Set the sleeping time of the thread (default 10)
			myServer.SleepMilliseconds = sleepMs;
		}
	}

	// Reads all the messages received between the previous update and this one
	void Update()
	{
		if (oscMode == OscMode.RECEIVE || oscMode == OscMode.SEND_RECEIVE) {
			// Read received messages
			for (var i = 0; i < OSCHandler.Instance.packets.Count; i++) {
				// Process OSC
				receivedOSC(OSCHandler.Instance.packets[i]);
				// Remove them once they have been read.
				OSCHandler.Instance.packets.Remove(OSCHandler.Instance.packets[i]);
				i--;
			}
		}

		// Send random number to the client
		if (oscMode == OscMode.SEND || oscMode == OscMode.SEND_RECEIVE) {
			float randVal = UnityEngine.Random.Range(0f, 0.7f);
			OSCHandler.Instance.SendMessageToClient("myClient", "/1/fader1", randVal);
		}
	}

	// Process OSC message
	private void receivedOSC(OSCPacket pckt)
	{
		if (pckt == null) {
			Debug.Log("Empty packet");
			return;
		}

		// format: string hostname, int index, byte[] color, byte[] points
		int index = 0;
		Color color = new Color(0f, 0f, 0f);
		List<Vector3> points = new List<Vector3>();

		switch (msgMode) {
			case (MsgMode.P5):
				index = (int)pckt.Data[1];
				color = bytesToColor((byte[])pckt.Data[2]);
				points = bytesToVec3s((byte[])pckt.Data[3]);
				break;
			case (MsgMode.OF):
				OSCMessage msg = pckt.Data[0] as UnityOSC.OSCMessage;
				index = (int)msg.Data[1];
				color = bytesToColor((byte[])msg.Data[2]);
				points = bytesToVec3s((byte[])msg.Data[3]);
				break;
		}

		StartCoroutine(doInstantiateStroke(color, points));

		//latk.target.position = new Vector3(pos.x * scaler, pos.y * scaler, 0f);
		//latk.clicked = pos.z > 0.5f;
		//Debug.Log(pos);

		/*
        // Origin
        int serverPort = pckt.server.ServerPort;

        // Address
        string address = pckt.Address.Substring(1);

        // Data at index 0
        string data0 = pckt.Data.Count != 0 ? pckt.Data[0].ToString() : "null";

        // Print out messages
        Debug.Log("Input port: " + serverPort.ToString() + "\nAddress: " + address + "\nData [0]: " + data0);
		*/
	}

	private IEnumerator doInstantiateStroke(Color color, List<Vector3> points) {
		latk.inputInstantiateStroke(color, points);
		yield return null;
	}

	Color asColor(byte[] bytes) {
		byte[] rBytes = { bytes[0], bytes[1], bytes[2], bytes[3] };
		byte[] gBytes = { bytes[4], bytes[5], bytes[6], bytes[7] };
		byte[] bBytes = { bytes[8], bytes[9], bytes[10], bytes[11] };
		float r = BitConverter.ToSingle(rBytes, 0);
		float g = BitConverter.ToSingle(gBytes, 0);
		float b = BitConverter.ToSingle(bBytes, 0);
		return new Color(r, g, b);
	}

	List<Vector3> asPoints(byte[] bytes) {
		List<Vector3> returns = new List<Vector3>();

		for (int i = 0; i < bytes.Length; i+=12) {
			byte[] xBytes = { bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3] };
			byte[] yBytes = { bytes[i + 4], bytes[i + 5], bytes[i + 6], bytes[i + 7] };
			byte[] zBytes = { bytes[i + 8], bytes[i + 9], bytes[i + 10], bytes[i + 11] };	
			float x = BitConverter.ToSingle(xBytes, 0);
			float y = BitConverter.ToSingle(yBytes, 0);
			float z = BitConverter.ToSingle(zBytes, 0);
			returns.Add(new Vector3(x, y, z));
		}

		return returns;
	}

	byte[] floatsToBytes(float[] floats) {
		MemoryStream stream = new MemoryStream();
		BinaryWriter bw = new BinaryWriter(stream);
		for (int i=0; i<floats.Length; i++) {
			bw.Write(floats[i]);
		}
		bw.Flush();
		byte[] returns = stream.ToArray();
		return returns;
	}

	float[] bytesToFloats(byte[] bytes) {
		MemoryStream stream = new MemoryStream(bytes);
		BinaryReader br = new BinaryReader(stream);
		int len = (int)(bytes.Length/4);
		float[] returns = new float[len];
		for (int i = 0; i < len; i++) {
			returns[i] = br.ReadSingle();
		}
		return returns;
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

}