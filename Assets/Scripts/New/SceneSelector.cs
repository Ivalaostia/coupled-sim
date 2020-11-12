﻿using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

public class SceneSelector : MonoBehaviour
{
    LevelManager _lvlManager;
    public int sceneSelect;
    public int manualSelection;
    public bool useManualSelection;
    public int manualHostRole;
    public int manualClientRole;
    public bool SendLoadMsgToClient;

    public enum Mapping
    {
        Baseline,
        Mapping1,
        Mapping2
    };

    [SerializeField]
    Mapping _Mapping;

    private void Awake()
    {
        PersistentManager.Instance.nextScene = false;

        if (PersistentManager.Instance.createOrder == true && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8")
        {
            PersistentManager.Instance.ExpOrder = SceneRandomizerBlock(); // SceneRandomizer();
            PersistentManager.Instance.createOrder = false;
            if (useManualSelection == false)
            {
                PersistentManager.Instance.experimentnr = PersistentManager.Instance.ExpOrder[0];
                Debug.LogError($"Experiment nr = {PersistentManager.Instance.experimentnr}");
            }
            else if(useManualSelection == true)
            {
                PersistentManager.Instance.experimentnr = manualSelection;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.LogError($"listnr = {PersistentManager.Instance.listNr}/{PersistentManager.Instance.ExpOrder.Count - 1}; thus if statement = {PersistentManager.Instance.listNr >= PersistentManager.Instance.ExpOrder.Count-1}" );
        Debug.LogError($"sendEndGameToCLient in sceneselector = {PersistentManager.Instance.SendEndGameToClient}");
        if (other.transform.tag == "NEXT")
        {
            if (PersistentManager.Instance.ClientClosed == true && PersistentManager.Instance.listNr >= PersistentManager.Instance.ExpOrder.Count-1)
            {
                //Application.Quit(); // build version
                UnityEditor.EditorApplication.isPlaying = false; // editor version
                Debug.LogError("entered STOP UNITY");
            }
            else if (PersistentManager.Instance.listNr >= PersistentManager.Instance.ExpOrder.Count-1)
            {
                PersistentManager.Instance.SendEndGameToClient = true; // value doesn't change in host. WHY?
                Debug.LogError($"Entered send end game to client, value = {PersistentManager.Instance.SendEndGameToClient}");
            }
            else if (useManualSelection == false)
            {
                PersistentManager.Instance.client_nextScene = 1;

                if(PersistentManager.Instance.SendEndGameToClient == false)
                {
                    Invoke("nextExperiment", 4);
                    Debug.LogError("Go to next scene");
                }
                
                PersistentManager.Instance.stopLogging = true;
                PersistentManager.Instance.nextScene = true;

                // Select next exp
                if (PersistentManager.Instance.listNr < PersistentManager.Instance.ExpOrder.Count - 1)
                {
                    PersistentManager.Instance.listNr++;
                    PersistentManager.Instance.experimentnr = PersistentManager.Instance.ExpOrder[PersistentManager.Instance.listNr];
                    Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");
                }
                /*else if(SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8")
                {
                    SendEndGameToClient = true;
                }*/
            }
            else if (useManualSelection == true)
            {
                //Application.Quit(); // build version
                //UnityEditor.EditorApplication.isPlaying = false; // editor version

                PersistentManager.Instance.stopLogging = true;
                PersistentManager.Instance.nextScene = true;
                PersistentManager.Instance.experimentnr = manualSelection;
            }
        }
    }

    public void nextExperiment()
    {
        //SceneManager.LoadSceneAsync("StartScene");
        Debug.LogError("SWITCHING SCENES");

        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync("StartScene");
        // Don't let the Scene activate until you allow it to
        asyncOperation.allowSceneActivation = false;

        // Send a message to the client to load new scene and allow host to activate scene
        if(asyncOperation.progress >= 0.9f)
        {
            Debug.LogError("Next scene allowed to load");
            asyncOperation.allowSceneActivation = true;
            SendLoadMsgToClient = true;
        }
    }

    public void setSendLoadMsgToCLient()
    {
        SendLoadMsgToClient = false;
    }

    public SceneSelector(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;
        //Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");

        // experiment definition selection
        sceneSelect = PersistentManager.Instance.experimentnr;
        if (sceneSelect > _lvlManager.Experiments.Length)
        {
            Debug.LogError("Selected experiment definition out of bounds.");
            Debug.LogError($"experiment nr = {sceneSelect}");
        }
    }

    public void GazeEffectOnAV()
    {
        if (PersistentManager.Instance.experimentnr < 4 || PersistentManager.Instance.experimentnr > 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4 && PersistentManager.Instance.experimentnr <= 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = true;
        }
        Debug.LogError($"Stop with eye gaze = {PersistentManager.Instance._StopWithEyeGaze}");
    }

    public void VisualizeGaze()
    {
        if (PersistentManager.Instance.experimentnr < 4)
        {
            PersistentManager.Instance._visualizeGaze = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4)
        {
            PersistentManager.Instance._visualizeGaze = true;
        }
    }

    private List<int> SceneRandomizer()
    {
        // Randomize exp 0-3, every exp 3 time
        List<int> Block_one = shuffledList(0, 3, 4);

        // Randomize Mapping 1, exp 4-7
        List<int> Block_two = shuffledList(4, 7, 4);

        // Randomize Mapping 2, exp 8-11
        List<int> Block_three = shuffledList(8, 11, 4);

        // Randomize choosing mapping 1 or 2
        int randomInt = Random.Range(1, 2);

        // Set order of the blocks
        if(randomInt == 1)
        {
            Block_one.AddRange(Block_two);
            Block_one.AddRange(Block_three);
        }
        else if (randomInt == 2)
        {
            Block_one.AddRange(Block_three);
            Block_one.AddRange(Block_two);
        }

        // Prints the list for debugging
        //Debug.LogError($"Randomint = {randomInt}");
        /*for (int i = 0; i < Block_one.Count; i++)
        {
            Debug.LogError($"List one = {Block_one[i]}");
        }*/

        return Block_one;
    }

    private List<int> SceneRandomizerBlock()
    {
        List<int> Block = new List<int>();

        if (_Mapping == Mapping.Baseline)
        {
            // Randomize exp 0-3, every exp 4 times
            Block = shuffledList(0, 3, 4);
        }
        else if (_Mapping == Mapping.Mapping1)
        {
            // Randomize Mapping 1, exp 4-7
            Block = shuffledList(5, 6, 1); //shuffledList(4, 7, 1);
        }
        else if (_Mapping == Mapping.Mapping2)
        {
            // Randomize Mapping 2, exp 8-11
            Block = shuffledList(8, 11, 4);
        }

        // Prints the list for debugging
        /*for (int i = 0; i < Block.Count; i++)
        {
            Debug.LogError($"List = {Block[i]}");
        }*/

        return Block;
    }

    private List<int> shuffledList(int start, int end, int reps)
    {
        List<int> Block =  makeList(start, end, reps);
        Block = Shuffler(Block);
        return Block;
    }

    private List<int> Shuffler(List<int> _Block)
    {
        for (int i = 0; i < _Block.Count; i++)
        {
            int temp = _Block[i];
            int randomIndex = Random.Range(i, _Block.Count);
            _Block[i] = _Block[randomIndex];
            _Block[randomIndex] = temp;
        }
        return _Block;
    }

    private List<int> makeList(int start, int end, int reps)
    {
        List<int> Block = new List<int>();
        for (int i = start; i <= end; i++)
        {
            for (int j = 0; j <= reps-1; j++)
            {
                Block.Add(i);
            }
        }
        return Block;
    }
}
