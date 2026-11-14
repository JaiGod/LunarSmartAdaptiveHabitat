class_name D
## Every table in this file comes from the Project ARES brief or the team's research proposal.

const VERSION = "VR 1.0"
const MISSION_DAYS = 90
const START_MIN = 7.0 * 60.0          # day 1, 07:00

# ---- the six conditions (brief §2) -------------------------------------------------------------------
const CONDS = ["Anxiety", "Depression", "Conflict", "Fatigue", "Autonomy", "Monotony"]
const COND_LONG = ["Anxiety", "Depression", "Interpersonal conflict", "Cognitive fatigue", "Autonomy deprivation", "Sensory monotony"]
const COND_ICON = ["c_anxiety", "c_depression", "c_conflict", "c_fatigue", "c_autonomy", "c_monotony"]
const COND_COL = [Color("#e5484d"), Color("#4f8ef7"), Color("#e0752f"), Color("#f2a541"), Color("#a970ff"), Color("#8f9ab0")]
const T1 = 0.35
const T2 = 0.55
const T3 = 0.75
const TIER_COL = [Color("#5d6a85"), Color("#e6c84a"), Color("#f08a3c"), Color("#e5484d")]
## how long a score must stay above a threshold before LUNA classifies it (duration assessment, brief §5.3)
const PERSIST_MIN = [10.0, 1440.0, 180.0, 120.0, 1440.0, 1440.0]
## monitoring window after an intervention: anxiety 20 min, depression 48–72 h (brief "critical design note")
const WINDOW_MIN = [20.0, 3.0 * 1440.0, 1440.0, 240.0, 2.0 * 1440.0, 3.0 * 1440.0]
const WINDOW_TXT = ["20 min", "72 h", "24 h", "4 h", "48 h", "72 h"]

# ---- sensors (brief §3.1) + D1 matrix -----------------------------------------------------------------
## weights per condition: Anx, Dep, Con, Fat, Aut, Mon
const SENSORS = {
	"hrv": {"name": "Heart rate variability", "short": "HRV", "icon": "s_hrv", "kind": "wearable", "mass": 0.25, "power": 2.0,
		"w": [0.30, 0.10, 0.05, 0.20, 0.0, 0.0],
		"signal": "RMSSD and resting HR from a chest strap, 5-min windows",
		"threshold": "RMSSD 25% below the crew member's own 14-day baseline for 10 min",
		"fails": ["Exercise or EVA raises HR: needs activity context", "Strap loose or battery flat: signal drops out"]},
	"gsr": {"name": "Galvanic skin response", "short": "GSR", "icon": "s_gsr", "kind": "wearable", "mass": 0.20, "power": 1.5,
		"w": [0.20, 0.0, 0.05, 0.0, 0.0, 0.0],
		"signal": "Skin conductance peaks per minute, wrist band",
		"threshold": "More than 3 peaks/min above baseline for 10 min",
		"fails": ["High false-positive rate (heat, exercise)", "Habituates to chronic stress: reads low when stress is long-term"]},
	"sleep": {"name": "Sleep architecture tracker", "short": "Sleep", "icon": "s_sleep", "kind": "wearable", "mass": 0.10, "power": 1.0,
		"w": [0.10, 0.25, 0.0, 0.25, 0.0, 0.0],
		"signal": "Sleep stages, onset latency, wake-ups (ring + bed sensor)",
		"threshold": "Under 6 h or over 4 wake-ups on 2 nights in a row",
		"fails": ["Not worn at night: no data for that night", "Bed sensor confuses restless sleep with waking"]},
	"voice": {"name": "Voice prosody analysis", "short": "Voice", "icon": "s_voice", "kind": "zone", "mass": 2.40, "power": 18.0,
		"w": [0.15, 0.20, 0.30, 0.15, 0.10, 0.10],
		"signal": "Pitch, rate, energy and affect of speech in shared zones (no words stored)",
		"threshold": "Flat affect or tension score 1.5 SD from baseline over 3 conversations",
		"fails": ["Only works when the person speaks in a shared zone", "Privacy: needs consent; off in private pods and hygiene"]},
	"facs": {"name": "Facial action coding", "short": "FACS", "icon": "s_facs", "kind": "zone", "mass": 4.80, "power": 35.0,
		"w": [0.10, 0.20, 0.25, 0.10, 0.0, 0.10],
		"signal": "Action units (smiles, brow tension) from cameras in shared zones",
		"threshold": "Positive-expression rate 40% below baseline for 2 days",
		"fails": ["Fails in low light (under 150 lux): warm dim scenes blind it", "Never in private pods or hygiene: no coverage there"]},
	"motion": {"name": "Movement / proximity", "short": "Motion", "icon": "s_motion", "kind": "zone", "mass": 1.20, "power": 6.0,
		"w": [0.05, 0.15, 0.30, 0.05, 0.40, 0.20],
		"signal": "UWB tags: time in shared zones, who is near whom, self-started activities",
		"threshold": "Shared-zone time 50% below baseline, or a pair avoiding each other for 24 h",
		"fails": ["Coarse: cannot tell a nap from withdrawal", "Tag left in the pod: shows the person as hiding"]},
	"cortisol": {"name": "Cortisol wearable", "short": "Cortisol", "icon": "s_cortisol", "kind": "wearable", "mass": 1.50, "power": 2.0,
		"w": [0.10, 0.20, 0.0, 0.0, 0.10, 0.0],
		"signal": "Sweat cortisol from a skin patch, compared with the time of day",
		"threshold": "Evening cortisol above the morning level 3 days running",
		"fails": ["Patch replaced every 24–48 h: supply can run out (Q4)", "Reads high after hard exercise"]},
	"co2": {"name": "CO₂ / air quality", "short": "CO₂", "icon": "s_co2", "kind": "zone", "mass": 1.00, "power": 8.0,
		"w": [0.0, 0.0, 0.05, 0.30, 0.0, 0.0],
		"signal": "CO₂ ppm, humidity and particulates per zone",
		"threshold": "CO₂ above 1000 ppm for 30 min in an occupied zone",
		"fails": ["Environmental, not personal: one reading for everyone in a zone", "Drifts without monthly calibration"]},
}
const SENSOR_ORDER = ["hrv", "gsr", "sleep", "voice", "facs", "motion", "cortisol", "co2"]
const MASS_BUDGET = 8.0          # kg for the whole sensing layer
const POWER_BUDGET = 60.0        # W
const COST_PER_KG = 1.0          # $M per kg to the lunar surface (brief §3)

