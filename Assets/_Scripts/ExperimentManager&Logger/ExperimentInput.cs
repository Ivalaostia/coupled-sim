﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class ExperimentInput : MonoBehaviour
{
  
    public MyCameraType camType = MyCameraType.Leap;

    public GameObject environment;
    public bool debug = true;

    public bool makeVirtualCable = true;

    public string subjectDataFolder;
    public string subjectName = "Dummy";
    public int experimentOrder;

    public bool automateSpeed = true;
    public bool saveData = true;
    public float animationTime = 2.5f;

    public float FoVCamera = 87;
    public bool calibratedUsingHands = false;
    public float driverViewHorizontalDistance = 0f;
    public float driverViewVerticalDistance = 0f;
    public float driverViewSideDistance = 0f;

    [Header("Inputs")]
    public KeyCode myPermission = KeyCode.F1;
    public KeyCode resetHeadPosition = KeyCode.F2;
    public KeyCode spawnSteeringWheel = KeyCode.F3;
    public KeyCode calibrateGaze = KeyCode.F4;
    public KeyCode resetExperiment = KeyCode.Escape;

    public KeyCode toggleDriving = KeyCode.D;

    public KeyCode toggleSymbology = KeyCode.T;

    public KeyCode setToLastWaypoint = KeyCode.Backspace;
    public KeyCode inputNameKey = KeyCode.I;

    public KeyCode saveTheData = KeyCode.F7;

    public string environmentScene = "Environment";
    public string waitingRoomScene = "WaitingScene";
    public string drivingPractiseScene = "DrivingPractiseScene";
    public string targetScene = "Targets";
    public string calibrationScene = "CalibrationScene";
    public string experimentScene1 = "ExperimentScene1";
    public string experimentScene2 = "ExperimentScene2";
    public string experimentScene3 = "ExperimentScene3";
    public string experimentScene4 = "ExperimentScene4";

    [Header("Car Controls")]

    public string ParticpantInputAxisLeft = "SteerButtonLeft";
    public string ParticpantInputAxisRight = "SteerButtonRight";

    public string GasWithKeyboard = "GasKeyBoard";
    public string SteerWithKeyboard = "SteerKeyBoard";
    public string BrakeWithKeyboard = "BrakeKeyBoard";

    public string Gas = "GasKeyBoardas";
    public string Steer = "Steer";
    public string Brake = "BrakeKeyBoard";

    public string[] sceneArray;
    public int sceneIndex = 0;
    public string currentDrivingScene;

    private void Awake()
    {
        Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL);

        Debug.Log("Calling start() of experimentInput...");
        string[] array = {drivingPractiseScene, experimentScene1, experimentScene3, experimentScene4 };
        sceneArray = array;

        ReadCSVSettingsFile();
        Debug.Log($"Subject Name = {subjectName}...");
        //Set input of data log folder:
        subjectDataFolder = string.Join("/", System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop).Replace("\\", "/"), "Data", System.DateTime.Now.ToString("MM-dd_HH-mm") + "_" + subjectName);
        Debug.Log($"Creating {subjectDataFolder}...");
        System.IO.Directory.CreateDirectory(subjectDataFolder);

        if (environment == null) { environment = GameObject.FindGameObjectWithTag("Environment"); }

    }


    public string GetNextScene()
    {
        currentDrivingScene = sceneArray[sceneIndex];
        
        sceneIndex++;

        return currentDrivingScene;
    }

    public bool IsNextScene()
    {
        if(sceneIndex < sceneArray.Length) { return true; }
        else { return false; }
    }

    public int GetExperimentNumber()
    {
        return sceneIndex;
    }
    public void SetCalibrationDistances(float horizontal, float vertical, float side)
    {
        calibratedUsingHands = true;
        driverViewHorizontalDistance = horizontal;
        driverViewVerticalDistance = vertical;
        driverViewSideDistance = side;
    }

    public bool ReadCSVSettingsFile()
    {
        string fileName = "experimentSettings.csv";
        string filePath = Application.dataPath + "/" + fileName;
    
        Debug.Log($"Trying to read {filePath}...");

        try 
        {
            string fileData = System.IO.File.ReadAllText(filePath);
            string[] lines = fileData.Split('\n');

            //Data: [0] = Name, [1] = order
            string[] lineData = lines[0].Trim().Split(';');
            subjectName = lineData[0];
            experimentOrder = int.Parse(lineData[1]);

            return true;
        }
        catch { Debug.Log("Could not read file...."); return false; }
    
    }
}
