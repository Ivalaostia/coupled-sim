﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

//logger class
public class WorldLogger
{
    TrafficLightsSystem _lights;
    PlayerSystem _playerSystem;
    BinaryWriter _fileWriter;
    float _startTime;

    public WorldLogger(PlayerSystem playerSys)
    {
        _playerSystem = playerSys;
    }

    List<PlayerAvatar> _driverBuffer = new List<PlayerAvatar>();

    //writes metadata header in binary log file
    public void BeginLog(string fileName, ExperimentDefinition experiment, TrafficLightsSystem lights, float time)
    {
        _lights = lights;
        if (!Directory.Exists("ExperimentLogs"))
        {
            Directory.CreateDirectory("ExperimentLogs");
        }
        _fileWriter = new BinaryWriter(File.Create("ExperimentLogs/" + fileName + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".binLog"));
        _startTime = time;
        _fileWriter.Write(DateTime.Now.ToBinary());
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        _fileWriter.Write(_driverBuffer.IndexOf(_playerSystem.LocalPlayer));
        _fileWriter.Write(_driverBuffer.Count);
        _fileWriter.Write(_playerSystem.Pedestrians.Count);
        _fileWriter.Write(experiment.PointsOfInterest.Length);
        foreach (var poi in experiment.PointsOfInterest)
        {
            _fileWriter.Write(poi.name);
            _fileWriter.Write(poi.position);
            _fileWriter.Write(poi.rotation);
        }
        if (_lights != null)
        {
            _fileWriter.Write(_lights.CarLights.Length);
            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }
            _fileWriter.Write(_lights.PedestrianLights.Length);
            foreach (var light in _lights.PedestrianLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }
        }
        else
        {
            _fileWriter.Write(0);
            _fileWriter.Write(0);
        }
    }

    string GetHierarchyString(Transform trans)
    {
        List<string> names = new List<string>();
        while (trans != null)
        {
            names.Add(trans.name);
            trans = trans.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    //main logging logic
    //adds a single entry to the logfile
    public void LogFrame(float ping, float time)
    {
        _fileWriter.Write(time - _startTime);
        _fileWriter.Write(ping);

        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        foreach (var driver in _driverBuffer)
        {
            _fileWriter.Write(driver.transform.position);
            _fileWriter.Write(driver.transform.rotation);
            _fileWriter.Write((int)driver._carBlinkers.State);
            if (driver == _playerSystem.LocalPlayer)
            {
                var rb = driver.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                Assert.IsFalse(rb.isKinematic);
                _fileWriter.Write(rb.velocity);
            }
        }
        foreach (var pedestrian in _playerSystem.Pedestrians)
        {
            pedestrian.GetPose().SerializeTo(_fileWriter);
        }
        if (_lights != null)
        {
            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write((byte)light.State);
            }
            foreach (var light in _lights.PedestrianLights)
            {
                _fileWriter.Write((byte)light.State);
            }
        }
    }

    //cleans up after logging has ended
    public void EndLog()
    {
        // TODO(jacek): for now we just call EndLog in NetworkingManager.OnDestroy, so we close the log when the game finishes
        // Remove that call and this null check when we have proper "End experiment" handling
        if (_fileWriter != null)
        {
            _fileWriter.Dispose();
        }
    }
}

//convert binary log into human readable csv
public class LogConverter
{
    public struct SerializedPOI
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;

