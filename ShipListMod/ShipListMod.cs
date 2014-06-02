using UnityEngine;
using Toolbar;
using System.Collections;
using System.Collections.Generic;

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
	}
	
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ShipListFlight : ShipListMod
	{
		public override string ClassName { get { return this.name; } }
	}

	// actual implementation of the main class.
	public class ShipListMod : MonoBehaviour
	{
		private struct cfg {
			public const string BasePath = "Kiwa";
			public static Vector2 windowSize = new Vector2(640,480);

			public static float nameWidth { get { return 0.35f * windowSize.x; } }
			public static float crewWidth { get { return 0.08f * windowSize.x; } }
			public static float fuelWidth { get { return 0.14f * windowSize.x; } }
		};

		private static IButton toolbarButton = null;

		public virtual string ClassName { get; set; }
		private Rect windowPosition;

		private static bool staticsInitialized = false;
		private static GUIStyle windowStyle = null;
		//private static List<ResourceDef> resourceDefs = null;

		private bool clearButton = false;
		private bool showEmptyButtonState = false;
		private bool allowLoadingButtonState = false;
		private static bool showWindow = false;

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
			createToolbarButton();
			if (!staticsInitialized) {
				initStyles();
				//resourceDefs = ResourceDef.getMyResourceList();
				staticsInitialized = true;
			}
			windowPosition = new Rect(Screen.width-40-cfg.windowSize.x, (Screen.height-cfg.windowSize.y)/2, cfg.windowSize.x, cfg.windowSize.y);
			RenderingManager.AddToPostDrawQueue(0, OnDraw);
			SLStaticData.UpdateGameScene();
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
			if ((!SLStaticData.CurrentSceneIsSafe) && (!showWindow)) {
				tryGetSingleVesselData(FlightGlobals.ActiveVessel, false, true);
			}
		}
		
		private void OnDraw()
		{
			if (showWindow) {
				windowPosition = GUI.Window (1234, windowPosition, OnWindow, "Ship List", windowStyle);
			}
		}

		private Vector2 scrollPosition = Vector2.zero;
		private static GUIStyle scrollAreaStyle = new GUIStyle(HighLogic.Skin.scrollView);
		private void OnWindow(int windowID)
		{
			GUILayout.BeginHorizontal();
			clearButton |= GUILayout.Button("Clear and Force Update", GUILayout.Width(cfg.windowSize.x * 0.25f));
			showEmptyButtonState = GUILayout.Toggle(showEmptyButtonState, "Show Empty/Unknown Vessels");
			GUILayout.EndHorizontal();
			if (SLStaticData.CurrentSceneIsSafe) {
				GUILayout.BeginHorizontal();
				allowLoadingButtonState = GUILayout.Toggle(allowLoadingButtonState, "Load Vessels:");
				SLmayLoad.enableLoadShips     = GUILayout.Toggle(SLmayLoad.enableLoadShips, "Ships");
				SLmayLoad.enableLoadStations  = GUILayout.Toggle(SLmayLoad.enableLoadStations, "Stations");
				SLmayLoad.enableLoadAsteroids = GUILayout.Toggle(SLmayLoad.enableLoadAsteroids, "Asteroids");
				SLmayLoad.enableLoadOthers    = GUILayout.Toggle(SLmayLoad.enableLoadOthers, "Others");
				SLmayLoad.enableLoadLanded    = GUILayout.Toggle(SLmayLoad.enableLoadLanded, "Landed");
				GUILayout.EndHorizontal();
			}

			// title row
			GUILayout.BeginHorizontal();
			GUILayout.Space(10);
			GUILayout.Label("Vessel Name", GUILayout.Width(cfg.nameWidth));
			GUILayout.Label("Crew",        GUILayout.Width(cfg.crewWidth));
			foreach (ResourceDef rd in SLStaticData.resourceDefs) {
				GUILayout.Label(rd.displayName, GUILayout.Width(cfg.fuelWidth));
			}
			GUILayout.Space(10);
			GUILayout.EndHorizontal();

			scrollPosition = GUILayout.BeginScrollView(scrollPosition, scrollAreaStyle);
			foreach (SingleVesselData vd in getVesselData()) {
				GUILayout.BeginHorizontal();
				GUILayout.Label(vd.name, GUILayout.Width(cfg.nameWidth));
				try {
					GUILayout.Label(vd.crewString, GUILayout.Width(cfg.crewWidth));
					if (vd.otherInfo != null) {
						GUILayout.Label(vd.otherInfo, GUILayout.Width(3 * cfg.fuelWidth));
					} else {
						foreach (string fuelinfo in vd.fuelStrings)
						{
							GUILayout.Label(fuelinfo, GUILayout.Width(cfg.fuelWidth));
						}
					}
				} catch {
					Debug.LogError("[ShipListMod] exception during gui-update for \"" + vd.name + "\"");
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			GUI.DragWindow();
		}


		////////////////////////////////
		private SingleVesselData tryGetSingleVesselData(Vessel v, bool allowLoading, bool allowInflightUpdating)
		{
			if ((v.vesselType == VesselType.Flag) || (v.vesselType == VesselType.EVA)) {
				return null;
			}
			try {
				return AllVesselData.getData(v, allowLoading, allowInflightUpdating);
			} catch {
				Debug.LogError("[ShipListMod] getVesselData exception for \"" + v.GetName() + "\"");
				return null;
			}
		}

		private IEnumerable getVesselData()
		{
			bool allowLoading = allowLoadingButtonState;
			bool allowInflightUpdating = true;
			if (SLStaticData.CurrentSceneIsSafe) {
				allowInflightUpdating = false;
			} else {
				allowLoading = false;
			}

			foreach (Vessel v in FlightGlobals.Vessels) {
				SingleVesselData vd = tryGetSingleVesselData(v, allowLoading, allowInflightUpdating);

				if (vd != null) {
					if ((vd.hasData && vd.hasAnyResourcesOrCrew) || showEmptyButtonState)
					{
						yield return vd;
					}
				}
			}
		}

		////////////////////////////////
		private string GetPath(string subPath) {
			// "\" PathSeparator makes Toolbar unhappy
			return System.IO.Path.Combine(cfg.BasePath, subPath).Replace('\\', '/');
		}

		////////////////////////////////
		private void createToolbarButton()
		{
			toolbarButton = ToolbarManager.Instance.add("ShipListMod", "shipList");
			toolbarButton.TexturePath = GetPath("Plugins/ToolbarIcons/shiplisticon");
			toolbarButton.ToolTip = "Ship List Mod";
			toolbarButton.Visibility = new GameScenesVisibility(GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT);
			toolbarButton.Visible = true;
			toolbarButton.Enabled = true;
			toolbarButton.OnClick += (e) => { showWindow = !showWindow; };

			if (! SLStaticData.EverBeenInFlightState) {
				toolbarButton.Enabled = false;
				toolbarButton.ToolTip = "Ship List Mod - disabled until you have been \"in flight\" once.";
			}
		}

		internal void OnDestroy()
		{
			if (toolbarButton != null) {
				toolbarButton.Destroy();
			}
		}

		private void initStyles()
		{
			windowStyle = new GUIStyle (HighLogic.Skin.window);
		}

		public static void Debug_Log(object msg)
		{
#if DEBUG
			Debug.Log(msg);
#endif
		}
	} // class ShipListMod


	////////////////////////////////
	class ResourceDef
	{
		private PartResourceDefinition prd = null;
		public string displayName { get; private set; }
		public int id { get { return (prd != null) ? (prd.id) : (-1); } }
		
		ResourceDef(PartResourceDefinition prd, string shortName)
		{
			this.prd = prd;
			this.displayName = shortName;
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

			if ((!v.loaded) || (v.parts.Count < 1)) {
				return;
			}
			
			Dictionary<int, Resource> tempDict = new Dictionary<int, Resource>();
			foreach (ResourceDef rd in SLStaticData.resourceDefs){
				tempDict.Add(rd.id, new Resource(rd));
			}
			
			foreach (Part p in v.parts){
				foreach (PartResource pr in p.Resources){
					if (tempDict.ContainsKey(pr.info.id))
					{
						tempDict[pr.info.id].amount += pr.amount;
						tempDict[pr.info.id].maxAmount += pr.maxAmount;
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
		public bool hasAnyResourcesOrCrew { get; private set; }
		public string otherInfo { get; private set; }
		public bool needsUpdate { get; set; }
		public bool notYetLoaded { get; set; }
		public bool failedToLoad { get; set; }
		public bool manuallyLoaded { get; set; }

		public string crewString { get; private set; }
		private int crew, maxCrew;

		public string[] fuelStrings { get; private set; }
		private VesselResources vres = null;

		private const float UpdateInterval = 10f;  // seconds
		private float nextUpdateTimestamp = 0f;
		
		public SingleVesselData(Vessel v)
		{
			hasData = false;
			name = v.GetName();
			hasAnyResourcesOrCrew = false;
			otherInfo = null;
			needsUpdate = true;
			notYetLoaded = true;
			failedToLoad = false;
			manuallyLoaded = false;

			ShipListMod.Debug_Log("new SingleVesselResources for " + name);
			vres = new VesselResources(v);
			collectData(v);
		}

		public void Update(Vessel v)
		{
			if ((nextUpdateTimestamp < Time.time)
				|| (needsUpdate && v.loaded))
			{
				if (v.loaded) { ShipListMod.Debug_Log("updating SingleVesselResources for " + name); }
				collectData(v);
			}
		}

		private void collectData(Vessel v)
		{
			nextUpdateTimestamp = Time.time + UpdateInterval + Random.Range(0f,1f);
			if (!v.loaded) {
				if (!hasData) {
					crewString = "";
					otherInfo = "NotLoaded";
					if (v.LandedOrSplashed) { otherInfo += ",Landed"; }
					if (failedToLoad) { otherInfo += ",FailedToLoad"; }
					otherInfo += ",type=" + v.vesselType.ToString();
				}
				return;
			}

			hasData = true;
			name = v.GetName();
			hasAnyResourcesOrCrew = false;
			otherInfo = null;
			needsUpdate = false;
			notYetLoaded = false;

			crewString = "None";
			crew = v.GetCrewCount();
			maxCrew = v.GetCrewCapacity();
			if (maxCrew > 0) {
				crewString = crew + " / " + maxCrew;
				hasAnyResourcesOrCrew = true;
			}

			fuelStrings = new string[SLStaticData.resourceDefs.Count];
			vres.UpdateResources(v);
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

		public static SingleVesselData getData(Vessel v, bool allowLoading, bool allowInflightUpdating)
		{
			if (v == null) { Debug.LogError("[ShipListMod] AllVesselData.getData(null)"); return null; }

			SingleVesselData svd;
			allowLoading &= SLmayLoad.query;

			if (! vessels.ContainsKey(v.id)) {
				vessels.Add(v.id, new SingleVesselData(v));
				svd = vessels[v.id];
				if (!v.loaded) {
					svd.needsUpdate = true;
					svd.notYetLoaded = true;
				}
			} else {
				svd = vessels[v.id];
			}

			bool doTryLoad = svd.notYetLoaded && allowLoading;
			bool doTryUpdate = svd.needsUpdate || allowInflightUpdating;

			// check any overrides which still prevent loading
			if (doTryLoad) {
				bool vesselTypeFlag = false;
				switch(v.vesselType)
				{
				case VesselType.Station:
					vesselTypeFlag = SLmayLoad.enableLoadStations; break;
				case VesselType.Ship:
					vesselTypeFlag = SLmayLoad.enableLoadShips; break;
				case VesselType.SpaceObject:
					vesselTypeFlag = SLmayLoad.enableLoadAsteroids; break;
				case VesselType.Flag:
				case VesselType.EVA:
					vesselTypeFlag = false; break;
				default:
					vesselTypeFlag = SLmayLoad.enableLoadOthers; break;
				}
				if ((v.LandedOrSplashed && !SLmayLoad.enableLoadLanded) || (!vesselTypeFlag))
				{
					// don't touch these unless explicitly requested
					ShipListMod.Debug_Log("[ShipListMod] AllVesselData.getData(): skip loading \"" + svd.name + "\"");
					doTryLoad = false;
					//svd.notYetLoaded = false;
					svd.needsUpdate = false;
				}
			}

			// actually try to load
			if (doTryLoad) {
				try {
					ShipListMod.Debug_Log("[ShipListMod] AllVesselData.getData(): trying to load \"" + svd.name + "\"");
					SLmayLoad.notifyDidLoad();
					svd.notYetLoaded = false;
					svd.manuallyLoaded = true;
					v.Load();
					svd.needsUpdate = true;
				} catch {
					svd.failedToLoad = true;
					doTryUpdate = false;
					Debug.LogWarning("[ShipListMod] AllVesselData.getData(): failed to load \"" + svd.name + "\"");
				}
			}

			if (doTryUpdate) {
				svd.Update(v);

#if false
// that didn't work out too well
				if (svd.manuallyLoaded && svd.hasData && SLStaticData.CurrentSceneIsSafe) {
					ShipListMod.Debug_Log("[ShipListMod] AllVesselData.getData(): unloading \"" + svd.name + "\"");
					v.Unload();
					svd.manuallyLoaded = false;
				}
#endif
			}

			return svd;
		}
	} // class AllVesselData


	////////////////////////////////
	static class SLStaticData
	{
		public static GameScenes CurrentGuiScene = GameScenes.LOADING;

		public static void UpdateGameScene()
		{
			CurrentGuiScene = HighLogic.LoadedScene;
			if (HighLogic.LoadedSceneIsFlight) {
				_everBeenInFlightState = true;
			}
		}

		public static bool CurrentSceneIsSafe { get { return !HighLogic.LoadedSceneIsFlight; } }

		private static bool _everBeenInFlightState = false;
		public static bool EverBeenInFlightState { get { return _everBeenInFlightState; } }

		private static List<ResourceDef> _resourceDefs = null;
		public static List<ResourceDef> resourceDefs {
			get {
				if (_resourceDefs == null) {
					_resourceDefs = ResourceDef.getMyResourceList();
				}
				return _resourceDefs;
			}
		}

		public static void Clear()
		{
			CurrentGuiScene = GameScenes.LOADING;
			_everBeenInFlightState = false;
			_resourceDefs = null;
		}
	} // class SLStaticData
	

	////////////////////////////////
	static class SLmayLoad
	{
		private const float loadInterval = 1f; // seconds
		private static float nextLoadTimestamp = 0f;

		static public void notifyDidLoad()
		{
			nextLoadTimestamp = Time.time + loadInterval;
		}
		static public bool query {
			get	{
				return SLStaticData.EverBeenInFlightState && (nextLoadTimestamp < Time.time);
			}
		}

		public static bool enableLoadStations = false;
		public static bool enableLoadShips = false;
		public static bool enableLoadLanded = false;
		public static bool enableLoadAsteroids = false;
		public static bool enableLoadOthers = false;
	} // class SLmayLoad

} // namespace KSPShipList
