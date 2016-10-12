/*
 * Ship List Mod for Kerbal Space Program
 * https://github.com/kitoma/ksp_shiplist
 * License: GPL v2.
 * 
 * This mod uses some UI code from the Switch Active Vessel Mod
 * (http://forum.kerbalspaceprogram.com/threads/78183)
 * for the VesselType filter UI, with permission from the author
 * (http://forum.kerbalspaceprogram.com/members/100960-avivey).
 * Original license of that code: BSD 2.
 */

using UnityEngine;
using KSP.UI.Screens;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace KSPShipList
{
	// clean up every time we get to / return to the main menu.
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class ShipListInitMainMenu : MonoBehaviour
	{
		public void Awake()
		{
			ShipListMod.ClearStatics();
		}
	}

	// three places where the mod is active: SpaceCenter, TrackingStation, Inflight. Not in any Editors or elsewhere.
	[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
	public class ShipListSpaceCenter : ShipListMod
	{
		public override string ClassName { get { return this.name; } }
	}

	[KSPAddon(KSPAddon.Startup.TrackingStation, false)]
	public class ShipListTrackingStation : ShipListMod
	{
		public override string ClassName { get { return this.name; } }

		// the TrackingStation subclass is a bit more complicated because it has to register
		// a listener to know the active vessel (for SOI-filtering)

		private void activeShipChanged(MapObject target) {
			activeSOIname = null;
			try {
				if (target != null) {
					activeSOIname = target.vessel.orbit.referenceBody.name;
				}
			} catch {
				// just ignore any NullReferenceExceptions which do occur during scene loading
			}
		}

		public void Start()
		{
			GameEvents.onPlanetariumTargetChanged.Add(activeShipChanged);
		}

		public new void Awake()
		{
			activeSOIname = null;
			base.Awake();
		}

		public new void OnDestroy()
		{
			GameEvents.onPlanetariumTargetChanged.Remove(activeShipChanged);
			activeSOIname = null;
			base.OnDestroy();
		}
	}

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ShipListFlight : ShipListMod
	{
		public override string ClassName { get { return this.name; } }

		public override bool isSOIfiltered(string referenceBodyName)
		{
			if (SOIfilter.filterIsActiveVessel) {
				// update vessel name
				try {
					activeSOIname = FlightGlobals.ActiveVessel.orbit.referenceBody.name;
				} catch {
					// just in case..
				}
			}
			return base.isSOIfiltered(referenceBodyName);
		}
	}

	////////////////////////////////
	// actual implementation of the main class.
	public class ShipListMod : MonoBehaviour
	{
		private struct cfg {
			public const int ShipListWindowID = 1231648609; // Kiwa.hexdump
			public const int ShipListSettingsWindowID = ShipListWindowID + 1;
			public static Vector2 windowSize = new Vector2(640,480);

			public static float nameMinWidth { get { return 200f; } } //0.35f * windowSize.x; } }
			public static GUILayoutOption nameLayout = GUILayout.MinWidth(nameMinWidth);
			public static float crewWidth { get { return 44f; } } //0.08f * windowSize.x; } }
			public static float fuelWidth { get { return 90f; } } //0.14f * windowSize.x; } }
		};

        private static ApplicationLauncherButton applauncherButton = null;

        public virtual string ClassName { get; set; }
		private Rect windowPosition;

		private static bool staticsInitialized = false;
		private static GUIStyle windowStyle = null;
		private static GUIStyle scrollAreaStyle = null;

		private bool clearButton = false;
		private bool showEmptyButtonState = false;
		private static bool showWindow = false;

		public static SOIFilterUI SOIfilter;

		public static void ClearStatics()
		{
			ShipListMod.Debug_Log("[ShipListMod] clearstatics called");
			SLStaticData.Clear();
			//SLStaticData.UpdateGameScene();
			AllVesselData.Clear();
			staticsInitialized = false;
		}

		public void Awake()
		{
			createButton();
			if (!staticsInitialized) {
				initStyles();
				staticsInitialized = true;
			}
			windowPosition = new Rect(Screen.width-40-cfg.windowSize.x, (Screen.height-cfg.windowSize.y)/2, cfg.windowSize.x, cfg.windowSize.y);
			SLStaticData.UpdateGameScene();
			SOIfilter = new SOIFilterUI();
			print ("Loaded ShipListMod (" + ClassName + ").");
		}

		// public void Start() { }

		public void Update()
		{
			SLStaticData.UpdateGameScene();
			if (clearButton) {
				ShipListMod.Debug_Log("[ShipListMod] clearButton activated.");
				AllVesselData.Clear();
				clearButton = false;
			}

			// while in-flight, track the active vessel even if the gui is closed
			if ((HighLogic.LoadedSceneIsFlight) && (!showWindow)) {
				tryGetSingleVesselData(FlightGlobals.ActiveVessel);
			}
		}

		private void OnGUI()
		{
			drawGUI();
		}

		private void drawGUI()
		{
			if (showWindow) {
				// TODO try this:
				// GUI.skin = Highlogic.Skin
				// (but it should already be covered by the windowStyle argument...)
				windowPosition = GUI.Window(cfg.ShipListWindowID, windowPosition, OnWindow, "Ship List", windowStyle);
			}
		}

		private Vector2 scrollPosition = Vector2.zero;
		private Vector2 scrollTitlePosition = Vector2.zero;
		private void OnWindow(int windowID)
		{
			GUILayout.BeginHorizontal();
			clearButton |= GUILayout.Button("Reset/Clear");
			showEmptyButtonState = GUILayout.Toggle(showEmptyButtonState, "Show Empty");
			GUILayout.FlexibleSpace();
			GUILayout.Label("Filters:");
			GUILayout.Label("", GUILayout.Width(60), GUILayout.Height(20));
			var soiSelectorRect = GUILayoutUtility.GetLastRect();
			VesselTypeSelectorUI.DrawFilter();
			GUILayout.EndHorizontal();

			// compute namewidth. TODO : move computation to filter update function
			float nameWidth = cfg.windowSize.x - 64;
			if (SLStaticData.showCrew) { nameWidth -= 3 + cfg.crewWidth; }
			nameWidth -= SLStaticData.resourceDefs.Count * (3 + cfg.fuelWidth);
			//Debug_Log("nameWidth=" + nameWidth + " minNameWidth=" + cfg.nameMinWidth);
			if (nameWidth > cfg.nameMinWidth) {
				cfg.nameLayout = GUILayout.Width(nameWidth);
			} else {
				cfg.nameLayout = GUILayout.MinWidth(cfg.nameMinWidth);
			}

			// title row
			scrollTitlePosition.x = scrollPosition.x;
			GUILayout.BeginScrollView(scrollTitlePosition, scrollAreaStyle, GUILayout.ExpandHeight(false));
			GUILayout.BeginHorizontal();
			GUILayout.Label("Vessel Name", cfg.nameLayout);
			if (SLStaticData.showCrew) {
				GUILayout.Space(3);
				GUILayout.Label("Crew", GUILayout.Width(cfg.crewWidth));
			}
			foreach (ResourceDef rd in SLStaticData.resourceDefs) {
				GUILayout.Space(3);
				GUILayout.Label(rd.displayName, GUILayout.Width(cfg.fuelWidth));
			}
			GUILayout.EndHorizontal();
			GUILayout.EndScrollView();

			// main scroll body
			int vesselCount = 0;
			scrollPosition = GUILayout.BeginScrollView(scrollPosition, scrollAreaStyle);
			foreach (SingleVesselData vd in getVesselData()) {
				if (!VesselTypeSelectorUI.isVesselTypeEnabled(vd.vesselType)) {
					continue;
				}
				if (isSOIfiltered(vd.referenceBodyName)) {
					continue;
				}
				GUILayout.BeginHorizontal();
				try {
					GUILayout.Label(vd.name, cfg.nameLayout);
					vesselCount += 1;
					// TODO : test
					// bool b = GUILayout.Button(vd.name, "label", GUILayout.MinWidth(cfg.nameMinWidth));
					// if (b) { Debug.Log("button for \"" + vd.name + "\""); }
					// end test
					if (SLStaticData.showCrew) {
						GUILayout.Space(3);
						GUILayout.Label(vd.crewString, GUILayout.Width(cfg.crewWidth));
					}
					if (vd.otherInfo != null) {
						GUILayout.Space(3);
						GUILayout.Label(vd.otherInfo, GUILayout.Width(SLStaticData.resourceDefs.Count * cfg.fuelWidth));
					} else {
						foreach (string fuelinfo in vd.fuelStrings)
						{
							GUILayout.Space(3);
							GUILayout.Label(fuelinfo, GUILayout.Width(cfg.fuelWidth));
						}
					}
				} catch {
					Debug.LogError("[ShipListMod] exception during gui-update for \"" + vd.name + "\"");
				}
				GUILayout.EndHorizontal();
			}
			if (vesselCount < 1) {
				GUILayout.BeginHorizontal();
				GUILayout.Label("No data");
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			// do this last to make sure it overlaps/overwrites other content
			SOIfilter.Show(soiSelectorRect);

			GUI.DragWindow();
		}


		////////////////////////////////
		private SingleVesselData tryGetSingleVesselData(Vessel v)
		{
			if (v == null) { return null; }
			try {
				return AllVesselData.getData(v);
			} catch {
				Debug.LogError("[ShipListMod] getVesselData exception for \"" + v.GetName() + "\"");
				return null;
			}
		}

		private IEnumerable getVesselData()
		{
			foreach (Vessel v in FlightGlobals.Vessels) {
				SingleVesselData vd = tryGetSingleVesselData(v);

				if (vd != null) {
					if ((vd.hasData && vd.hasAnyResourcesOrCrew) || showEmptyButtonState)
					{
						yield return vd;
					}
				}
			}
		}

		////////////////////////////////
		public string activeSOIname = null;
		public virtual bool isSOIfiltered(string referenceBodyName)
		{
			if (!SOIfilter.isFilterActive) {
				return false;
			}
			if (SOIfilter.filterIsActiveVessel) {
				return ((activeSOIname != null) && (activeSOIname != referenceBodyName));
			}
			if (SOIfilter.filterIsOtherBody) {
				string otherSOIname = SOIfilter.filterGetOtherBodyName;
				return ((otherSOIname != null) && (otherSOIname != referenceBodyName));
			}
			return false;
		}

		////////////////////////////////
		private void createButton()
		{
            const ApplicationLauncher.AppScenes VisibleInScenes = ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.FLIGHT;
            var texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
            var filename = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ToolbarIcons/shiplisticon.png").Replace('\\', '/');
            texture.LoadImage(File.ReadAllBytes(filename));
            applauncherButton = ApplicationLauncher.Instance.AddModApplication(OnTrue, OnFalse, null, null, null, OnDisable,
                VisibleInScenes, texture);
		}

        public void OnTrue() { showWindow = true; }
        public void OnFalse() { showWindow = false; }
        public void OnDisable() { showWindow = false; }

		public void OnDestroy()
		{
            if (applauncherButton != null) {
                try {
                    ApplicationLauncher.Instance.RemoveModApplication(applauncherButton);
                }
                catch { /* ignore */ }
                applauncherButton = null;
            }
		}

		private void initStyles()
		{
			windowStyle = new GUIStyle (HighLogic.Skin.window);
			scrollAreaStyle = new GUIStyle(HighLogic.Skin.scrollView);
		}

		public static void Debug_Log(object msg)
		{
#if DEBUG
			Debug.Log("[ShipListMod] " + msg);
#endif
		}
	} // class ShipListMod


	////////////////////////////////
	class ResourceDef
	{
		private PartResourceDefinition prd = null;
		public readonly string displayName;
		public int id { get { return (prd != null) ? (prd.id) : (-1); } }
		public readonly string resourceName;

		ResourceDef(PartResourceDefinition prd, string shortName)
		{
			this.prd = prd;
			this.displayName = shortName;
			this.resourceName = prd.name;
		}
		
		public static List<ResourceDef> getMyResourceList()
		{
			List<ResourceDef> retVal = new List<ResourceDef>();
			
			foreach (PartResourceDefinition prd in PartResourceLibrary.Instance.resourceDefinitions) {
				switch (prd.name) {
				case "LiquidFuel":
					retVal.Add(new ResourceDef(prd, "LF"));
					break;
				case "Oxidizer":
					retVal.Add(new ResourceDef(prd, "OX"));
					break;
				case "MonoPropellant":
					retVal.Add(new ResourceDef(prd, "MP"));
					break;
				default:
					// ignore
					break;
				}
			}
			
			return retVal;
		}

		public static Dictionary<string, PartResourceDefinition> getResourceDefIndexByName()
		{
			Dictionary<string, PartResourceDefinition> retVal = new Dictionary<string, PartResourceDefinition>();

			foreach (PartResourceDefinition prd in PartResourceLibrary.Instance.resourceDefinitions) {
				retVal.Add(prd.name, prd);
			}

			return retVal;
		}
	} // class ResourceDef
	

	////////////////////////////////
	class Resource
	{
		public double amount = 0, maxAmount = 0;
		public ResourceDef rdef = null;
		public int id { get { return (rdef != null) ? (rdef.id) : (-1); } }
		
		public Resource(ResourceDef rdef)
		{
			this.rdef = rdef;
		}
		
		public string displayAmount { get { return display (amount); } }
		public string displayMaxAmount { get { return display(maxAmount); } }
		public override string ToString()
		{
			if (maxAmount == 0) { return "None"; }
			return displayAmount + " / " + displayMaxAmount;
		}
		
		private string display(double v)
		{
			if (v < 100) {
				return v.ToString("0.00");
			}
			return v.ToString("0");
		}
	} // class Resource
	

	////////////////////////////////
	class VesselResources : Dictionary<int, Resource>
	{
		public VesselResources(Vessel v) : base(new Dictionary<int, Resource>())
		{
			ShipListMod.Debug_Log("new VesselResources for " + v.GetName());
		}
		
		public void UpdateResources(Vessel v)
		{
			ShipListMod.Debug_Log("updating VesselResources for " + v.GetName());

			Dictionary<int, Resource> tempDict = new Dictionary<int, Resource>();
			foreach (ResourceDef rd in SLStaticData.resourceDefs){
				tempDict.Add(rd.id, new Resource(rd));
			}

			if (v.loaded) {
				// active vessel or otherwise loaded vessel case:
				foreach (Part p in v.parts) {
					foreach (PartResource pr in p.Resources) {
						if (tempDict.ContainsKey(pr.info.id)) {
							tempDict[pr.info.id].amount += pr.amount;
							tempDict[pr.info.id].maxAmount += pr.maxAmount;
						}
					}
				}
			} else {
				// inactive vessel case:
				Dictionary<string, PartResourceDefinition> resourceIndex = SLStaticData.resourceDefsIndex;
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots) {
					foreach (ProtoPartResourceSnapshot r in p.resources) {
						if (!resourceIndex.ContainsKey(r.resourceName)) {
							ShipListMod.Debug_Log("unknown resource \"" + r.resourceName + "\"");
							continue;
						}
						int key = resourceIndex[r.resourceName].id;
						if (tempDict.ContainsKey(key)) {
                            tempDict[key].amount += r.amount;
                            tempDict[key].maxAmount += r.maxAmount;
						}
					}
				}
			}

			this.Clear();
			foreach (KeyValuePair<int,Resource> kv in tempDict)
			{
				if (kv.Value.maxAmount > 0) {
					this.Add(kv.Key, kv.Value);
				}
			}
		}
	} // class VesselResources


	////////////////////////////////
	class SingleVesselData
	{
		public bool hasData { get; private set; }
		public string name { get; private set; }
		public VesselType vesselType { get; private set; }
		public bool hasAnyResourcesOrCrew { get; private set; }
		public string otherInfo { get; private set; }

		public string crewString { get; private set; }
		private int crew, maxCrew;

		public string[] fuelStrings { get; private set; }
		private VesselResources vres = null;

		public string referenceBodyName { get; private set; }

		private const float UpdateInterval = 10f;  // seconds
		private float nextUpdateTimestamp = 0f;
		
		public SingleVesselData(Vessel v)
		{
			hasData = false;
			hasAnyResourcesOrCrew = false;
			if (v == null) {
				name = "??";
				otherInfo = "vessel == null";
			} else {
				name = v.GetName();
				vesselType = v.vesselType;
				otherInfo = null;
				referenceBodyName = null;
			}

			ShipListMod.Debug_Log("new SingleVesselData for " + name);
			vres = new VesselResources(v);
			collectData(v);
		}

		public void Update(Vessel v)
		{
			if (nextUpdateTimestamp < Time.time)
			{
				ShipListMod.Debug_Log("updating SingleVesselData for " + name);
				collectData(v);
			}
		}

		private void collectData(Vessel v)
		{
			nextUpdateTimestamp = Time.time + UpdateInterval + Random.Range(0f,1f);

			hasData = true;
			name = v.GetName();
			vesselType = v.vesselType;
			hasAnyResourcesOrCrew = false;
			otherInfo = null;

			try {
				referenceBodyName = v.orbit.referenceBody.name;
			} catch {
				Debug.LogError("[ShipListMod] exception when collecting referenceBodyName");
			}

			try {
				collectCrewData(v);
			} catch {
				Debug.LogError("[ShipListMod] exception in collectCrewData()");
			}

			fuelStrings = new string[SLStaticData.resourceDefs.Count];
			try {
				vres.UpdateResources(v);
			} catch {
				Debug.LogError("[ShipListMod] exception in UpdateResources()");
			}
			int index = 0;
			foreach (ResourceDef rd in SLStaticData.resourceDefs) {
				if (vres.ContainsKey(rd.id)) {
					fuelStrings[index] = vres[rd.id].ToString();
					hasAnyResourcesOrCrew = true;
				} else {
					fuelStrings[index] = "-";
				}
				++index;
			}

			if (!hasAnyResourcesOrCrew) {
				otherInfo += ":NoCrewOrResourcesFound";
			}
		}

		private void collectCrewData(Vessel v)
		{
			crewString = "None";

			if (v.isEVA) {
				hasAnyResourcesOrCrew = true;
				crewString = "EVA";
			}

			crew = 0;
			maxCrew = 0;
			if (v.loaded) {
				// active vessel or otherwise loaded vessel case:
				crew = v.GetCrewCount();
				maxCrew = v.GetCrewCapacity();
				if (maxCrew > 0) {
					crewString = crew + " / " + maxCrew;
					hasAnyResourcesOrCrew = true;
				}
			} else {
				// inactive vessel case
				crew = v.protoVessel.GetVesselCrew().Count;
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					maxCrew += p.partInfo.partPrefab.CrewCapacity;
				}
			}

			if ((maxCrew > 0) || (crew > 0)) {
				// it should not happen that there is crew without maxCrew, but it would be interesting to know when it happens
				if (crew > maxCrew) { Debug.LogWarning("[ShipListMod] unexpected crew counts: capacity=" + maxCrew + ", actual=" + crew); }
				
				hasAnyResourcesOrCrew = true;
				crewString = crew + " / " + maxCrew;
			}
		}
		
	} // class SingleVesselData
	

	////////////////////////////////
	static class AllVesselData
	{
		private static Dictionary<System.Guid, SingleVesselData> vessels = new Dictionary<System.Guid, SingleVesselData>();

		public static void Clear()
		{
			vessels.Clear();
		}

		public static SingleVesselData getData(Vessel v)
		{
			if (v == null) { Debug.LogError("[ShipListMod] AllVesselData.getData(null)"); return null; }

			SingleVesselData svd;

			if (! vessels.ContainsKey(v.id)) {
				vessels.Add(v.id, new SingleVesselData(v));
				svd = vessels[v.id];
			} else {
				svd = vessels[v.id];
			}

			svd.Update(v);

			return svd;
		}
	} // class AllVesselData


	////////////////////////////////
	static class SLStaticData
	{
		public static GameScenes CurrentGuiScene = GameScenes.LOADING;

		public static void UpdateGameScene()
		{
			if (CurrentGuiScene != HighLogic.LoadedScene)
			{
				CurrentGuiScene = HighLogic.LoadedScene;
				bool tmp = UiUtils.hasIcons;  // check and load icon reference (as required)
			}
		}

		public static bool showCrew = true;

		private static List<ResourceDef> _resourceDefs = null;
		private static Dictionary<string, PartResourceDefinition> _resourceIndex = null;
		private static void initResourceDefs()
		{
			_resourceDefs = ResourceDef.getMyResourceList();
			_resourceIndex = ResourceDef.getResourceDefIndexByName();
		}
		public static List<ResourceDef> resourceDefs {
			get {
				if (_resourceDefs == null) { initResourceDefs(); }
				return _resourceDefs;
			}
		}
		public static Dictionary<string, PartResourceDefinition> resourceDefsIndex {
			get {
				if (_resourceIndex == null) { initResourceDefs(); }
				return _resourceIndex;
			}
		}

		public static void Clear()
		{
			CurrentGuiScene = GameScenes.LOADING;
			_resourceDefs = null;
			_resourceIndex = null;
		}
	} // class SLStaticData

} // namespace KSPShipList