        public override string ToString()
        {
            var rot = Rotation.eulerAngles;
            return $"{Name};{Position.x};{Position.y};{Position.z};{rot.x};{rot.y};{rot.z}";
        }
    }
    static SerializedPOI[] ParsePOI(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var pois = new SerializedPOI[count];
        for (int i = 0; i < count; i++)
        {
            pois[i].Name = reader.ReadString();
            pois[i].Position = reader.ReadVector3();
            pois[i].Rotation = reader.ReadQuaternion();
        }
        return pois;
    }
    public static SerializedPOI[] GetPOIs(string sourceFile)
    {
        using (var reader = new BinaryReader(File.OpenRead(sourceFile)))
        {
            var unusedTimestamp = reader.ReadInt64();
            var unusedLocalDriver = reader.ReadInt32();
            var unusedDriverCount = reader.ReadInt32();
            var unusedPedestrianCount = reader.ReadInt32();
            return ParsePOI(reader);
        }
    }

    public const string UNITY_WORLD_ROOT = "World Root";

    const int NumFramesInVelocityRunningAverage = 10;

    Vector3[] _driverPositions;
    RunningAverage[] _driverVels;
    bool _firstFrame;
    float _prevTime;
    //translation logic
    //referenceName, referencePos, referenceRot - parameters specifining new origin point, allowing transforming data into new coordinate system
    public void TranslateBinaryLogToCsv(string sourceFile, string dstFile, string[] pedestrianSkeletonNames, string referenceName, Vector3 referencePos, Quaternion referenceRot)
    {
        _driverPositions = null;
        _driverVels = null;
        _firstFrame = true;

        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        const string separator = ";";
        const int columnsPerDriver = 3 /*pos x,y,z*/ + 3 /*rot x,y,z */ + 1 /*blinkers*/ + 3 /* local velocity */ + 3 /* local smooth velocity */ + 3 /* world velocity */ + 3 /* world velocity smooth */;
        const int columnsForLocalDriver = columnsPerDriver + 3 /* rb velocity x,y,z */ + 3 /* rb velocity local x,y,z */;
        const int columnsPerBone = 6;
        int columnsPerPedestrian = pedestrianSkeletonNames.Length * columnsPerBone + columnsPerBone; // + columnsPerBone is for the root transform;
        var toRefRot = Quaternion.Inverse(referenceRot);

        var srcFile = File.OpenRead(sourceFile);
        using (var reader = new BinaryReader(srcFile))
        using (var writer = new StreamWriter(File.Create(dstFile)))
        {
            writer.WriteLine($"Root;{referenceName}");
            var startTime = DateTime.FromBinary(reader.ReadInt64());
            var startString = startTime.ToString("HH:mm:ss") + ":" + startTime.Millisecond.ToString();
            writer.WriteLine($"Start Time;{startString}");
            var localDriver = reader.ReadInt32();
            var numDrivers = reader.ReadInt32();
            var numPedestrians = reader.ReadInt32();

            // POI
            var pois = new List<SerializedPOI>(ParsePOI(reader));
            pois.AddRange(customPois);
            pois.Add(new SerializedPOI()
            {
                Name = UNITY_WORLD_ROOT,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            });
            Vector3 PosToRefPoint(Vector3 p) => toRefRot * (p - referencePos);
            Quaternion RotToRefPoint(Quaternion q) => toRefRot * q;
            for (int i = 0; i < pois.Count; i++)
            {
                var poi = pois[i];
                poi.Position = PosToRefPoint(poi.Position);
                poi.Rotation = RotToRefPoint(poi.Rotation);
                writer.WriteLine(poi);
            }

            int numCarLights = reader.ReadInt32();
            List<string> carLights = new List<string>();
            for (int i = 0; i < numCarLights; i++)
            {
                carLights.Add(reader.ReadString());
            }
            int numPedestrianLights = reader.ReadInt32();
            List<string> pedestrianLights = new List<string>();
            for (int i = 0; i < numPedestrianLights; i++)
            {
                pedestrianLights.Add(reader.ReadString());
            }

            //****************
            // HEADER ROWS
            //****************

            writer.Write("Timestamp;Roundtrip Time to Host;");

            // Drivers header
            for (int i = 0; i < numDrivers; i++)
            {
                if (i == localDriver)
                {
                    writer.Write(string.Join(separator, Enumerable.Repeat($"Driver{i}", columnsForLocalDriver)));
                }
                else
                {
                    writer.Write(string.Join(separator, Enumerable.Repeat($"Driver{i}", columnsPerDriver)));
                }
                if (i < numDrivers - 1)
                {
                    writer.Write(separator);
                }
            }
            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }
            for (int i = 0; i < numPedestrians; i++)
            {
                var pedestrianName = $"Pedestrian{i}";
                writer.Write(string.Join(separator, Enumerable.Repeat(pedestrianName, columnsPerPedestrian)));
            }
            if (numCarLights > 0)
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, carLights));
            }
            if (numPedestrianLights > 0)
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, pedestrianLights));
            }
            writer.Write("\n");

            // No bone names for drivers
            writer.Write(separator); // for the Timestamp column
            writer.Write(separator); // for the Ping column
            if (localDriver == -1)
            {
                writer.Write(string.Join(separator, new string(';', numDrivers * columnsPerDriver)));
            }
            else
            {
                writer.Write(string.Join(separator, new string(';', (numDrivers - 1) * columnsPerDriver + columnsForLocalDriver)));
            }
            var sb = new StringBuilder();
            sb.Append(string.Join(separator, Enumerable.Repeat("Root", columnsPerBone)));
            sb.Append(";");
            for (int i = 0; i < pedestrianSkeletonNames.Length; i++)
            {
                sb.Append(string.Join(separator, Enumerable.Repeat(pedestrianSkeletonNames[i], columnsPerBone)));
                if (i < pedestrianSkeletonNames.Length - 1)
                {
                    sb.Append(separator);
                }
            }
            for (int i = 0; i < numPedestrians; i++)
            {
                writer.Write(sb.ToString());
            }

            writer.Write("\n");

            writer.Write(separator); // for the Timestamp column
            writer.Write(separator); // for the Ping column

            const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z";
            const string localDriverTransformHeader = driverTransformHeader + ";rb_vel_x;rb_vel_y;rb_vel_z;rb_vel_local_x;rb_vel_local_y;rb_vel_local_z";
            List<string> headers = new List<string>();
            for (int i = 0; i < numDrivers; i++)
            {
                if (i == localDriver)
                {
                    headers.Add(localDriverTransformHeader);
                }
                else
                {
                    headers.Add(driverTransformHeader);
                }
            }
            writer.Write(string.Join(separator, headers));
            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }
            const string boneTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z";
            writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians * (pedestrianSkeletonNames.Length + 1))));

            writer.Write("\n");

            //****************
            // ACTUAL DATA
            //****************

            List<string> line = new List<string>();
            while (srcFile.Position < srcFile.Length)
            {
                line.Clear();
                float timeStamp = reader.ReadSingle();
                float roundtrip = reader.ReadSingle();
                line.Add(timeStamp.ToString());
                line.Add(roundtrip.ToString());
                if (_driverPositions == null)
                {
                    _driverPositions = new Vector3[numDrivers];
                    _driverVels = new RunningAverage[numDrivers];
                    for (int i = 0; i < numDrivers; i++)
                    {
                        _driverVels[i] = new RunningAverage(NumFramesInVelocityRunningAverage);
                    }
                }
                for (int i = 0; i < numDrivers; i++)
                {
                    var pos = PosToRefPoint(reader.ReadVector3());
                    var rot = reader.ReadQuaternion();
                    var euler = RotToRefPoint(rot).eulerAngles;
                    var blinkers = reader.ReadInt32();
                    var lastPos = _driverPositions[i];
                    _driverPositions[i] = pos;
                    var dt = timeStamp - _prevTime;
                    var vel = (pos - lastPos) / dt * SpeedConvertion.Mps2Kmph;
                    var inverseRotation = Quaternion.Inverse(rot);
                    var speed = inverseRotation * vel;
                    if (i == localDriver)
                    {
                        var rbVel = reader.ReadVector3() * SpeedConvertion.Mps2Kmph;
                        if (_firstFrame)
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else
                        {
                            _driverVels[i].Add(vel);
                            var velSmooth = _driverVels[i].Get();
                            var localSmooth = rot * velSmooth;
                            var rbVelLocal = inverseRotation * rbVel;
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z};{rbVel.x};{rbVel.y};{rbVel.z};{rbVelLocal.x};{rbVelLocal.y};{rbVelLocal.z}");
                        }
                    }
                    else
                    {
                        if (_firstFrame)
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else
                        {
                            _driverVels[i].Add(vel);
                            var velSmooth = _driverVels[i].Get();
                            var localSmooth = rot * velSmooth;
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z}");
                        }
                    }
                }
                _firstFrame = false;
                for (int i = 0; i < numPedestrians; i++)
                {
                    var pos = reader.ReadListVector3();
                    var rot = reader.ReadListQuaternion();
                    var unused = reader.ReadInt32();
                    for (int j = 0; j < pos.Count; j++)
                    {
                        var p = PosToRefPoint(pos[j]);
                        var r = RotToRefPoint(rot[j]).eulerAngles;
                        line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z}");
                    }
                }
                for (int i = 0; i < numCarLights; i++)
                {
                    line.Add(((LightState)reader.ReadByte()).ToString());
                }
                for (int i = 0; i < numPedestrianLights; i++)
                {
                    line.Add(((LightState)reader.ReadByte()).ToString());
                }
                writer.Write(string.Join(separator, line));
                writer.Write("\n");
                _prevTime = timeStamp;
            }
        }
    }

    public LogConverter(PlayerAvatar pedestrianPrefab)
    {
        var transforms = pedestrianPrefab.SyncTransforms;
        _pedestrianSkeletonNames = new string[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            _pedestrianSkeletonNames[i] = transforms[i].name;
        }
    }

    string[] _pedestrianSkeletonNames;
    bool _open;
    List<string> _fileNames = new List<string>();
    string _selectedFileName = "";
    SerializedPOI[] _pois;

    List<SerializedPOI> customPois = new List<SerializedPOI>() {
        new SerializedPOI()
        {
            Name = "ldist",
            Position = new Vector3(0, 0, 7.75f),
            Rotation = Quaternion.Euler(new Vector3())
        },
                new SerializedPOI()
        {
            Name = "rdist",
            Position = new Vector3(0, 0, 2.75f),
            Rotation = Quaternion.Euler(new Vector3())
        },
        new SerializedPOI()
        {
            Name = "spawn point",
            Position = new Vector3(0, 0.2224625f, 0),
            Rotation = Quaternion.Euler(new Vector3())
        },

    };

    //displays GUI and handles interactions for a single POI
    public void OnGUI_CustomPoiButton(int i, string name)
    {
        SerializedPOI serializedPOI = customPois[i];
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.Label("name:");
        serializedPOI.Name = GUILayout.TextField(serializedPOI.Name, GUILayout.Width(100));
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("position:");
        var tmp = serializedPOI.Position;
        GUILayout.BeginHorizontal();
        OnGUI_FloatField(ref tmp.x); OnGUI_FloatField(ref tmp.y); OnGUI_FloatField(ref tmp.z);
        GUILayout.EndHorizontal();
        serializedPOI.Position = tmp;
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("rotation:");
        tmp = serializedPOI.Rotation.eulerAngles;
        GUILayout.BeginHorizontal();
        OnGUI_FloatField(ref tmp.x); OnGUI_FloatField(ref tmp.y); OnGUI_FloatField(ref tmp.z);
        GUILayout.EndHorizontal();
        serializedPOI.Rotation = Quaternion.Euler(tmp);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Transform with " + serializedPOI.Name))
        {
            var fullName = "ExperimentLogs/" + name;
            var csvName = "ExperimentLogs/" + serializedPOI.Name + "-" + name.Replace("binLog", "csv");
            TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, serializedPOI.Name, serializedPOI.Position, serializedPOI.Rotation);
        }
    }

    //helper function displaying text field accepting float numbers
    private void OnGUI_FloatField(ref float x)
    {
        string tmp;
        float ftmp;
        tmp = GUILayout.TextField(x.ToString(CultureInfo.InvariantCulture.NumberFormat), GUILayout.Width(100));
        if (float.TryParse(tmp, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out ftmp))
        {
            x = ftmp;
        }
    }

    //displays GUI and handles interactions of a log transforming logic
    public void OnGUI()
    {
        if (!_open && GUILayout.Button("Convert log to csv"))
        {
            var files = Directory.GetFiles("ExperimentLogs/");
            _fileNames.Clear();
            foreach (var file in files)
            {
                if (file.EndsWith("binLog"))
                {
                    _fileNames.Add(file.Split('/')[1]);
                }
            }
            _open = true;
        }
        if (_open)
        {
            if (GUILayout.Button("Close"))
            {
                _open = false;
                _selectedFileName = null;
            }
            foreach (var name in _fileNames)
            {
                if (string.IsNullOrEmpty(_selectedFileName))
                {
                    if (GUILayout.Button(name))
                    {
                        _selectedFileName = name;
                        _pois = GetPOIs("ExperimentLogs/" + name);
                    }
                }
                else if (_selectedFileName == name)
                {

                    GUILayout.BeginVertical();
                    GUILayout.Label(_selectedFileName);

                    GUILayout.Label("Custom reference points:");
                    for (int i = 0; i < customPois.Count(); i++)
                    {
                        OnGUI_CustomPoiButton(i, name);
                    }
                    GUILayout.Label("Stored reference Point:");

                    if (GUILayout.Button("Transform with " + UNITY_WORLD_ROOT))
                    {
                        var fullName = "ExperimentLogs/" + name;
                        var csvName = "ExperimentLogs/" + UNITY_WORLD_ROOT + "-" + name.Replace("binLog", "csv");
                        TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, UNITY_WORLD_ROOT, default, Quaternion.identity);
                    }
                    foreach (var poi in _pois)
                    {
                        if (GUILayout.Button("Transform with " + poi.Name))
                        {
                            var fullName = "ExperimentLogs/" + name;
                            var csvName = "ExperimentLogs/" + poi.Name + "-" + name.Replace("binLog", "csv");
                            TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, poi.Name, poi.Position, poi.Rotation);
                        }
                    }
                    GUILayout.EndVertical();
                }
            }
        }
    }
}
