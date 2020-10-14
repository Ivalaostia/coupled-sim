﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Leap;

[RequireComponent(typeof(XMLManager))]
public class ExperimentManager : MonoBehaviour
{
    //Attachted to the object with the experiment manager should be a XMLManager for handling the saving of the data.
    [Header("Experiment Input")]
    public string subjectName="You";
    public bool automateSpeed = true;
    public bool saveData=true;
    public Color navigationColor;
    public MyCameraType camType;
    public List<ExperimentSetting> experiments;

    [Header("Inputs")]
    public KeyCode myPermission = KeyCode.F1;
    public KeyCode resetHeadPosition = KeyCode.F2;
    public KeyCode spawnSteeringWheel = KeyCode.F3; 
    public KeyCode calibrateGaze = KeyCode.F4;
    public KeyCode resetExperiment = KeyCode.Escape;
    
    public KeyCode keyTargetDetected = KeyCode.Space;
    
    public KeyCode setToLastWaypoint = KeyCode.R;
    public KeyCode inputNameKey = KeyCode.Y;

    public KeyCode saveTheData = KeyCode.F7;
    
    public string ParticpantInputAxisLeft = "SteerButtonLeft";
    public string ParticpantInputAxisRight = "SteerButtonRight";

    [Header("Car Controls")]
    public string GasWithKeyboard = "GasKeyBoard";
    public string SteerWithKeyboard = "SteerKeyBoard";
    public string BrakeWithKeyboard = "BrakeKeyBoard";

    public string Gas = "Gas";
    public string Steer = "Steer";
    public string Brake = "Brake";

    [Header("GameObjects")]
    public Material conformal;
    public HUDMaterials HUDMaterials;
    public LayerMask layerToIgnoreForTargetDetection;
    public Transform navigationRoot;
    public Navigator car;
    public GameObject steeringWheelPrefab;
    private GameObject steeringWheelObject;
    //UI objects
    public Text UIText;
    public Text guiName;
    public UnityEngine.UI.Image blackOutScreen;
    //Scriptable gameState object
    public GameState gameState;
    //Waiting room transform
    public Transform waitingRoom;

    //Mirror cameras from car
    private Camera rearViewMirror;
    private Camera leftMirror;
    private Camera rightMirror;

    //The camera used and head position inside the car

    private Transform varjoRig;
    private Transform leapRig;
    private Transform normalCam;
    [HideInInspector]
    public Transform driverView;
    private Transform usedCam;
    private Transform originalParentCamera;

    // expriment objects and lists
    [HideInInspector]
    public Experiment activeExperiment;
    [HideInInspector]
    public List<Experiment> experimentList;