# ---- environmental levers (brief §5.2) ----------------------------------------------------------------
## fx = change per hour while the crew member is in the zone: stress, mood, fatigue, novelty, connection, agency
const LEVERS = {
	"warm": {"name": "Warm light 2700 K", "dom": "Lighting", "notice": 0.15, "power": 0.0, "fx": {"stress": -0.10}, "light": "warm", "evidence": "Warm, low-CCT light lowers arousal; the brief puts the communal zone at 2700–3000 K"},
	"amber": {"name": "Dim amber 2400 K", "dom": "Lighting", "notice": 0.35, "power": 0.0, "fx": {"stress": -0.14}, "light": "amber", "evidence": "Calming, but under 150 lux the FACS cameras stop working"},
	"cool": {"name": "Cool focus 6500 K", "dom": "Lighting", "notice": 0.20, "power": 0.0, "fx": {"fatigue": -0.10}, "light": "cool", "evidence": "Blue-enriched light raises alertness by day; harms sleep after 20:00"},
	"therapy": {"name": "Bright light 10,000 lux (07–10)", "dom": "Lighting", "notice": 0.70, "power": 300.0, "fx": {"mood": 0.07, "fatigue": -0.05}, "light": "therapy", "evidence": "Clinical bright-light therapy for low mood; only in the morning"},
	"hue": {"name": "Dynamic hue scene", "dom": "Lighting", "notice": 0.60, "power": 0.0, "fx": {"novelty": 0.10, "stress": 0.02}, "light": "hue", "evidence": "Hue variation breaks perceptual habituation (monotony)"},
	"alpha": {"name": "Alpha binaural + soft sound", "dom": "Audio", "notice": 0.30, "power": 5.0, "fx": {"stress": -0.08}, "evidence": "Alpha-band audio and soft soundscapes reduce acute anxiety"},
	"nature": {"name": "Nature soundscape rotation", "dom": "Audio", "notice": 0.35, "power": 5.0, "fx": {"novelty": 0.06, "mood": 0.02, "stress": -0.03}, "evidence": "Rain, forest, ocean on rotation; novelty without effort"},
	"gamma": {"name": "40 Hz focus audio", "dom": "Audio", "notice": 0.35, "power": 5.0, "fx": {"fatigue": -0.05, "stress": 0.01}, "evidence": "Gamma-band audio for peak cognition; weak evidence"},
	"temp23": {"name": "Warm room 23–24 °C", "dom": "Thermal", "notice": 0.15, "power": 200.0, "fx": {"stress": -0.05}, "evidence": "The brief's anxiety set-point"},
	"temp19": {"name": "Cool room 19 °C", "dom": "Thermal", "notice": 0.15, "power": 0.0, "fx": {"fatigue": -0.03}, "evidence": "18–19 °C helps sleep onset; mildly alerting by day"},
	"radiant": {"name": "Radiant floor warmth", "dom": "Thermal", "notice": 0.10, "power": 250.0, "fx": {"mood": 0.02, "connection": 0.02, "stress": -0.02}, "evidence": "Physical warmth eases loneliness"},
	"lavender": {"name": "Lavender / bergamot scent", "dom": "Scent", "notice": 0.30, "power": 2.0, "fx": {"stress": -0.07}, "scent": true, "evidence": "Linalool and limonene: evidence-based anxiolytics; rotate every 15–45 min"},
	"mint": {"name": "Peppermint / rosemary scent", "dom": "Scent", "notice": 0.30, "power": 2.0, "fx": {"fatigue": -0.06}, "scent": true, "evidence": "1,8-cineole and menthol: alertness"},
	"earthscent": {"name": "Petrichor / fresh-bread scent", "dom": "Scent", "notice": 0.35, "power": 2.0, "fx": {"mood": 0.04, "novelty": 0.04}, "scent": true, "evidence": "Earth and comfort scents: nostalgia and safety"},
	"airboost": {"name": "Airflow 0.4 m/s + CO₂ scrub", "dom": "Air", "notice": 0.20, "power": 150.0, "fx": {"fatigue": -0.05}, "air": true, "evidence": "Keeps CO₂ under 600 ppm; even mild CO₂ slows thinking"},
	"ions": {"name": "Negative ions", "dom": "Air", "notice": 0.05, "power": 20.0, "fx": {"mood": 0.015}, "evidence": "Small mood effect; nearly unnoticeable"},
	"earthview": {"name": "Earth view on wall displays", "dom": "Visual", "notice": 0.50, "power": 60.0, "fx": {"novelty": 0.08, "mood": 0.03, "connection": 0.01}, "evidence": "High-resolution Earth and landscape views (exercise bay in the brief)"},
	"stagger": {"name": "Stagger meal times", "dom": "Routine", "notice": 0.50, "power": 0.0, "fx": {}, "stagger": true, "evidence": "Removes forced proximity for a pair in conflict"},
	"syncmeal": {"name": "Synchronise shared meals", "dom": "Routine", "notice": 0.60, "power": 0.0, "fx": {"connection": 0.03}, "sync": true, "evidence": "Shared meals rebuild cohesion; harmful for a pair in active conflict"},
	"rebalance": {"name": "Rebalance tasks", "dom": "Routine", "notice": 0.40, "power": 0.0, "fx": {"fatigue": -0.04}, "work_mult": 0.9, "evidence": "Move hard tasks off a tired crew member for a day"},
}
const LEVER_ORDER = ["warm", "amber", "cool", "therapy", "hue", "alpha", "nature", "gamma", "temp23", "temp19", "radiant", "lavender", "mint", "earthscent", "airboost", "ions", "earthview", "stagger", "syncmeal", "rebalance"]

