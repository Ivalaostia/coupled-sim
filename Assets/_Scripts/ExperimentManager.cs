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
    public MyCameraType camType;
    public List<ExperimentSetting> experiments;

    [Header("Inputs")]
    public KeyCode keyUserReady = KeyCode.F1;
    public KeyCode keyTargetDetected = KeyCode.Space;
    public KeyCode setToLastWaytpoint = KeyCode.Escape;
    public KeyCode resetHeadPosition = KeyCode.F2;
    public KeyCode resetExperiment = KeyCode.F3;
    public KeyCode startDriving = KeyCode.Alpha1;

    [Header("Car Controls")]
    public string GasWithKeyboard = "GasKeyBoard";
    public string SteerWithKeyboard = "SteerKeyBoard";
    public string BrakeWithKeyboard = "BrakeKeyBoard";

    public string Gas = "Gas";
    public string Steer = "Steer";
    public string Brake = "Brake";

    [Header("GameObjects")]

    public LayerMask layerToIgnoreForTargetDetection;
    public Transform navigationRoot;
    public Navigator car;

    //UI objects
    public Text UIText;
    public UnityEngine.UI.Image BlackOutScreen;
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
    private Transform LeapVarjoRig;
    private Transform normalCam;
    private Transform headPosition;
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
    private float animationTime = 2f; //time for lighting aniimation in seconds
    private float previousSteeringButtonInput;
    private Controller controller;
    private bool endSimulation = false;

    void Awake()
    {
        BlackOutScreen.color = new Color(0, 0, 0, 1f);
        BlackOutScreen.CrossFadeAlpha(0f, 0f, true);
        //Set gamestate to waiting
        gameState.SetGameState(GameStates.Waiting);

        //If using leap motion controller make a controller object
        if (camType == MyCameraType.Leap) { controller = new Controller(); }

        SetGameObjectsFromCar();
    }
    private void SetGameObjectsFromCar()
    {
        //FindObjectOfType head position
        headPosition = car.transform.Find("Driver View");

        if(headPosition == null) { throw new System.Exception("Could not find head position in the given car..."); }
        //WE have multiple cameras set-up (varjoRig, LeapRig, Normal camera, and three cameras for the mirrors)
        Camera[] cameras = car.GetComponentsInChildren<Camera>(true);

        foreach(Camera camera in cameras)
        {
            if (camera.name == "LeftCamera") { leftMirror = camera; }
            if (camera.name == "RightCamera") { rightMirror = camera; }
            if (camera.name == "MiddleCamera") { rearViewMirror = camera; }

            if (camera.name == "NormalCamera") { normalCam = camera.transform; }
        }

        Varjo.VarjoManager[] varjoRigs = car.GetComponentsInChildren<Varjo.VarjoManager>(true);
        foreach (Varjo.VarjoManager rig in varjoRigs)
        {
            if (rig.transform.parent.name == "Leap Rig") { LeapVarjoRig = rig.transform; }
            else { varjoRig = rig.transform; }
        }
        
        if(leftMirror == null || rightMirror == null || rearViewMirror == null || normalCam == null || LeapVarjoRig == null || varjoRig == null)
        {
            Debug.Log("Couldnt set all cameras....");
        }
}
    private void Start()
    {

        //Set camera 
        SetCamera();
        SetCarControlInput();

        //Set up all experiments
        SetUpExperiments();

        //Set DataManager
        SetDataManager();

        //Set up car
        SetUpCar();

        //Get main camera to waiting room
        GoToWaitingRoom();
    }
    private void FixedUpdate()
    {
        //Start coroutines to dimlights and change the position of the camera to desired locations
        if (gameState.isTransitionToWaitingRoom()) { StartCoroutine(GoToWaitingRoomCoroutine(endSimulation)); }
        else if (gameState.isTransitionToCar()) { StartCoroutine(GoToCarCoroutine()); }

        
    }
    void Update()
    {

        activeExperiment.experimentTime += Time.deltaTime;

        if (gameState.isWaiting())
        {
            if (Input.GetKeyDown(keyUserReady)) { gameState.SetGameState(GameStates.TransitionToCar); }
        }

        //During experiment check for target deteciton key to be pressed
        else if (gameState.isExperiment()) {
            
            SetTargetVisibilityTime();

            //stupid solution for the continues output of the button (this function should obviously only trigger once) so we check if the previous value was already 1 'pushed down'
            if (Input.GetKeyDown(keyTargetDetected) || (Input.GetAxis("SteerButtonRight") != 0 && previousSteeringButtonInput != 1)) { ProcessUserInputTargetDetection(); Debug.Log("Pressed down!"); }
            if (Input.GetKeyDown(setToLastWaytpoint)) { SetCarToLastWaypoint();  }
            if (Input.GetKeyDown(resetHeadPosition)) { SetCameraPosition(headPosition.position, headPosition.rotation); }
            if (Input.GetKeyDown(resetExperiment)) { ResetExperiment(); }
            if (Input.GetKeyDown(startDriving) && automateSpeed) { car.GetComponent<SpeedController>().StartDriving(); }
        }
        previousSteeringButtonInput = Input.GetAxis("SteerButtonRight");


        //if we finished a navigation we go to the waiting room
        if (NavigationFinished() && gameState.isExperiment())
         {
            
            gameState.SetGameState(GameStates.TransitionToWaitingRoom);
            Debug.Log("Navigation finished...");
            if (!IsNextNavigation()){ endSimulation = true; }
           /* else
            {
                gameState.SetGameState(GameStates.Finished);
                StartCoroutine(EndSimulation());
                Debug.Log("Simulation finished");
            }*/
        }
    }
    void ResetExperiment()
    {
        //Does not reset targets....
        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car);
        SetUpCar();
    }
    void SetCarControlInput()
    {
        VehiclePhysics.VPStandardInput carController = car.GetComponent<VehiclePhysics.VPStandardInput>();
        if (camType == MyCameraType.Normal)
        {
            carController.steerAxis = SteerWithKeyboard;
            carController.throttleAndBrakeAxis = GasWithKeyboard;
            car.gameObject.GetComponent<ControlBrakeForce>().brakeInput = BrakeWithKeyboard;
        }
        else
        {
            carController.steerAxis = Steer;
            carController.throttleAndBrakeAxis = Gas;
            car.gameObject.GetComponent<ControlBrakeForce>().brakeInput = Brake;
        }
    }
    void SetCamera()
    {
        if (camType == MyCameraType.Varjo) { usedCam = varjoRig; }
        else if (camType == MyCameraType.Leap) { usedCam = LeapVarjoRig; }
        else if (camType == MyCameraType.Normal) { usedCam = normalCam; }

        varjoRig.gameObject.SetActive(camType == MyCameraType.Varjo);
        LeapVarjoRig.parent.gameObject.SetActive(camType == MyCameraType.Leap);
        normalCam.gameObject.SetActive(camType == MyCameraType.Normal);

        //Destroy unneeded cameras
        if (camType != MyCameraType.Varjo) { Destroy(varjoRig.gameObject); };
        if (camType != MyCameraType.Leap) { Destroy(LeapVarjoRig.parent.gameObject); };
        if (camType != MyCameraType.Normal) { Destroy(normalCam.gameObject); }
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
    IEnumerator GoToWaitingRoomCoroutine(bool endSimulation)
    {
        gameState.SetGameState(GameStates.Waiting);
        BlackOutScreen.CrossFadeAlpha(1f, animationTime, false);
        yield return new WaitForSeconds(animationTime + 0.75f);
        
        //Save the data (doing this will screen is dark as this causes some lag)
        //KEEP BEFORE SETTING UP NEXT EXPERIMENT
        SaveData();

        if (!endSimulation) { SetupNextExperiment(); }
        else { gameState.SetGameState(GameStates.Finished); }
        GoToWaitingRoom();
    }
    IEnumerator GoToCarCoroutine()
    {
        gameState.SetGameState(GameStates.Waiting);
        GoToCar();

        BlackOutScreen.CrossFadeAlpha(1f, 0f, true);
        //For some reason crossfading to zero goes way faster so we increase the animationTime by 4.....
        BlackOutScreen.CrossFadeAlpha(0f, animationTime * 4, false);

        yield return new WaitForSeconds(1f);

        gameState.SetGameState(GameStates.Experiment);
    }
    void SetCarToLastWaypoint()
    {
        //Get previouswwaypoint which is not a splinepoint
        Waypoint previousWaypoint = car.target.previousWaypoint;
        while(previousWaypoint.operation == Operation.SplinePoint) { previousWaypoint = previousWaypoint.previousWaypoint; }

        Vector3 targetPos = previousWaypoint.transform.position;
        Quaternion targetRot = previousWaypoint.transform.rotation;

        StartCoroutine(SetCarSteadyAt(targetPos, targetRot));
    }
    IEnumerator SetCarSteadyAt(Vector3 targetPos, Quaternion targetRot)
    {
        //Somehow car did some back flips when not keeping it steady for some time after repositioning.....
        float step = 0.01f;
        float totalSeconds = 0.2f;
        float count = 0;

        while (count < totalSeconds)
        {
            car.transform.position = targetPos;
            car.transform.rotation = targetRot;

            car.GetComponent<Rigidbody>().velocity = Vector3.zero;
            car.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            count += step;
            yield return new WaitForSeconds(step);
        }
    }
    private void TurnLightsOnFast()
    {
        BlackOutScreen.CrossFadeAlpha(0f, 0f, true);
    }
    void SetUpExperiments()
    {
        //Set all navigation in navigatoin root to inactive
        foreach(Transform child in navigationRoot) { child.gameObject.SetActive(false); }

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
        activeNavigationHelper = activeExperiment.navigationHelper;
        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car);

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
        //Set new navigation for car
        car.SetNewNavigation(activeExperiment.navigation);
        activeNavigationHelper.GetComponent<RenderNavigation>().SetNavigationObjects();
        //Put car in right position
        Transform startLocation = activeNavigationHelper.GetStartPointNavigation();

        StartCoroutine(SetCarSteadyAt(startLocation.position, startLocation.rotation));
    }
    void SetCarSound(bool input)
    {
        //car.GetComponent<AudioSource>().enabled = input;
    }
    IEnumerator RenderStartScreenText()
    {
        Debug.Log("Rendering startscreen...");
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        //If first experiment we render your welcome
        if ((GetIndexCurrentExperiment()) == 0) { UIText.text = $"Eye-calibration incoming when your ready!"; }
        else { 
            UIText.text = $"Experiment {GetIndexCurrentExperiment() } completed..."; 
        }
        UIText.CrossFadeAlpha(1, 2.5f, false);

        yield return new WaitForSeconds(3f);
        
        UIText.CrossFadeAlpha(0, 2.5f, false);
        
        yield return new WaitForSeconds(3f);
        
        if ((GetIndexCurrentExperiment()) != 0 ) {  UIText.text = $"Experiment {GetIndexCurrentExperiment() + 1 } starting when your ready...";}
        
        UIText.CrossFadeAlpha(1, 2.5f, false);
    }
    void GoToCar()
    {
        Debug.Log("Returning to car...");

        //If using varjo we need to do something different with the head position as it is contained in a varjo Rig gameObject which is not the camera position of varjo

        //camPositioner.SetCameraPosition(CameraPosition.Car, camType);
        SetCameraPosition(headPosition.position, headPosition.rotation);       

        usedCam.SetParent(originalParentCamera);

        //Activate mirror cameras (When working with the varjo it deactivates all other cameras....)
        //Does not work when in Start() or in Awake()...
        ActivateMirrorCameras();
        
        //Turns on sound of the car (somehow you still hear this in the waiting room....)
        SetCarSound(true);
    }
    void SetCameraPosition(Vector3 goalPos, Quaternion goalRot)
    {
        if (camType == MyCameraType.Varjo)
        {
            Transform varjoCam = usedCam.GetChild(0);
            Vector3 correctedGoalPos = usedCam.position - varjoCam.position;
            usedCam.position = goalPos + correctedGoalPos;

            usedCam.rotation = goalRot;
        }
        else
        {
            usedCam.transform.position = goalPos;
            usedCam.transform.rotation = goalRot;
        }
    }
    void GoToWaitingRoom()
    {
        TurnLightsOnFast();

        //Turns on sound of the car (somehow you still hear this in the waiting room....)
        SetCarSound(false);

        Debug.Log("Going to waiting room...");
        //camPositioner.SetCameraPosition(CameraPosition.WaitingRoom, camType);
        if (originalParentCamera == null) { originalParentCamera = usedCam.parent; }

        Vector3 goalPos = waitingRoom.transform.position + new Vector3(0,1f,0);
        Quaternion goalRot = waitingRoom.transform.rotation;
        SetCameraPosition(goalPos, goalRot);

        usedCam.SetParent(waitingRoom);

        if (gameState.isFinished()) { StartCoroutine(RenderEndSimulation()); }
        else{ StartCoroutine(RenderStartScreenText()); }
    }
    IEnumerator RenderEndSimulation()
    {
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        yield return new WaitForSeconds(1.0f);
        UIText.text = $"Thanks for participating {subjectName}!";
        UIText.CrossFadeAlpha(1, 2.5f, false);

    }
    bool IsNextNavigation()
    {
        //If this is not the last experiment in the list return true else false
        int index = GetIndexCurrentExperiment();
        if (index+1 == experimentList.Count) { return false; }
        if (experimentList[index+1] == null ) { return false; }
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
        activeNavigationHelper = activeExperiment.navigation.GetComponent<NavigationHelper>();
        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car);

        Debug.Log("Experiment " + activeExperiment.navigation.name + " loaded...");
    }
    void ProcessUserInputTargetDetection()
    {
        //if there is a target visible which has not already been detected
        List<Target> targetList = activeNavigationHelper.GetActiveTargets();
        List<Target> visibleTargets = new List<Target>();
        int targetCount = 0;

        //Check if there are any visible targets
        foreach (Target target in targetList)
        {
            
            if (TargetIsVisible(target, maxNumberOfRandomRayHits))
            {
                visibleTargets.Add(target);
                targetCount++;
                Debug.Log($"{target.waypoint.name}: {target.name} visible...");
            }
        }
        //We do not accept multiple visible targets at the same time.
        if (targetCount == 0)
        {
            dataManager.AddFalseAlarm();
        }
        else if (targetCount == 1)
        {
            dataManager.AddTrueAlarm(visibleTargets[0]) ;
            visibleTargets[0].SetDetected(activeExperiment.experimentTime);

        }
        else
        {
            Target closestTarget = null;
            float distance = 100000f;
            foreach(Target target in visibleTargets)
            {
                float current_distance = Vector3.Distance(car.transform.position, target.transform.position);
                if (current_distance < distance)
                {
                    closestTarget = target;
                    distance = current_distance;
                }
            }
            dataManager.AddTrueAlarm(closestTarget);
            closestTarget.SetDetected(activeExperiment.experimentTime);
            //throw new System.Exception("ERROR: Counting two visible targets, this is not implemented yet...");
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
        float sign = Vector3.Dot(car.transform.forward, (usedCam.transform.position - target.transform.position));
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
        Vector3 direction = target.transform.position - usedCam.transform.position;
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
                currentDirection = (target.transform.position + randomPerpendicularDirection * targetRadius) - usedCam.transform.position;

                if (Physics.Raycast(usedCam.transform.position, currentDirection, out hit, 10000f, ~layerToIgnoreForTargetDetection))
                {
                    Debug.DrawRay(usedCam.transform.position, currentDirection, Color.green);
                    if (hit.collider.gameObject.tag == "Target")
                    {
                        Debug.DrawLine(usedCam.transform.position, usedCam.transform.position + currentDirection * 500, Color.cyan, Time.deltaTime, false);
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
                    Debug.Log($"Target {target.name} became visible at {activeExperiment.experimentTime}s ...");
                    target.startTimeVisible = activeExperiment.experimentTime;
                }
            }
        }
    }
    private void SetDataManager()
    {
        //Get attatched XMLManager
        dataManager = gameObject.GetComponent<XMLManager>(); 
        //Throw error if we dont have an xmlManager
        if(dataManager == null) { throw new System.Exception("Error in Experiment Manager -> A XMLManager should be attatched if you want to save data..."); }

        dataManager.SetAllInputs(car.gameObject, activeExperiment.navigation.transform, subjectName);

        if (saveData) { dataManager.StartNewMeasurement(); }
    }
    private void SetUpDataManagerNewExperiment()
    {
        //Skip if we dont save data
        if (!saveData) { return; }
        dataManager.SetNavigation(activeExperiment.navigation.transform);
        dataManager.StartNewMeasurement();
    }
    private void SaveData()
    {
        if (saveData) { dataManager.SaveData(); }
    }
    private void SetHeadPositionUsingHands()
    {
        
        Frame frame = controller.Frame(); // controller is a Controller object
        if (frame.Hands.Count > 0)
        {
            List<Hand> hands = frame.Hands;

            foreach (Hand hand in hands)
            {
                Debug.Log(hand.PalmPosition.ToString()); // is in milimeters from the leap Rig
            }
        }
    }
    private void ActivateMirrorCameras()
    {
        rearViewMirror.enabled = true; rearViewMirror.cullingMask = -1; // -1 == everything

        rightMirror.enabled = true; rightMirror.cullingMask = -1;

        leftMirror.enabled = true; leftMirror.cullingMask = -1;
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

    private bool active;
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
 