    private NavigationHelper activeNavigationHelper;
    //The data manger handling the saving of vehicle and target detection data Should be added to the experiment manager object 
    private XMLManager dataManager;
    //Maximum raycasts used in determining visbility:  We use Physics.RayCast to check if we can see the target. We cast this to a random positin on the targets edge to see if it is partly visible.
    private int maxNumberOfRandomRayHits = 40;
    private float animationTime = 3f; //time for lighting aniimation in seconds
    private float lastUserInputTime = 0f ; 
    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)
    private bool endSimulation = false;

    //booleans used by UserInputName() and for processing user input from steer
    private bool inputName = false;  private bool firstFrameProcessingInput = true;
    private float userInputTime = 0f; private float userInputThresholdTime = 0.2f;
    void Awake()
    {
        blackOutScreen.color = new Color(0, 0, 0, 1f);
        blackOutScreen.CrossFadeAlpha(0f, 0f, true);
        //Set gamestate to waiting
        gameState.SetGameState(GameStates.Waiting);
        
        //Get all gameobjects we intend to use from the car (and do some setting up)
        SetGameObjectsFromCar();

        //Set camera (uses the gameobjects set it SetGameObjectsFromCar()) 
        SetCamera();
        
    }
    private void Start()
    {
        //Set rotation of varjo cam to be zero w.r.t. rig
        if (camType == MyCameraType.Varjo || camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
        
        SetCarControlInput();

        //Set up all experiments
        SetUpExperiments();

        //Set DataManager
        SetDataManager();

        //Set up car
        SetUpCar();

        //Get main camera to waiting room
        GoToWaitingRoom();

        //Activate mirror cameras (When working with the varjo it deactivates all other cameras....)
        //Does not work when in Start() or in Awake()...
        ActivateMirrorCameras();
    }

    void Update()
    {
        //Add to the timer of the exprimerent
        if (gameState.isExperiment()) { activeExperiment.experimentTime += Time.deltaTime; }

        //testing gaze data
        if (Input.GetKeyDown(saveTheData)) { SaveData(); dataManager.StartNewMeasurement(); }

        if (gameState.isWaiting()) 
        {
            //Inputs during actual experiment:
            if (Input.GetKeyDown(resetHeadPosition) && camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
            if (Input.GetKeyDown(spawnSteeringWheel)&& camType == MyCameraType.Leap)
            {
                bool success = driverView.GetComponent<CalibrateUsingHands>().SetPositionUsingHands();
                if (success) { SpawnSteeringWheel(); }
            }

            //Inputs when I am doing some TESTING
            if (Input.GetAxis(ParticpantInputAxisRight) == 1 && camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
            if (Input.GetAxis(ParticpantInputAxisLeft) == 1 && camType == MyCameraType.Leap)
            {
                bool success = driverView.GetComponent<CalibrateUsingHands>().SetPositionUsingHands();
                if (success){ SpawnSteeringWheel();}
            }

            //////// always /////////////
            //Start experiment
            if (Input.GetKeyDown(myPermission)) { StartCoroutine(GoToCarCoroutine()); }// gameState.SetGameState(GameStates.TransitionToCar); }

            //Input of subject Name
            if (Input.GetKeyDown(inputNameKey)) { inputName = true; Debug.Log("Pressed inputKeyName..."); }
            InputPlayerName();
        }

        //During experiment check for target deteciton key to be pressed
        else if (gameState.isExperiment()) {
            
            //Looks for targets to appear in field of view and sets their visibility timer accordingly
            SetTargetVisibilityTime();
            
            //Destory the steeringwheel we may generate while calibrating hands on steeringwheel while in the waiting room
            if(steeringWheelObject != null) { Destroy(steeringWheelObject); }

            //User inputs

            //When I am doing some TESTING
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { car.GetComponent<SpeedController>().StartDriving(true); }
            
            //Target detection when we already started driving
            if (car.GetComponent<SpeedController>().IsDriving() && (Input.GetKeyDown(keyTargetDetected) || UserInput())) { ProcessUserInputTargetDetection(); }

            //First input will be the start driving command (so if not already driving we will start driving)
            else if (!car.GetComponent<SpeedController>().IsDriving() && UserInput() && automateSpeed) { car.GetComponent<SpeedController>().StartDriving(true); }

            //Researcher inputs
            if (Input.GetKeyDown(myPermission)) { car.navigationFinished = true; } //Finish navigation early
            if (Input.GetKeyDown(setToLastWaypoint)) { SetCarToLastWaypoint();  }
            if (Input.GetKeyDown(resetHeadPosition))
            {
                if (camType == MyCameraType.Leap) { driverView.GetComponent<CalibrateUsingHands>().SetPositionUsingHands(); }
                SetCameraPosition(driverView.position, driverView.rotation);
            }
            if (Input.GetKeyDown(resetExperiment)) { ResetExperiment(); }
        }

        //Request gaze calibration
        if (Input.GetKeyDown(calibrateGaze)) { GetComponent<VarjoExample.VarjoGazeCalibrationRequest>().RequestGazeCalibration(); }

        //if we finished a navigation we go to the waiting room
        if (NavigationFinished() && gameState.isExperiment())
         {
            StartCoroutine(GoToWaitingRoomCoroutine());
            //gameState.SetGameState(GameStates.TransitionToWaitingRoom);
            Debug.Log("Navigation finished...");
            if (!IsNextNavigation()){ Debug.Log("Simulation finished..."); endSimulation = true; }
        }
    }
    private void SetGameObjectsFromCar()
    {
        //FindObjectOfType head position
        driverView = car.transform.Find("Driver View");
        if (driverView == null) { throw new System.Exception("Could not find head position in the given car..."); }

        //WE have multiple cameras set-up (varjoRig, LeapRig, Normal camera, and three cameras for the mirrors)
        Camera[] cameras = car.GetComponentsInChildren<Camera>(true);

        foreach (Camera camera in cameras)
        {
            if (camera.name == "LeftCamera") { leftMirror = camera; }
            if (camera.name == "RightCamera") { rightMirror = camera; }
            if (camera.name == "MiddleCamera") { rearViewMirror = camera; }

            if (camera.name == "NormalCamera") { normalCam = camera.transform; }
        }

        Varjo.VarjoManager[] varjoRigs = car.GetComponentsInChildren<Varjo.VarjoManager>(true);
        foreach (Varjo.VarjoManager rig in varjoRigs)
        {
            if (rig.transform.parent.name == "Leap Rig") { leapRig = rig.transform.parent; }
            else { varjoRig = rig.transform; }
        }

        if (leftMirror == null || rightMirror == null || rearViewMirror == null || normalCam == null || leapRig == null || varjoRig == null)
        {
            Debug.Log("Couldnt set all cameras....");
        }
    }
    void SpawnSteeringWheel()
    {
        if(camType != MyCameraType.Leap) { return; } //This function is made for when we are using leap motion controller with hand tracking
        if (steeringWheelPrefab == null) { return; }
        if (steeringWheelObject != null) { Destroy(steeringWheelObject); }

        steeringWheelObject = Instantiate(steeringWheelPrefab);

        //Make desired rotation for the steeringwheel
        Vector3 handVector = driverView.GetComponent<CalibrateUsingHands>().GetRightToLeftHand();//steeringWheelObject.transform.Find("LeftHandPosition").transform.position - steeringWheelObject.transform.Find("RightHandPosition").transform.position;
        Quaternion desiredRot = Quaternion.LookRotation(Vector3.Cross(handVector, Vector3.up), Vector3.up);

        //steeringWheelObject.transform.rotation = desiredRot;
        
        steeringWheelObject.transform.position = CameraTransform().position - driverView.GetComponent<CalibrateUsingHands>().GetSteeringWheelToCam();

        Debug.Log($"Spawned steering wheel with steeringWheelToCam {driverView.GetComponent<CalibrateUsingHands>().GetSteeringWheelToCam()}...");

    }
    public Transform CameraTransform()
    {
        if(camType == MyCameraType.Leap) { return usedCam.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if(camType == MyCameraType.Varjo) { return usedCam.Find("VarjoCamera"); }
        else if(camType == MyCameraType.Normal) { return usedCam; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
    void ResetExperiment()
    {
        dataManager.SaveData();
        dataManager.StartNewMeasurement();
        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car, HUDMaterials);
        car.GetComponent<SpeedController>().StartDriving(false);
        SetUpCar();
    }
    public List<string> GetCarControlInput()
    {
        //Used in the XMLManager to save user input
        List<string> output = new List<string>();

        if (camType == MyCameraType.Normal)
        {
            output.Add(SteerWithKeyboard);
            output.Add(GasWithKeyboard);
            output.Add(BrakeWithKeyboard);
        }
        else
        {
            output.Add(Steer);
            output.Add(Gas);
            output.Add(Brake);
        }
        return output;
    }
    void SetCarToLastWaypoint()
    {
        //Get previouswwaypoint which is not a splinepoint
        Waypoint previousWaypoint = car.target.previousWaypoint;
        while (previousWaypoint.operation == Operation.SplinePoint) { previousWaypoint = previousWaypoint.previousWaypoint; }

        Vector3 targetPos = previousWaypoint.transform.position;
        Quaternion targetRot = previousWaypoint.transform.rotation;

        StartCoroutine(SetCarSteadyAt(targetPos, targetRot));
    }
    void SetUpExperiments()
    {
        //Set all navigation in navigatoin root to inactive
        foreach (Transform child in navigationRoot) { child.gameObject.SetActive(false); }

        //If noe xperiments given return...
        if (experiments.Count == 0) { return; }

        //Get experiment settings from the experiment manager and fill our experimentList
        experimentList = new List<Experiment>();
        foreach (ExperimentSetting experimentSetting in experiments)
        {
            if (experimentSetting == null) { continue; }
            Experiment experiment = new Experiment(experimentSetting.navigation, experimentSetting.navigationType, experimentSetting.transparency, false);
            experimentList.Add(experiment);
        }
        
        // Set first experiment to active
        activeExperiment = experimentList[0];
        activeExperiment.SetActive(true);
        
        //SetColors of navigation symbology
        SetColors();

        activeNavigationHelper = activeExperiment.navigationHelper;
        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car, HUDMaterials);

    }
    bool NavigationFinished()
    {
        //Checks wheter current navigation path is finished.
        if (car.navigationFinished) { return true; }

        //if(car.GetCurrentTarget().operation == Operation.EndPoint && car.navigationFinished && car.navigation == activeExperiment.navigation) { return true; }
        
        else { return false; }
    }
    void SetupNextExperiment()
    {
        

        //Activate next experiment (should only get here if we actually have a next experiment)
        ActivateNextExperiment();
        
        //set up car (Should always be after activating the new experiment!)
        SetUpCar();

        //Prep navigation (depends on car being set properly as well !!) 
        //activeNavigationHelper.PrepareNavigationForExperiment();

        //Should always be AFTER next experiment is activated.
        SetUpDataManagerNewExperiment();
    }
    void SetUpCar()
    {
        Debug.Log("Setting up car...");
        //Put car in right position
        Transform startLocation = activeNavigationHelper.GetStartPointNavigation();

        StartCoroutine(SetCarSteadyAt(startLocation.position, startLocation.rotation));
    }
    void GoToCar()
    {
        Debug.Log("Returning to car...");

        //If using varjo we need to do something different with the head position as it is contained in a varjo Rig gameObject which is not the position of the varjo camera
        usedCam.SetParent(originalParentCamera);

        SetCameraPosition(driverView.position, driverView.rotation);       
    }
    void GoToWaitingRoom()
    {
        Debug.Log("Going to waiting room...");
        //camPositioner.SetCameraPosition(CameraPosition.WaitingRoom, camType);
        if (originalParentCamera == null) { originalParentCamera = usedCam.parent; }

        Vector3 goalPos = waitingRoom.transform.position + new Vector3(0,1f,0);
        Quaternion goalRot = waitingRoom.transform.rotation;
        SetCameraPosition(goalPos, goalRot);

        usedCam.SetParent(waitingRoom);

        if (endSimulation) { StartCoroutine(RenderEndSimulation()); }
        else{ StartCoroutine(RenderStartScreenText()); }
    }
    bool IsNextNavigation()
    {
        //If this is not the last experiment in the list return true else false
        int index = GetIndexCurrentExperiment();
        if (index+1 >= experimentList.Count) { return false; }
        else { return true; }
    }
    int GetIndexCurrentExperiment()
    {
        return experimentList.FindIndex(a => a == activeExperiment);
    }
    void ActivateNextExperiment()
    {
        //Deactivate current experiment
        activeExperiment.SetActive(false);

        int currentIndex = GetIndexCurrentExperiment();
        activeExperiment = experimentList[currentIndex + 1];
        if (activeExperiment == null) { throw new System.Exception("Something went wrong in ExperimentManager --> ActivateNextExperiment "); }

        activeExperiment.SetActive(true);
        
        //SetColors of navigation symbology
        SetColors();

        activeNavigationHelper = activeExperiment.navigation.GetComponent<NavigationHelper>();
        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car, HUDMaterials);

        Debug.Log("Experiment " + activeExperiment.navigation.name + " loaded...");
    }
    void ProcessUserInputTargetDetection()
    {
        //Double inputs within thresholdUserInput time are discarded
        if (Time.time < (lastUserInputTime + thresholdUserInput)) { return; }
        lastUserInputTime = Time.time;
        //if there is a target visible which has not already been detected
        List<Target> targetList = activeNavigationHelper.GetActiveTargets();
        List<Target> visibleTargets = new List<Target>();
        int targetCount = 0;

        //Check if there are any visible targets
        foreach (Target target in targetList)
        {
            if (target.HasBeenLookedAt()) { visibleTargets.Add(target); }
            else if (TargetIsVisible(target, maxNumberOfRandomRayHits))
            {
                visibleTargets.Add(target);
                targetCount++;
                Debug.Log($"{target.GetID()} visible...");
            }
        }
        
        if (targetCount == 0) { dataManager.AddFalseAlarm(); }
        else if (targetCount == 1) { dataManager.AddTrueAlarm(visibleTargets[0]); visibleTargets[0].SetDetected(activeExperiment.experimentTime); }
        else
        {
            //When multiple targets are visible we base our decision on:
            //(1) On which target has been looked at most recently
            //(2) Or closest target
            Target targetChosen = null; 
            float mostRecentTime = 0f;
            float smallestDistance = 100000f;
            float currentDistance;
            
            foreach (Target target in visibleTargets)
            {
                //(1)
                if (target.fixationTime > mostRecentTime)
                {
                    targetChosen = target;
                    mostRecentTime = target.fixationTime;
                }
                //(2) Stops this when mostRecentTime variables gets set to something else then 0
                currentDistance = Vector3.Distance(CameraTransform().position, target.transform.position);
                if (currentDistance < smallestDistance && mostRecentTime == 0f)
                {
                    targetChosen = target;
                    smallestDistance = currentDistance;
                }
            }
            if(mostRecentTime == 0f) { Debug.Log("Chose target based on distance..."); }
            else { Debug.Log($"Chose target based on fixation time: {Time.time - mostRecentTime}..."); }

            dataManager.AddTrueAlarm(targetChosen);
            targetChosen.SetDetected(activeExperiment.experimentTime);
        }
    }
    Vector3 GetRandomPerpendicularVector(Vector3 vec)
    {

        vec = Vector3.Normalize(vec);

        float v1 = Random.Range(-1f, 1f);
        float v2 = Random.Range(-1f, 1f);

        float x; float y; float z;

        int caseSwitch = Random.Range(0, 3); //outputs 0,1 or, 2


        if (caseSwitch == 0)
        {
            // v1 = x, v2 = y, v3 = z
            x = v1; y = v2;
            z = -(x * vec.x + y * vec.y) / vec.z;
        }
        else if (caseSwitch == 1)
        {
            // v1 = y, v2 = z, v3 = x
            y = v1; z = v2;
            x = -(y * vec.y + z * vec.z) / vec.x;
        }
        else if (caseSwitch == 2)
        {
            // v1 = z, v2 = x, v3 = y
            z = v1; x = v2;
            y = -(z * vec.z + x * vec.x) / vec.y;
        }
        else
        {
            throw new System.Exception("Something went wrong in TargetManager -> GetRandomPerpendicularVector() ");
        }

        float mag = Mathf.Sqrt(x * x + y * y + z * z);
        Vector3 normal = new Vector3(x / mag, y / mag, z / mag);
        return normal;
    }
    private bool PassedTarget(Target target)
    {
        //Passed target if 
        //(1) passes the plane made by the waypoint and its forward direction. 
        // plane equation is A(x-a) + B(y-b) + C(z-c) = 0 = dot(Normal, planePoint - targetPoint)
        // Where normal vector = <A,B,Z>
        // pos = the cars position (x,y,z,)
        // a point on the plane Q= (a,b,c) i.e., target position

        bool passedTarget;
        float sign = Vector3.Dot(CameraTransform().forward, (CameraTransform().position - target.transform.position));
        float distance = Vector3.Distance(target.transform.position, transform.position);
        if (sign >= 0 ) { passedTarget = true; }
        else { passedTarget = false; }

        return passedTarget;
    }
    bool TargetIsVisible(Target target, int maxNumberOfRayHits)
    {
        //We will cast rays to the outer edges of the sphere (the edges are determined based on how we are looking towards the sphere)
        //I.e., with the perpendicular vector to the looking direction of the sphere

        bool isVisible = false;
        Vector3 direction = target.transform.position - CameraTransform().position;
        Vector3 currentDirection;
        RaycastHit hit;
        float targetRadius = target.GetComponent<SphereCollider>().radius;

        //If in front of camera we do raycast
        if (!PassedTarget(target))
        {
            //Vary the location of the raycast over the edge of the potentially visible target
            for (int i = 0; i < maxNumberOfRayHits; i++)
            {
                Vector3 randomPerpendicularDirection = GetRandomPerpendicularVector(direction);
                currentDirection = (target.transform.position + randomPerpendicularDirection * targetRadius) - CameraTransform().position;

                if (Physics.Raycast(CameraTransform().position, currentDirection, out hit, 10000f, ~layerToIgnoreForTargetDetection))
                {
                    Debug.DrawRay(CameraTransform().position, currentDirection, Color.green);
                    if (hit.collider.gameObject.tag == "Target")
                    {
                        Debug.DrawLine(CameraTransform().position, CameraTransform().position + currentDirection * 500, Color.cyan, Time.deltaTime, false);
                        isVisible = true;
                        break;
                    }
                }
            }
        }
        return isVisible;
    }
    void SetTargetVisibilityTime()
    {
        //Number of ray hits to be used. We user a smaller amount than when the user actually presses the detection button. Since this function is called many times in Update() 
        int numberOfRandomRayHits = 5;
        foreach (Target target in activeNavigationHelper.GetActiveTargets())
        {
            //If we didnt set start v isibility timer yet 
            if (target.startTimeVisible == target.defaultVisibilityTime)
            {
                if (TargetIsVisible(target, numberOfRandomRayHits))
                {
                    Debug.Log($"Target {target.GetID()} became visible at {activeExperiment.experimentTime}s ...");
                    target.startTimeVisible = activeExperiment.experimentTime;
                }
            }
        }
    }
    void SetCameraPosition(Vector3 goalPos, Quaternion goalRot)
    {
        usedCam.position = goalPos;
        usedCam.rotation = goalRot;
    }
    private void SetUpDataManagerNewExperiment()
    {
        //Skip if we dont save data
        if (!saveData) { return; }
        dataManager.SetNavigation(activeExperiment.navigation.transform);
    }
    private void SaveData()
    {
        if (saveData) { dataManager.SaveData(); }
    }
    void InputPlayerName()
    {
        if (inputName == true)
        {
            guiName.enabled = true;
            //ignores the first frame during which playerNameEditable is true
            if (firstFrameProcessingInput)
            {
                firstFrameProcessingInput = false;
                return;
            }
            foreach (char c in Input.inputString)
            {
                if (c == "\b"[0])
                {
                    if (guiName.text.Length != 0)
                    {
                        guiName.text = guiName.text.Substring(0, guiName.text.Length - 1);
                    }
                }
                else
                {
                    if (c == "\n"[0] || c == "\r"[0])
                    {
                        Debug.Log("User entered his name: " + guiName.text);
                        subjectName = guiName.text;
                        inputName = false;
                        guiName.enabled = false;
                    }
                    else
                    {
                        guiName.text += c;
                    }
                }
            }
        }
    }
    private bool UserInput()
    {
        //only sends true once every 0.1 seconds (axis returns 1 for multiple frames when a button is clicked)
        if ((userInputTime + userInputThresholdTime) > Time.time) { return false; }
        if (Input.GetAxis(ParticpantInputAxisLeft) == 1 || Input.GetAxis(ParticpantInputAxisRight) == 1) { userInputTime = Time.time; return true; }
        else { return false; }
    }
    private void ActivateMirrorCameras()
    {
        rearViewMirror.enabled = true; rearViewMirror.cullingMask = -1; // -1 == everything

        rightMirror.enabled = true; rightMirror.cullingMask = -1;

        leftMirror.enabled = true; leftMirror.cullingMask = -1;
    }
    private void SetColors()
    {
        navigationColor.a = activeExperiment.transparency;
        conformal.color = navigationColor;

        foreach (Material material in HUDMaterials.GetMaterials())
        {
            material.color = navigationColor;
        }
    }
    private void SetDataManager()
    {
        //Get attatched XMLManager
        dataManager = gameObject.GetComponent<XMLManager>();
        //Throw error if we dont have an xmlManager
        if (dataManager == null) { throw new System.Exception("Error in Experiment Manager -> A XMLManager should be attatched if you want to save data..."); }

        dataManager.SetAllInputs(car.gameObject, activeExperiment.navigation.transform, subjectName);
    }
    void SetCarControlInput()
    {
        VehiclePhysics.VPStandardInput carController = car.GetComponent<VehiclePhysics.VPStandardInput>();
        if (camType == MyCameraType.Normal)
        {
            carController.steerAxis = SteerWithKeyboard;
            carController.throttleAndBrakeAxis = GasWithKeyboard;
        }
        else
        {
            carController.steerAxis = Steer;
            carController.throttleAndBrakeAxis = Gas;
        }
    }
    void SetCamera()
    {
        //Get the to be used camera, destroy the others, and set this camera to be used for the reflection script of the mirrors
        if (camType == MyCameraType.Varjo) { usedCam = varjoRig; }
        else if (camType == MyCameraType.Leap) { usedCam = leapRig; }
        else if (camType == MyCameraType.Normal) { usedCam = normalCam; }

        varjoRig.gameObject.SetActive(camType == MyCameraType.Varjo);
        leapRig.gameObject.SetActive(camType == MyCameraType.Leap);
        normalCam.gameObject.SetActive(camType == MyCameraType.Normal);

        //Destroy unneeded cameras
        if (camType != MyCameraType.Varjo) { Destroy(varjoRig.gameObject); };
        if (camType != MyCameraType.Leap) { Destroy(leapRig.gameObject); };
        if (camType != MyCameraType.Normal)
        {
            Destroy(normalCam.gameObject);
            blackOutScreen = CameraTransform().Find("Canvas").Find("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
            blackOutScreen.color = new Color(0, 0, 0, 1f);
            blackOutScreen.CrossFadeAlpha(0f, 0f, true);
        }


        RearMirrorsReflection[] reflectionScript = car.GetComponentsInChildren<RearMirrorsReflection>(true);
        if (reflectionScript != null && usedCam != null) { reflectionScript[0].head = CameraTransform(); }
        else { Debug.Log("Could not set head position for mirro reflection script..."); }
    }
    IEnumerator RenderEndSimulation()
    {
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        yield return new WaitForSeconds(1.0f);
        UIText.text = $"Thanks for participating {subjectName}!";
        UIText.CrossFadeAlpha(1, 2.5f, false);

    }
    IEnumerator GoToWaitingRoomCoroutine()
    {
        gameState.SetGameState(GameStates.TransitionToWaitingRoom);

        blackOutScreen.CrossFadeAlpha(1f, animationTime, false);
        yield return new WaitForSeconds(animationTime + 1f);

        //Save the data (doing this while screen is dark as this causes some lag)
        //KEEP BEFORE SETTING UP NEXT EXPERIMENT
        SaveData();

        if (!endSimulation) { SetupNextExperiment(); }

        GoToWaitingRoom();
        
        gameState.SetGameState(GameStates.Waiting);

        blackOutScreen.CrossFadeAlpha(0f, animationTime, false);
    }
    IEnumerator GoToCarCoroutine()
    {
        gameState.SetGameState(GameStates.TransitionToCar);
       

        blackOutScreen.CrossFadeAlpha(1f, animationTime, false);
        
        yield return new WaitForSeconds(animationTime);
        
        GoToCar();
        
        blackOutScreen.CrossFadeAlpha(0f, animationTime, false);
        
        gameState.SetGameState(GameStates.Experiment);
        //Start new measurement
        dataManager.StartNewMeasurement();

        yield return new WaitForSeconds(animationTime );

        

        

        Debug.Log($"Verification cam position: {CameraTransform().position}, {driverView.position}...");
    }
    IEnumerator SetCarSteadyAt(Vector3 targetPos, Quaternion targetRot)
    {
        //Somehow car did some back flips when not keeping it steady for some time after repositioning.....
        float step = 0.01f;
        float totalSeconds = 0.5f;
        float count = 0;

        while (count < totalSeconds)
        {
            car.gameObject.transform.position = targetPos;
            car.gameObject.transform.rotation = targetRot;

            car.GetComponent<Rigidbody>().velocity = Vector3.zero;
            car.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

            count += step;
            yield return new WaitForSeconds(step);
        }
    }
    IEnumerator RenderStartScreenText()
    {
        Debug.Log("Rendering startscreen...");
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        //If first experiment we render your welcome
        if ((GetIndexCurrentExperiment()) == 0) { UIText.text = $"Eye-calibration incoming when your ready!"; }
        else
        {
            UIText.text = $"Experiment {GetIndexCurrentExperiment() } completed...";
        }
        UIText.CrossFadeAlpha(1, 2.5f, false);

        yield return new WaitForSeconds(3f);

        UIText.CrossFadeAlpha(0, 2.5f, false);

        yield return new WaitForSeconds(3f);

        if ((GetIndexCurrentExperiment()) != 0) { UIText.text = $"Experiment {GetIndexCurrentExperiment() + 1 } starting when your ready..."; }

        UIText.CrossFadeAlpha(1, 2.5f, false);
    }
}

[System.Serializable]
public class Experiment
{
    public Transform navigation;
    public NavigationHelper navigationHelper;
    public float experimentTime;
    public NavigationType navigationType;
    public float transparency = 0.3f;

    public bool active;

    public Experiment(Transform _navigation, NavigationType _navigationType, float _transparency, bool _active)
    {
        active = _active;
        navigation = _navigation;
        navigationType = _navigationType;
        transparency = _transparency;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        experimentTime = 0f;
    }
    public void SetActive(bool _active)
    {
        active = _active;
        navigation.gameObject.SetActive(_active);
    }
}
[System.Serializable]
public class ExperimentSetting
{
    public Transform navigation;
    public NavigationType navigationType;
    [Range(0.01f, 1f)]
    public float transparency = 0.3f;   
}
 