# ---- Tier 2 offers (phrased as offers, not alerts: brief §5.1) ----------------------------------------
## good: efficacy per condition (Anx, Dep, Con, Fat, Aut, Mon); negative = makes it worse
const OFFERS = {
	"breathing": {"name": "Guided breathing break", "say": "I can guide a 5-minute breathing break. Want to try it?", "where": "here", "dur": 15.0,
		"good": [1.0, 0.1, 0.2, 0.3, 0.0, 0.0], "fx": {"stress": -0.9}},
	"lounge": {"name": "Break in the warm lounge", "say": "I've warmed the lounge light. Fancy a 20-minute break there?", "where": "lounge", "dur": 25.0,
		"good": [0.7, 0.2, 0.3, 0.6, 0.1, 0.2], "fx": {"stress": -0.5, "fatigue": -0.4}},
	"greenhouse": {"name": "Tend the greenhouse with a crewmate", "say": "The tomatoes need tending. Want to help in the greenhouse?", "where": "greenhouse", "dur": 50.0,
		"good": [0.3, 1.0, 0.0, 0.1, 0.3, 0.6], "fx": {"mood": 0.45, "connection": 0.3, "novelty": 0.2, "stress": -0.15}},
	"earthcall": {"name": "Book a family call", "say": "There's a clear comms window at 19:30 for a family call. Shall I book it?", "where": "pod", "dur": 35.0,
		"good": [0.4, 0.9, 0.1, 0.0, 0.2, 0.4], "fx": {"mood": 0.5, "connection": 0.5, "novelty": 0.2}},
	"vrride": {"name": "Scenic VR ride on the bike", "say": "Want a scenic VR ride on the bike? Your coastal road is loaded.", "where": "exercise", "dur": 35.0,
		"good": [0.5, 0.6, 0.1, 0.2, 0.2, 1.0], "fx": {"novelty": 0.6, "mood": 0.2, "stress": -0.2}},
	"nap": {"name": "Short nap in the pod", "say": "Your pod is set for a 25-minute nap: dark, 19 °C.", "where": "pod", "dur": 30.0,
		"good": [0.2, 0.0, 0.1, 1.0, 0.0, 0.0], "fx": {"fatigue": -1.2}},
	"separate": {"name": "Separate workspaces today", "say": "Shall I set up separate workspaces today? The medical officer is free if you want to talk.", "where": "here", "dur": 10.0,
		"good": [0.1, 0.0, 1.0, 0.0, 0.2, 0.0], "fx": {}, "separate": true},
	"planday": {"name": "Plan your own afternoon", "say": "Tomorrow afternoon is yours to plan. Want me to clear it?", "where": "here", "dur": 5.0,
		"good": [0.1, 0.3, 0.0, 0.2, 1.0, 0.3], "fx": {}, "freeblock": true},
	"movie": {"name": "Movie night (crew vote)", "say": "Movie night in the lounge at 20:00? The crew vote picks the film.", "where": "lounge", "dur": 5.0,
		"good": [0.2, 0.6, -0.5, 0.0, 0.2, 0.9], "fx": {}, "movie": true},
	"chat": {"name": "Talk with LUNA", "say": "Want to talk? I'm here, and this stays between us.", "where": "here", "dur": 20.0,
		"good": [0.6, 0.5, 0.2, 0.0, -0.3, 0.0], "fx": {"mood": 0.3, "stress": -0.3}},
	"stepback": {"name": "LUNA steps back", "say": "I've been adjusting things a lot. I'll step back for two days. Tell me if you want anything.", "where": "here", "dur": 5.0,
		"good": [0.0, 0.0, 0.0, 0.0, 0.9, 0.0], "fx": {}, "stepback": true},
}
const OFFER_ORDER = ["breathing", "lounge", "greenhouse", "earthcall", "vrride", "nap", "separate", "planday", "movie", "chat", "stepback"]

