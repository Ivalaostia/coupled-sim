﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using System.Linq;
public class EyeTrackingVisuals : MonoBehaviour
{

    public VarjoPlugin.GazeData gazeData;
    //public ExperimentManager experimentManager;
    public GameObject lightObj;
    public GameObject lightObj2;
    public KeyCode switchGazeHighlight = KeyCode.H;
    public bool highlightGaze = true;
    public bool showCenterGaze = false;
    private bool isActive = true;
    private bool gotOwnGaze = true;
    private void Start()
    {
        MainManager mainManager = MyUtils.GetMainManager();
        if (!mainManager.debug) { lightObj.SetActive(false); lightObj2.SetActive(false); enabled = false; }
    }
    void Update()
    {

        if (Input.GetKeyDown(switchGazeHighlight)) { highlightGaze = !highlightGaze; }
        if (highlightGaze)
        {
            //Gets d
            try  { gazeData = MyUtils.GetExperimentManager().GetComponent<MyGazeLogger>().GetLastGaze(); if (gotOwnGaze) { Debug.Log("Got gaze from logger"); gotOwnGaze = false; } }
            catch { gazeData = VarjoPlugin.GetGaze(); if (!gotOwnGaze) { Debug.Log("Got gaze on my own..."); gotOwnGaze = true; } }

            if (gazeData.status == VarjoPlugin.GazeStatus.VALID)
            {
                SetLightActive(true);
                RenderGaze(gazeData);
            }
            else { SetLightActive(false); }

        }
        else
        {
            SetLightActive(false);
        }

        if (showCenterGaze)
        {
            lightObj2.transform.position = VarjoManager.Instance.HeadTransform.position;
            lightObj2.transform.rotation = VarjoManager.Instance.HeadTransform.rotation;
        }
    }

    public void Disable()
    {
       if(lightObj != null) { lightObj.SetActive(false); }
       if (lightObj2 != null) { lightObj2.SetActive(false); }
        enabled = false;
    }
    void SetLightActive(bool input)
    {
        if (input != isActive)
        {
/*            if (input) { Debug.Log("Gaze light activated!"); }
            else { Debug.Log("Gaze light DEactivated!"); }*/
            lightObj.SetActive(input);
            isActive = input;
        }
        
    }
    void RenderGaze(VarjoPlugin.GazeData data)
    {

        //cast ray with gaze direction and gaze starting position:
        Vector3 gazePosition = new Vector3((float)data.gaze.position[0], (float)data.gaze.position[1], (float)data.gaze.position[2]);
        Vector3 start = VarjoManager.Instance.HeadTransform.position + gazePosition;

        //This is realtive to the hmd rotation
        Vector3 gazeDirectionLocal = new Vector3((float)data.gaze.forward[0], (float)data.gaze.forward[1], (float)data.gaze.forward[2]);
        Vector3 gazeDirectionWorld = TransformToWorldAxis(gazeDirectionLocal, gazePosition);

        lightObj.transform.position = start;
        lightObj.transform.rotation = Quaternion.LookRotation(gazeDirectionWorld);
    }

    Vector3 TransformToWorldAxis(Vector3 gaze, Vector3 gazePosition)
    {
        Vector4 gazeH = new Vector4(gaze.x, gaze.y, gaze.z, 1);
        Vector3 xAxis = VarjoManager.Instance.HeadTransform.right;
        Vector3 yAxis = VarjoManager.Instance.HeadTransform.up;
        Vector3 zAxis = VarjoManager.Instance.HeadTransform.forward;

        float x = Vector4.Dot(new Vector4(xAxis.x, yAxis.x, zAxis.x, gazePosition.x), gazeH);
        float y = Vector4.Dot(new Vector4(xAxis.y, yAxis.y, zAxis.y, gazePosition.y), gazeH);
        float z = Vector4.Dot(new Vector4(xAxis.z, yAxis.z, zAxis.z, gazePosition.z), gazeH);


        return new Vector3(x, y, z);
    }
}

