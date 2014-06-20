/*
 * This file is part of the Ship List Mod for Kerbal Space Program
 * (http://forum.kerbalspaceprogram.com/threads/81821)
 * https://github.com/kitoma/ksp_shiplist
 * License: GPL v2.
 */

using UnityEngine;

namespace KSPShipList
{
	public class SOIFilterUI
	{
		GUIContent[] listEntries;
		private GUIStyle listStyle = new GUIStyle();
		private GUIStyle buttonStylePassive = "button";
		private GUIStyle buttonStyleActive = new GUIStyle("button");

		private bool showList = false;
		private int selectedItemIndex = 0;
		private GUIContent buttonText = new GUIContent("SOI");

		public bool isFilterActive { get { return (selectedItemIndex > 0); } }
		public bool filterIsActiveVessel { get { return (selectedItemIndex == 1); } }
		public bool filterIsOtherBody { get { return (selectedItemIndex > 1); } }
		public string filterGetOtherBodyName {
			get {
				if (!filterIsOtherBody) { return null; }
				try {
					return FlightGlobals.Bodies[selectedItemIndex-2].name;
				} catch {
					Debug.LogError("[ShipListMod] exception in filterGetOtherBodyName");
				}
				return null;
			}
		}

		public SOIFilterUI()
		{
			int numBodies = FlightGlobals.Bodies.Count;
			listEntries = new GUIContent[2 + numBodies];
			listEntries[0] = new GUIContent("No SOI filter");
			listEntries[1] = new GUIContent("Active Vessel");
			// add all celestial bodies to the list
			for (int i=0; i<numBodies; i++) {
				try {
					string name = FlightGlobals.Bodies[i].bodyName;
					try {
						var refbody = FlightGlobals.Bodies[i].orbit.referenceBody.orbit.referenceBody;
						while (refbody != null) {
							name = "- " + name;
							refbody = refbody.orbit.referenceBody;
						}
					} catch { /* ignore all null references */ }
					listEntries[2+i] = new GUIContent(name);
				} catch {
					listEntries[2+i] = new GUIContent("Error[" + i + "]");
				}
			}
			
			// Make a GUIStyle that has a solid white hover/onHover background to indicate highlighted items
			listStyle.normal.textColor = Color.white;
			var texWhite = new Texture2D(2, 2);
			var colors = new Color[4];
			for (int i=0; i<4; i++) { colors[i] = Color.white; }
			texWhite.SetPixels(colors);
			texWhite.Apply();
			listStyle.hover.background = texWhite;
			listStyle.onHover.background = texWhite;
			listStyle.padding.left = listStyle.padding.right = listStyle.padding.top = listStyle.padding.bottom = 2;

			// create guistyle for the button when the SOI filter is active
			buttonStyleActive.normal.textColor = Color.green;
		}

		public void Show(Rect where)
		{
			bool done;
			GUIStyle buttonStyle = buttonStylePassive;
			if (isFilterActive) { buttonStyle = buttonStyleActive; }

			done = Popup.List(where, ref showList, ref selectedItemIndex, buttonText, listEntries,
			                  buttonStyle, "box", listStyle);
		}
	}
}