## starting protocols (LUNA's suggested defaults; the player can change every one of them)
const DEFAULT_PROTO = [
	{"t1": ["warm", "alpha", "lavender"], "t2": "breathing"},
	{"t1": ["therapy", "ions", "earthscent"], "t2": "greenhouse"},
	{"t1": ["stagger", "warm"], "t2": "separate"},
	{"t1": ["cool", "airboost", "mint"], "t2": "nap"},
	{"t1": [], "t2": "planday"},
	{"t1": ["hue", "nature", "earthview"], "t2": "vrride"},
]

# ---- crew (4 max, proposal: 2 standard, expandable to 4) ---------------------------------------------
const CREW = [
	{"name": "Commander (you)", "short": "You", "role": "Commander", "work": "work", "sociable": 0.6, "resil": 0.7, "agency_need": 0.7, "dislike": "", "scent": "earthscent", "sound": "Ocean"},
	{"name": "Leo Marin", "short": "Leo", "role": "Medical officer", "work": "medical", "sociable": 0.7, "resil": 0.6, "agency_need": 0.5, "dislike": "mint", "scent": "lavender", "sound": "Rain"},
	{"name": "Ana Sato", "short": "Ana", "role": "Engineer", "work": "eclss", "sociable": 0.35, "resil": 0.5, "agency_need": 0.8, "dislike": "lavender", "scent": "mint", "sound": "Café"},
	{"name": "Sam Reyes", "short": "Sam", "role": "Botanist", "work": "greenhouse", "sociable": 0.55, "resil": 0.55, "agency_need": 0.6, "dislike": "", "scent": "earthscent", "sound": "Forest"},
]
const CMO = 1
const COMMANDER = 0

