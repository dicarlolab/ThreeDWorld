﻿using UnityEngine;
using System.Collections;
using NetMQ;
using LitJson;
using System.Collections.Generic;

public abstract class AbstractInputModule
{
#region Fields
    protected Avatar _myAvatar = null;

    const double DOUBLE_CONVERSION = 1 << 30;
//    const int VECTOR_ELEMENT_SHIFT = 1 << 10;
//    const int VECTOR_ELEMENT_MASK = (1 << 10)-1;
#endregion

    public AbstractInputModule(Avatar myAvatar)
    {
        _myAvatar = myAvatar;
    }

    // Read controller input and translate it into commands from an agent
    public abstract void SimulateInputFromController(ref JsonData responseMsgData);
    
    // Parse the input sent from the client and use it to update the controls for the next simulation segment
    public abstract void HandleNetInput(JsonData msgJsonData, ref Vector3 targetVel);

    public abstract void OnFixedUpdate();
#region Encoding/Decoding
    protected static void EncodeFloat01(float val, NetMQMessage msg)
    {
        EncodeDouble01((double) val, msg);
    }
    protected static void EncodeDouble01(double val, NetMQMessage msg)
    {
        int ret = (int)System.Math.Round(val * DOUBLE_CONVERSION);
        msg.Append(ret);
    }
    protected static float ReadFloat01(NetMQMessage msg, ref int curIndex)
    {
        return (float)ReadDouble01(msg, ref curIndex);
    }
    protected static double ReadDouble01(NetMQMessage msg, ref int curIndex)
    {
        if (msg.FrameCount <= curIndex)
            return 0.0f;
        return msg[curIndex++].ConvertToInt32() / DOUBLE_CONVERSION;
    }
    protected static void EncodeVector01(Vector3 val, NetMQMessage msg)
    {
        // TODO: Maybe optimize this to pack into a single integer? Probably not really necessary
        EncodeFloat01(val.x, msg);
        EncodeFloat01(val.y, msg);
        EncodeFloat01(val.z, msg);
    }
    protected static Vector3 ReadVector01(NetMQMessage msg, ref int curIndex)
    {
        Vector3 ret = Vector3.zero;
        ret.x = ReadFloat01(msg, ref curIndex);
        ret.y = ReadFloat01(msg, ref curIndex);
        ret.z = ReadFloat01(msg, ref curIndex);
        return ret;
    }
#endregion
};

public class InputModule : AbstractInputModule
{
#region Fields
    Vector3 cacheVel = Vector3.zero;
    Vector3 cacheAngVel = Vector3.zero;
#endregion

    public InputModule(Avatar myAvatar) : base(myAvatar) {}

    public override void SimulateInputFromController(ref JsonData data)
    {
        // Set movement
        Quaternion curRotation = _myAvatar.transform.rotation;
        Vector3 targetVelocity = Vector3.zero;
        targetVelocity.x = Input.GetAxis("Horizontal");
        targetVelocity.y = Input.GetAxis("Vertical");
        targetVelocity.z = Input.GetAxis("VerticalD");
        targetVelocity = curRotation * targetVelocity;
        
        // Read angular velocity
        Vector3 targetRotationVel = Vector3.zero;
        targetRotationVel.x = -Input.GetAxis("Vertical2");
        targetRotationVel.y = Input.GetAxis("Horizontal2");
        targetRotationVel.z = -Input.GetAxis("HorizontalD");

        if (Input.GetKey(KeyCode.Space))
            data["teleport_random"] = new JsonData(true);

//        data["get_obj_data"] = new JsonData(true);
//        data["relationships"] = new JsonData(JsonType.Array);
//        data["relationships"].Add("ALL");

//        // Convert from relative coordinates
//        Quaternion test = Quaternion.identity;
//        test = test * Quaternion.AngleAxis(targetRotationVel.z, curRotation * Vector3.forward);
//        test = test * Quaternion.AngleAxis(targetRotationVel.x, curRotation * Vector3.left);
//        test = test * Quaternion.AngleAxis(targetRotationVel.y, curRotation * Vector3.up);
//        targetRotationVel = test.eulerAngles;

        data["vel"] = targetVelocity.ToJson();
        data["ang_vel"] = targetRotationVel.ToJson();
    }

    // Parse the input sent from the client and use it to update the controls for the next simulation segment
    public override void HandleNetInput(JsonData jsonData, ref Vector3 targetVel)
    {
        // Get movement
        _myAvatar.sendSceneInfo = jsonData["sendSceneInfo"].ReadBool(false);
        cacheVel = _myAvatar.moveSpeed * jsonData["vel"].ReadVector3(Vector3.zero);
        targetVel = cacheVel;
        cacheAngVel = _myAvatar.rotSpeed * jsonData["ang_vel"].ReadVector3(Vector3.zero);
        if (jsonData["teleport_random"].ReadBool(false))
            _myAvatar.TeleportToValidPosition();
        _myAvatar.shouldCollectObjectInfo = jsonData["get_obj_data"].ReadBool(false);
        List<string> relationships = new List<string>();
        if (!jsonData["relationships"].ReadList(ref relationships))
        {
            string testStr = null;
			if (jsonData["relationships"].ReadString(ref testStr) && testStr != null && testStr.ToUpper() == "ALL") {
				relationships.Add(testStr);
			}
        }
		_myAvatar.relationshipsToRetrieve = relationships;
		JsonData actionsList = jsonData["actions"];
		int actionsCount = actionsList.Count;
		for (int i = 0; i < actionsCount; i++) {
			JsonData action = actionsList[0];
			string id = action["id"].ReadString();
			Vector3 vel = action["vel"].ReadVector3();
			Vector3 ang_vel = action["ang_vel"].ReadVector3();
			Rigidbody rb;
			Renderer rend;
			foreach (SemanticObject o in _myAvatar.observedObjs) {
				rend = o.gameObject.GetComponent<Renderer>();
				if (rend && rend.material) {
					string idval = rend.material.GetInt("_idval").ToString();
					if (idval == id) {
						rb = o.gameObject.GetComponent<Rigidbody>();
						rb.velocity = vel;
						rb.angularVelocity = ang_vel;
					}
				}
			}
		}
    }

    public override void OnFixedUpdate()
    {
        Rigidbody myRigidbody = _myAvatar.myRigidbody;
        float rotSpeed = _myAvatar.rotSpeed;

        myRigidbody.velocity = cacheVel;

        Vector3 angChange = cacheAngVel;
        // Clamp drag value for some momentum on gradual stopping
        Vector3 dragChange = -myRigidbody.angularVelocity;
        if (dragChange.magnitude > rotSpeed)
            dragChange = dragChange.normalized * rotSpeed;
        myRigidbody.AddRelativeTorque(angChange, ForceMode.VelocityChange);
        myRigidbody.AddTorque(dragChange, ForceMode.VelocityChange);
    }
}
