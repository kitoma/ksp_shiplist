/*
 * This file is part of the Ship List Mod for Kerbal Space Program
 * (http://forum.kerbalspaceprogram.com/threads/81821)
 * https://github.com/kitoma/ksp_shiplist
 * License: GPL v2.
 * 
 * It is strongly derived from the corresponding UI code of the Switch
 * Active Vessel Mod (http://forum.kerbalspaceprogram.com/threads/78183),
 * with permission from the author
 * (http://forum.kerbalspaceprogram.com/members/100960-avivey).
 * Original license of that code: BSD 2.
 */

using System.Collections.Generic;
using UnityEngine;

namespace KSPShipList
{
	static class VesselTypeSelectorUI
	{
		private static Dictionary<VesselType, bool> filteredTypes;
		private static List<VesselType> buttonOrder;

		static VesselTypeSelectorUI() {
			filteredTypes =	new Dictionary<VesselType, bool>();
			foreach (VesselType v in System.Enum.GetValues(typeof(VesselType))) {
				filteredTypes[v] = true;
			}
			filteredTypes[VesselType.Flag] = false;
			filteredTypes[VesselType.Debris] = false;
			filteredTypes[VesselType.Unknown] = false;

			buttonOrder = new List<VesselType>();
			buttonOrder.Add(VesselType.Debris);
			buttonOrder.Add(VesselType.Probe);
			buttonOrder.Add(VesselType.Rover);
			buttonOrder.Add(VesselType.Lander);
			buttonOrder.Add(VesselType.Ship);
			buttonOrder.Add(VesselType.Station);
			buttonOrder.Add(VesselType.Base);
			buttonOrder.Add(VesselType.EVA);
			buttonOrder.Add(VesselType.Flag);
			buttonOrder.Add(VesselType.SpaceObject);
			buttonOrder.Add(VesselType.Unknown);
			foreach (VesselType v in System.Enum.GetValues(typeof(VesselType))) {
				if (! buttonOrder.Contains(v)) {
					buttonOrder.Add(v);
				}
			}
		}
		
		public static bool isVesselTypeEnabled(VesselType type) {
			var enabled = false;
			filteredTypes.TryGetValue(type, out enabled);
			return enabled;
		}
		
		public static void DrawFilter() {
			var originalColor = GUI.color;
			//var originalSkin = GUI.skin;

			//GUI.skin = MapView.OrbitIconsTextSkin;

			if (UiUtils.hasIcons) {
				foreach (var type in buttonOrder) {
					var enabled = filteredTypes[type];
					//GUI.color = enabled ? originalColor : Color.black;
					GUI.color = enabled ? originalColor : Color.red;
					if (GUILayout.Button("", GUILayout.Width(24.0f), GUILayout.Height(20.0f))) {
						filteredTypes[type] = ! (filteredTypes[type]);
					}
					var button = GUILayoutUtility.GetLastRect();
					UiUtils.DrawOrbitIcon(button, type);
				}
			} else {
				GUILayout.Label("Filter unavailable, MapIcons not yet loaded");
			}

			//GUI.skin = originalSkin;
			GUI.color = originalColor;
		}
	}
	
	class UiUtils
	{
		static Dictionary<VesselType, Rect> OrbitIconLocation;

		static UiUtils() {
			iconsMap = null;
			OrbitIconLocation = new  Dictionary<VesselType, Rect> ();
			// new Rect(left, top, width, height). Count from bottom left point.
			// List was created in 0.23.5.
			OrbitIconLocation[VesselType.Debris] = new Rect(0.2f, 0.6f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.SpaceObject] = new Rect(0.8f, 0.2f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Unknown] = new Rect(0.6f, 0.6f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Probe] = new Rect(0.2f, 0f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Rover] = new Rect(0f, 0f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Lander] = new Rect(0.6f, 0f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Ship] = new Rect(0f, 0.6f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Station] = new Rect(0.6f, 0.2f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Base] = new Rect(0.4f, 0.0f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.EVA] = new Rect(0.4f, 0.4f, 0.2f, 0.2f);
			//OrbitIconLocation[VesselType.Flag] = new Rect(0.8f, 0.0f, 0.2f, 0.2f);
			OrbitIconLocation[VesselType.Flag] = new Rect(0.86f, 0.07f, 0.15f, 0.15f);
		}

		private static Texture2D iconsMap;

		public static bool hasIcons {
			get {
				if (iconsMap == null) {
					iconsMap = MapView.OrbitIconsMap;
				}
				return (iconsMap != null);
			}
		}

		public static void DrawOrbitIcon(Rect target, VesselType type) {
			if (iconsMap != null) {
				GUI.DrawTextureWithTexCoords(target,
			                                 iconsMap,
			                                 OrbitIconLocation[type]);
			}
		}
	}
}