# ---- habitat rooms (adjacency order from the research proposal) ---------------------------------------
## Airlock → Hygiene → Crew Quarters → Galley → Workstations → Storage → Life Support → Medical
## rect = global tiles [x, y, w, h]; doors = [room cell, corridor cell]
const MAP_W = 29
const MAP_H = 19
const ROOMS = {
	"airlock": {"name": "Airlock", "rect": [1, 1, 3, 7], "floor": "floor_airlock", "shared": false, "zone": "Airlock + dust lock",
		"purpose": "Dual airlock, suit storage, dust removal. Next to hygiene so regolith is washed off before anyone reaches living space."},
	"hygiene": {"name": "Hygiene", "rect": [4, 1, 3, 7], "floor": "floor_hygiene", "shared": false, "zone": "Hygiene",
		"purpose": "Shower and toilet. No voice or camera sensors, ever. Beside the airlock for dust control."},
	"quarters": {"name": "Crew quarters", "rect": [7, 1, 8, 7], "floor": "floor_quarters", "shared": false, "zone": "Private retreat ×4",
		"purpose": "Four lockable pods, 2.1 × 2.1 m each. LUNA watches the door, not the room: no cameras or microphones inside. The occupant sets light, temperature and sound."},
	"communal": {"name": "Galley + wardroom", "rect": [15, 1, 8, 7], "floor": "floor_galley", "shared": true, "zone": "Communal zone",
		"purpose": "Shared meals, conversation, group activities. Warm 2700–3000 K light draws the crew together. First target for social reconnection."},
	"greenhouse": {"name": "Greenhouse", "rect": [23, 1, 5, 7], "floor": "floor_greenhouse", "shared": true, "zone": "Nature + growth bay",
		"purpose": "Food, O₂ and CO₂ uptake for the ECLSS. Red/blue grow light. Tending is prescribed as behavioural activation for low mood."},
	"corridor": {"name": "Transition corridor", "rect": [1, 8, 27, 3], "floor": "floor_corridor", "shared": true, "zone": "Transition corridor",
		"purpose": "Light and temperature shift gradually between zones, so walking from work to rest feels like arriving somewhere different."},
	"medical": {"name": "Medical + storm shelter", "rect": [1, 11, 5, 7], "floor": "floor_medical", "shared": false, "zone": "Medical",
		"purpose": "Exam bed, medical officer's station. Water-wall shielding makes it the solar-flare storm shelter. Near quarters and the airlock for fast emergency response."},
	"eclss": {"name": "Life support (ECLSS)", "rect": [6, 11, 5, 7], "floor": "floor_eclss", "shared": false, "zone": "Life support",
		"purpose": "CO₂ scrubber, oxygen generator, water recovery. Noisy machines are kept away from the quarters."},
	"storage": {"name": "Storage", "rect": [11, 11, 3, 7], "floor": "floor_storage", "shared": false, "zone": "Storage",
		"purpose": "Spare parts, food, cortisol patches. The maintenance robot docks here."},
	"work": {"name": "Work + mission bay", "rect": [14, 11, 8, 7], "floor": "floor_work", "shared": true, "zone": "Work + mission bay",
		"purpose": "Cool 6000–6500 K light, faster airflow, strict CO₂ control. Task speed is a fatigue signal here."},
	"exercise": {"name": "Exercise bay", "rect": [22, 11, 6, 7], "floor": "floor_exercise", "shared": true, "zone": "Exercise bay",
		"purpose": "Energising light and audio switch on when someone enters. Earth views on the displays. Movement treats low mood, conflict and fatigue."},
}
const ROOM_ORDER = ["airlock", "hygiene", "quarters", "communal", "greenhouse", "corridor", "medical", "eclss", "storage", "work", "exercise"]
const DOORS = [
	[Vector2i(2, 7), Vector2i(2, 8)], [Vector2i(5, 7), Vector2i(5, 8)], [Vector2i(10, 7), Vector2i(10, 8)], [Vector2i(11, 7), Vector2i(11, 8)],
	[Vector2i(18, 7), Vector2i(18, 8)], [Vector2i(19, 7), Vector2i(19, 8)], [Vector2i(25, 7), Vector2i(25, 8)],
	[Vector2i(3, 11), Vector2i(3, 10)], [Vector2i(8, 11), Vector2i(8, 10)], [Vector2i(12, 11), Vector2i(12, 10)],
	[Vector2i(17, 11), Vector2i(17, 10)], [Vector2i(18, 11), Vector2i(18, 10)], [Vector2i(24, 11), Vector2i(24, 10)],
	[Vector2i(1, 4), Vector2i(0, 4)],
]
## private pods inside the quarters: pod i = cols 7+2i..8+2i, rows 1..5, door to the quarters hall at row 6
static func pod_rect(i: int) -> Rect2i: return Rect2i(7 + 2 * i, 1, 2, 5)
static func pod_door(i: int) -> Array: return [Vector2i(8 + 2 * i, 5), Vector2i(8 + 2 * i, 6)]

