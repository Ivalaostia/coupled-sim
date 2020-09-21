/******************************************************************************
 * Copyright (C) Leap Motion, Inc. 2011-2018.                                 *
 * Leap Motion proprietary and confidential.                                  *
 *                                                                            *
 * Use subject to the terms of the Leap Motion SDK Agreement available at     *
 * https://developer.leapmotion.com/sdk_agreement, or another agreement       *
 * between Leap Motion and you, your company or other organization.           *
 ******************************************************************************/

using UnityEngine;
using Varjo;

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
#else
using UnityEngine.VR;
#endif

namespace Leap.Unity {

  /// <summary>
  /// Wraps various (but not all) "XR" calls with Unity 5.6-supporting "VR" calls
  /// via #ifdefs.
  /// </summary>
  public static class XRSupportUtil {

    #if UNITY_2019_2_OR_NEWER
    private static System.Collections.Generic.List<XRNodeState> nodeStates =
      new System.Collections.Generic.List<XRNodeState>();
    #endif

    public static bool IsXREnabled() {
      #if UNITY_2017_2_OR_NEWER
      return XRSettings.enabled;
      #else
      return VRSettings.enabled;
      #endif
    }

    public static bool IsXRDevicePresent() {
      #if UNITY_2017_2_OR_NEWER
      return XRDevice.isPresent;
      #else
      return VRDevice.isPresent;
      #endif
    }

    static bool outputPresenceWarning = false;
    public static bool IsUserPresent(bool defaultPresence = true) {
      #if UNITY_2017_2_OR_NEWER
      var userPresence = XRDevice.userPresence;
      if (userPresence == UserPresenceState.Present) {
        return true;
      } else if (!outputPresenceWarning && userPresence == UserPresenceState.Unsupported) {
        Debug.LogWarning("XR UserPresenceState unsupported (XR support is probably disabled).");
        outputPresenceWarning = true;
      }
      #else
      if (!outputPresenceWarning){
        Debug.LogWarning("XR UserPresenceState is only supported in 2017.2 and newer.");
        outputPresenceWarning = true;
      }
      #endif
      return defaultPresence;
    }

    // Modified to work with Varjo Unity plugin
    public static Vector3 GetXRNodeCenterEyeLocalPosition() {
      return VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);
    }

    public static Quaternion GetXRNodeCenterEyeLocalRotation() {
      return VarjoManager.Instance.GetHMDOrientation(VarjoPlugin.PoseType.CENTER);
    }

    public static Vector3 GetXRNodeHeadLocalPosition() {
      return VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);
    }

    public static Quaternion GetXRNodeHeadLocalRotation() {
      return VarjoManager.Instance.GetHMDOrientation(VarjoPlugin.PoseType.CENTER);
    }

    /*
    public static Vector3 GetXRNodeCenterEyeLocalPosition() {
      #if UNITY_2019_2_OR_NEWER
      InputTracking.GetNodeStates(nodeStates);
      Vector3 position;
      foreach(XRNodeState state in nodeStates) {
        if(state.nodeType == XRNode.CenterEye &&
           state.TryGetPosition(out position))
        { return position; }
      }
      return Vector3.zero;
      #elif UNITY_2017_2_OR_NEWER
      return InputTracking.GetLocalPosition(XRNode.CenterEye);
      #else
      return InputTracking.GetLocalPosition(VRNode.CenterEye);
      #endif
    }

    public static Quaternion GetXRNodeCenterEyeLocalRotation() {
      #if UNITY_2019_2_OR_NEWER
      InputTracking.GetNodeStates(nodeStates);
      Quaternion rotation;
      foreach (XRNodeState state in nodeStates) {
        if (state.nodeType == XRNode.CenterEye &&
            state.TryGetRotation(out rotation))
        { return rotation; }
      }
      return Quaternion.identity;
      #elif UNITY_2017_2_OR_NEWER
      return InputTracking.GetLocalRotation(XRNode.CenterEye);
      #else
      return InputTracking.GetLocalRotation(VRNode.CenterEye);
      #endif
    }

    public static Vector3 GetXRNodeHeadLocalPosition() {
      #if UNITY_2019_2_OR_NEWER
      InputTracking.GetNodeStates(nodeStates);
      Vector3 position;
      foreach(XRNodeState state in nodeStates) {
        if(state.nodeType == XRNode.Head &&
           state.TryGetPosition(out position))
        { return position; }
      }
      return Vector3.zero;
      #elif UNITY_2017_2_OR_NEWER
      return InputTracking.GetLocalPosition(XRNode.Head);
      #else
      return InputTracking.GetLocalPosition(VRNode.Head);
      #endif
    }

    public static Quaternion GetXRNodeHeadLocalRotation() {
      #if UNITY_2019_2_OR_NEWER
      InputTracking.GetNodeStates(nodeStates);
      Quaternion rotation;
      foreach (XRNodeState state in nodeStates) {
        if (state.nodeType == XRNode.Head &&
            state.TryGetRotation(out rotation))
        { return rotation; }
      }
      return Quaternion.identity;
      #elif UNITY_2017_2_OR_NEWER
      return InputTracking.GetLocalRotation(XRNode.Head);
      #else
      return InputTracking.GetLocalRotation(VRNode.Head);
      #endif
    }
    */


    public static Vector3 GetXRNodeLocalPosition(int node) {
      #if UNITY_2019_2_OR_NEWER
      InputTracking.GetNodeStates(nodeStates);
      Vector3 position;
      foreach(XRNodeState state in nodeStates) {
        if(state.nodeType == (XRNode)node &&
           state.TryGetPosition(out position))
        { return position; }
      }
      return Vector3.zero;
      #elif UNITY_2017_2_OR_NEWER
      return InputTracking.GetLocalPosition((XRNode)node);
      #else
      return InputTracking.GetLocalPosition((VRNode)node);
      #endif
    }

    public static Quaternion GetXRNodeLocalRotation(int node) {
      #if UNITY_2019_2_OR_NEWER
      InputTracking.GetNodeStates(nodeStates);
      Quaternion rotation;
      foreach (XRNodeState state in nodeStates) {
        if (state.nodeType == (XRNode)node &&
            state.TryGetRotation(out rotation))
        { return rotation; }
      }
      return Quaternion.identity;
      #elif UNITY_2017_2_OR_NEWER
      return InputTracking.GetLocalRotation((XRNode)node);
      #else
      return InputTracking.GetLocalRotation((VRNode)node);
      #endif
    }

    public static void Recenter() {
      InputTracking.Recenter();
    }

    public static string GetLoadedDeviceName() {
      #if UNITY_2017_2_OR_NEWER
      return XRSettings.loadedDeviceName;
      #else
      return VRSettings.loadedDeviceName;
      #endif
    }

    public static bool IsRoomScale() {
      #if UNITY_2017_2_OR_NEWER
      return XRDevice.GetTrackingSpaceType() == TrackingSpaceType.RoomScale;
      #else
      return VRDevice.GetTrackingSpaceType() == TrackingSpaceType.RoomScale;
      #endif
    }

    public static float GetGPUTime() {
      float gpuTime = 0f;
      #if UNITY_5_6_OR_NEWER
      #if UNITY_2017_2_OR_NEWER
      UnityEngine.XR.XRStats.TryGetGPUTimeLastFrame(out gpuTime);
      #else
      UnityEngine.VR.VRStats.TryGetGPUTimeLastFrame(out gpuTime);
      #endif
      #else
      gpuTime = UnityEngine.VR.VRStats.gpuTimeLastFrame;
      #endif
      return gpuTime;
    }

  }

}