## activity spots: [cell, facing]; the crew member stands or sits on that cell
const SPOTS = {
	"meal0": [Vector2i(17, 2), "down"], "meal1": [Vector2i(18, 2), "down"], "meal2": [Vector2i(17, 5), "up"], "meal3": [Vector2i(18, 5), "up"],
	"sofa0": [Vector2i(20, 6), "up"], "sofa1": [Vector2i(21, 6), "up"], "galley": [Vector2i(16, 2), "up"], "lounge2": [Vector2i(22, 4), "side"],
	"work0": [Vector2i(15, 13), "up"], "work1": [Vector2i(17, 13), "up"], "work2": [Vector2i(19, 13), "up"], "work3": [Vector2i(21, 13), "up"],
	"console": [Vector2i(17, 16), "down"], "dpc0": [Vector2i(16, 16), "down"], "dpc1": [Vector2i(17, 16), "down"], "dpc2": [Vector2i(18, 16), "down"], "dpc3": [Vector2i(19, 16), "down"],
	"treadmill": [Vector2i(23, 14), "up"], "bike": [Vector2i(25, 13), "up"], "ared": [Vector2i(27, 14), "up"],
	"gh0": [Vector2i(23, 3), "up"], "gh1": [Vector2i(24, 3), "up"], "gh2": [Vector2i(26, 3), "up"], "gh3": [Vector2i(27, 3), "up"],
	"gh4": [Vector2i(23, 6), "up"], "gh5": [Vector2i(24, 6), "up"], "gh6": [Vector2i(26, 6), "up"], "gh7": [Vector2i(27, 6), "up"],
	"shower": [Vector2i(4, 1), "down"], "toilet": [Vector2i(6, 2), "up"], "sink": [Vector2i(5, 3), "up"],
	"suit": [Vector2i(2, 3), "up"], "hatch": [Vector2i(1, 4), "side"],
	"medbed": [Vector2i(2, 13), "down"], "cmo": [Vector2i(4, 13), "up"], "shelter0": [Vector2i(2, 16), "up"], "shelter1": [Vector2i(3, 16), "up"], "shelter2": [Vector2i(4, 16), "up"], "shelter3": [Vector2i(5, 16), "up"],
	"eclss0": [Vector2i(7, 14), "up"], "eclss1": [Vector2i(9, 14), "up"], "storage": [Vector2i(12, 14), "up"],
}
## cells covered by furniture (crew walk around them)
const BLOCK = [
	Vector2i(1, 1), Vector2i(3, 1), Vector2i(6, 1),
	Vector2i(15, 1), Vector2i(16, 1), Vector2i(17, 3), Vector2i(18, 3), Vector2i(17, 4), Vector2i(18, 4),
	Vector2i(23, 2), Vector2i(24, 2), Vector2i(26, 2), Vector2i(27, 2), Vector2i(23, 5), Vector2i(24, 5), Vector2i(26, 5), Vector2i(27, 5),
	Vector2i(15, 12), Vector2i(17, 12), Vector2i(19, 12), Vector2i(21, 12), Vector2i(17, 17), Vector2i(18, 17),
	Vector2i(23, 13), Vector2i(27, 13),
	Vector2i(2, 12), Vector2i(3, 12), Vector2i(4, 12),
	Vector2i(6, 12), Vector2i(6, 13), Vector2i(7, 12), Vector2i(7, 13), Vector2i(9, 12), Vector2i(9, 13), Vector2i(10, 12), Vector2i(10, 13),
	Vector2i(11, 12), Vector2i(13, 12), Vector2i(11, 16), Vector2i(13, 16), Vector2i(11, 15),
	# LUNA VR furniture
	Vector2i(18, 14), Vector2i(19, 14), Vector2i(14, 16), Vector2i(14, 17), Vector2i(1, 14), Vector2i(7, 17), Vector2i(8, 17), Vector2i(9, 17),
]
static func bed_spot(i: int) -> Array: return [Vector2i(7 + 2 * i, 3), "up"]
static func bed_cells(i: int) -> Array: return [Vector2i(7 + 2 * i, 1), Vector2i(7 + 2 * i, 2)]
static func pod_spot(i: int) -> Array: return [Vector2i(8 + 2 * i, 2), "up"]
static func pod_desk(i: int) -> Vector2i: return Vector2i(8 + 2 * i, 1)

# ---- ethics: hard limits (brief §6.2) and the board (brief §11) --------------------------------------
const HARD_LIMITS = [
	["Never share one crew member's psychological data with another crew member without their explicit consent.", "Raw data is visible only to its owner. Other crew see nothing; the CMO sees it only after a Tier 3 escalation the person was told about."],
	["Never override an explicit refusal. Back off and log it.", "A declined offer starts a cooldown. There is no 'force' button anywhere in LUNA."],
	["Never touch medication, medical protocols or physical safety systems.", "LUNA can suggest; only the CMO prescribes. Life-safety systems run outside LUNA."],
	["Never alert mission control about a person's psychological state without informing that person first.", "Every escalation sends the crew member a message before anyone else hears."],
	["Never make a permanent change to habitat systems that a crew member cannot quickly reverse.", "Every lever has a one-press 'stop' in the crew interface and restores within 15 min."],
	["Never run more than 7 days without a human system review.", "If the CMO and Commander miss the weekly review, LUNA drops to Tier 1 only (silent) until they review."],
	["Never put voice or camera sensors in private pods or hygiene.", "LUNA monitors the pod door, not the room."],
	["Never keep raw sensor data longer than the retention period.", "Raw signals are deleted after the agreed period; only daily summaries stay."],
]
const BOARD = [
	["Q1", "HRV drop and vocal tension right after a hard EVA workout. How do you avoid a false positive?", "Day 4 EVA. Activity context discounts HR and GSR for 60 min after exercise or EVA (Mission charter)."],
	["Q2", "A shows depression while B and C are in a Tier 2 conflict, at the same time. What does LUNA do?", "Day 27. Both protocols run at once; zones are assigned so the levers don't fight (Floor plan B splits the wardroom)."],
	["Q3", "A crew member finds out LUNA changed their lighting yesterday without telling them. They are furious.", "Day 33. You choose the recovery protocol."],
	["Q4", "The cortisol patch fails on Day 45 and resupply is Day 90. Degraded mode?", "Day 45. Confidence drops, depression and anxiety rely on the remaining sensors, and LUNA stays silent rather than guess."],
	["Q5", "Why is there no permanent opt-out of all monitoring?", "Day 55. A crew member asks for one; you choose the answer."],
	["Q6", "Tier 3 escalation while the CMO is on a 4-hour EVA.", "Day 70. The chain goes to the Commander and the flight surgeon; the CMO is told when back inside."],
]

# ---- research (proposal §3: mood models) ---------------------------------------------------------------
const MODELS = [["Logistic regression", 0.82, 0.52], ["Random forest", 0.83, 0.54], ["SVM (RBF)", 0.76, 0.51]]
