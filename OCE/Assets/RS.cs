using SpatialSys.UnitySDK;   // Spatial.io SDK namespace: gives access to Avatar, GUI, Data Store, etc.
using System.Collections;    // Required for IEnumerator and coroutines (for delayed or animated actions)
using UnityEngine;           // Core Unity namespace: GameObject, MonoBehaviour, Vector3, etc.

public class RS : MonoBehaviour
{
    // ======================
    // SESSION & TIMING SETUP
    // ======================

    private string oceID;                  // Unique identifier for this session ("OCE_xxxxxx")
    private float Timer_Interval = 0f;     // Tracks time since last reinforcement-eligible action
    private float Timer_Session = 0f;      // Tracks total elapsed time of the session
    private bool sessionEnded = false;     // Flag: has the session finished?
    private bool isCounting_Interval = false; // Should we be incrementing Timer_Interval?
    private bool isCounting_Session = false;  // Should we be incrementing Timer_Session?

    // ================================
    // VISUAL & AUDIO GAME OBJECTS
    // ================================

    private GameObject Ligth_Green;       // Green light indicator in the scene
    private GameObject Ligth_Red;         // Red light indicator in the scene
    private GameObject Ligth_Gen;         // Generic (yellow/gen) light indicator

    private GameObject Model_Level;       // The level “platform” the user interacts with
    private GameObject Pellet;            // The reward pellet object

    private Vector3 Initial_Position_Level;   // Remember where the level started
    private Vector3 Initial_Position_Pellet;  // Remember where the pellet started

    private AudioSource Audio_Level_Down; // Sound played when the level is pressed down
    private AudioSource Audio_Level_Up;   // Sound played when the level is released
    private AudioSource Audio_Eat;        // Sound for “eating” the pellet
    private AudioSource Audio_Dispenser;  // Sound for dispensing a pellet

    // ====================================
    // REINFORCEMENT & RESPONSE TRACKING
    // ====================================

    private int Response_Count = 0;           // How many times the user has pressed/released
    private int Reinforcement_Count = 0;      // How many pellets have been dispensed
    private int Reinforcements_Eaten_Count = 0; // How many pellets the user has “eaten”

    // Ratio (number of responses) and Interval (minimum time) schedules
    private float Interval = 0.01f;           // Current time-based schedule threshold
    private int Ratio = 5;                    // Current response-count threshold
    private int RatioCount = 0;               // How many responses since last reinforcement

    // Player position tracking (relative to the level) 
    private float X_Participant = 0f;
    private float Y_Participant = 0f;

    // Lists for randomly choosing schedules
    public int[] Ratio_List = { 0, 0, 0, 0, 0 };      // Fill these in with desired ratios
    public int[] Interval_List = { 0, 0, 0, 0, 0 };   // Fill these in with desired intervals
    public float Session_Time = 10f;                  // Total session length in seconds

    // =====================
    // DATA RECORDING SETUP
    // =====================

    private string Data = "";    // CSV-formatted session data we will build up
    private int currentBlockIndex = 0;  // For splitting data into chunks for server upload
    private System.Collections.Generic.List<string> dataBlocks = null;

    // =======================
    // UNITY LIFECYCLE: START
    // =======================

    void Start()
    {
        // 1) Create a random session ID with date so each run is unique
		string dateStamp = System.DateTime.Now.ToString("ddMMyy");
        oceID = "OCE_" + dateStamp + "_" + Random.Range(100000, 999999);

        // 2) Build the CSV header (ID + column names)
        Data = oceID + "\n";
        Data += "Time,Resp,Rein,Eat,X,Y\n";

        // 3) Begin counting the interval timer
        isCounting_Interval = true;

        // 4) Find the red/green/generic lights in the scene hierarchy
        Ligth_Red = GameObject.Find("Ligth_Red_VG");
        Ligth_Green = GameObject.Find("Ligth_Green_VG");
        Ligth_Gen = GameObject.Find("Ligth_Gen_VG");

        // 5) Start with only the red light on (others off)
        Ligth_Green.SetActive(false);
        Ligth_Gen.SetActive(false);

        // 6) Find the level model, remember its starting position
        Model_Level = GameObject.Find("Model_Level");
        Initial_Position_Level = Model_Level.transform.position;

        // 7) Find the pellet, remember its start pos, and hide it
        Pellet = GameObject.Find("Pellet");
        Initial_Position_Pellet = Pellet.transform.position;
        Pellet.SetActive(false);

        // 8) Grab AudioSource components by name (if they exist)
        Audio_Level_Down = GameObject.Find("Audio_Level_Down")?.GetComponent<AudioSource>();
        Audio_Level_Up   = GameObject.Find("Audio_Level_Up")?.GetComponent<AudioSource>();
        Audio_Eat        = GameObject.Find("Audio_Eat")?.GetComponent<AudioSource>();
        Audio_Dispenser  = GameObject.Find("Audio_Dispenser")?.GetComponent<AudioSource>();

        // 9) Randomly pick one ratio and one interval from your lists
        Ratio = Ratio_List[Random.Range(0, Ratio_List.Length)];
        Interval = Interval_List[Random.Range(0, Interval_List.Length)];
    }

    // =====================================
    // USER INVOKED: START THE EXPERIMENT
    // =====================================

    // Called by a UI button or other event to teleport the user and begin the session
    public void TeleportParticipant()
    {
        // a) Get the local player's avatar through the Spatial SDK
        IAvatar localAvatar = SpatialBridge.actorService.localActor.avatar;

        // b) Compute the new position: start-of-level + (x:0, y:2, z:-3)
        //    (this moves the user 2 units up and 3 units backward relative to the level)
        Vector3 targetPosition = Initial_Position_Level + new Vector3(0f, 2f, -3f);

        // c) Teleport the avatar in one atomic call (preserves their current rotation)
        localAvatar.SetPositionRotation(targetPosition, localAvatar.rotation);

        // d) Begin counting the session and interval timers
        StartTimer();
        StartCoroutine(RecordDataPeriodically());

        // e) Show a quick on-screen message using Spatial’s GUI service
        SpatialBridge.coreGUIService.DisplayToastMessage("Session Started");

        // f) Now we should track the overall session time too
        isCounting_Session = true;
    }

    // ====================================
    // LEVEL PRESS & RELEASE EVENT HANDLERS
    // ====================================

    // Called when the user presses the level button down
    public void Activate_LevelPress()
    {
        // Play sound if assigned
        if (Audio_Level_Down) Audio_Level_Down.Play();

        // Start a coroutine to animate the level tilting down
        StartCoroutine(RotateLevelPress());
    }

    // Called when the user releases the level button
    public void Activate_LevelRelease()
    {
        // Play release sound
        if (Audio_Level_Up) Audio_Level_Up.Play();

        // Animate level tilting back up
        StartCoroutine(RotateLevelRelease());

        // Count this as a user response
        Response_Count++;
        RatioCount++;

        // Check if we meet both: (a) enough time has passed AND (b) enough responses
        if (Timer_Interval >= Interval && RatioCount >= Ratio)
        {
            // Dispense a pellet: play sound, switch lights, show pellet at its start pos
            if (Audio_Dispenser) Audio_Dispenser.Play();
            Ligth_Red.SetActive(false);
            Ligth_Gen.SetActive(true);
            Pellet.SetActive(true);
            Pellet.transform.position = Initial_Position_Pellet;

            Reinforcement_Count++;
            // After 2 seconds, turn the red light back on
            StartCoroutine(Ligth_Red_Off(2f));
        }
    }

    // =================================
    // LEVEL ANIMATION COROUTINES
    // =================================

    // Animate the level rotating downward by 15 degrees over 0.1s
    private IEnumerator RotateLevelPress()
    {
        yield return RotateLevel(Model_Level, 15f, 0.1f);
    }

    // Animate the level rotating back up by –10 degrees over 0.1s
    private IEnumerator RotateLevelRelease()
    {
        yield return RotateLevel(Model_Level, -10f, 0.1f);
    }

    // Generic helper to smoothly rotate any GameObject around Z axis
    private IEnumerator RotateLevel(GameObject target, float targetAngle, float duration)
    {
        float time = 0f;
        // Get current Z rotation in –180…+180 range
        float startAngle = target.transform.rotation.eulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;

        // Lerp from startAngle to targetAngle over duration
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float currentAngle = Mathf.Lerp(startAngle, targetAngle, Mathf.SmoothStep(0f, 1f, t));
            target.transform.rotation = Quaternion.Euler(
                target.transform.rotation.eulerAngles.x,
                -90f,                    // Keep the Y-axis tilt fixed
                currentAngle
            );
            yield return null;
        }

        // Ensure exact final rotation
        target.transform.rotation = Quaternion.Euler(
            target.transform.rotation.eulerAngles.x,
            -90f,
            targetAngle
        );
    }

    // ====================================
    // PELLET “EATEN” EVENT
    // ====================================

    // Call when the user “eats” or removes the pellet
    public void Destroy_Pellet()
    {
        Reinforcements_Eaten_Count++;
        if (Audio_Eat) Audio_Eat.Play();
        if (Pellet) Pellet.SetActive(false);
    }

    // ====================================
    // TURN RED LIGHT BACK ON COROUTINE
    // ====================================

    // After a delay, re-enable the red light and reset schedule parameters
    private IEnumerator Ligth_Red_Off(float delay)
    {
        yield return new WaitForSeconds(delay);

        Ligth_Red.SetActive(true);
        Ligth_Gen.SetActive(false);

        // Pick new random ratio & interval
        Ratio = Ratio_List[Random.Range(0, Ratio_List.Length)];
        Interval = Interval_List[Random.Range(0, Interval_List.Length)];

        RatioCount = 0;
        StartTimer(); // Restart the interval timer
    }

    // =======================
    // TIMER CONTROL METHODS
    // =======================

    // Start (or restart) the interval timer
    public void StartTimer()
    {
        isCounting_Interval = true;
        Timer_Interval = 0f;
    }

    // Pause the interval timer
    public void StopTimer()
    {
        isCounting_Interval = false;
    }

    // ============================
    // UNITY LIFECYCLE: UPDATE LOOP
    // ============================

    void Update()
    {
        // a) Increment the interval timer if active
        if (isCounting_Interval)
        {
            Timer_Interval += Time.deltaTime;
        }

        // b) Increment the session timer if the session has started
        if (isCounting_Session)
        {
            Timer_Session += Time.deltaTime;
        }

        // c) Check for session end based on elapsed time
        if (Timer_Session >= Session_Time && !sessionEnded)
        {
            EndSession();
        }

        // d) Continuously track player position relative to level
        IAvatar localAvatar = SpatialBridge.actorService.localActor.avatar;
        Vector3 playerPosition = localAvatar.position;

        X_Participant = playerPosition.x - Initial_Position_Level.x;
        Y_Participant = playerPosition.z - Initial_Position_Level.z;
    }

    // ===========================
    // SESSION TEARDOWN & CLEANUP
    // ===========================

    // Called once when the session time limit is reached
    private void EndSession()
    {
        sessionEnded = true;
        isCounting_Interval = false;

        // Hide all lights and pellet
        Ligth_Red.SetActive(false);
        Ligth_Green.SetActive(false);
        Ligth_Gen.SetActive(false);
        Pellet.SetActive(false);

        // Move the avatar up to (0,5,0) to get them out of the way
        IAvatar localAvatar = SpatialBridge.actorService.localActor.avatar;
        Vector3 targetPosition = new Vector3(0f, 5f, 0f);
        localAvatar.SetPositionRotation(targetPosition, localAvatar.rotation);

        SpatialBridge.coreGUIService.DisplayToastMessage("Session finished!");
        SaveSessionData(Data);
    }

    // ============================
    // DATA STORAGE & UPLOAD LOGIC
    // ============================

    // Save the full CSV string into Spatial’s cloud variable store
    private void SaveSessionData(string sessionData)
    {
        SpatialBridge.userWorldDataStoreService.SetVariable("sessionData", sessionData);
        SpatialBridge.coreGUIService.DisplayToastMessage("✅ Data saved");
    }

    // Send data to an external server in base64 chunks
    public void SendDataToServer(string sessionData, string blockName)
    {
        string encodedData = System.Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(sessionData)
        );
        string url = $"https://lcsia.com.mx/VR/VRSpatialGetData.php?block_name={blockName}&session_data={encodedData}";

        SpatialBridge.spaceService.OpenURL(url);
    }

    // Load saved data from Spatial cloud, split into 120-line blocks, then send
    public void OpenSavedSessionData()
    {
        if (dataBlocks == null)
        {
            // First call: fetch the stored CSV
            SpatialBridge.userWorldDataStoreService
                .GetVariable("sessionData", "")
                .SetCompletedEvent((response) =>
            {
                string savedData = response.stringValue;

                if (!string.IsNullOrEmpty(savedData))
                {
                    string[] lines = savedData
                        .Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                    dataBlocks = new System.Collections.Generic.List<string>();

                    // Keep the header separate, then create 120-line chunks
                    string header = lines[0];
                    for (int i = 1; i < lines.Length; i += 120)
                    {
                        int count = Mathf.Min(120, lines.Length - i);
                        string block = header + "\n" + string.Join("\n", lines, i, count);
                        dataBlocks.Add(block);
                    }

                    currentBlockIndex = 0;
                    SendNextBlock();
                }
                else
                {
                    SpatialBridge.coreGUIService.DisplayToastMessage("❌ No saved data found.");
                }
            });
        }
        else
        {
            // Subsequent calls: keep sending remaining blocks
            SendNextBlock();
        }
    }

    // Send the next block of data, or finish if we're done
    private void SendNextBlock()
    {
        if (currentBlockIndex < dataBlocks.Count)
        {
            string block = dataBlocks[currentBlockIndex];
            string blockName = oceID + "_P" + currentBlockIndex;
            SendDataToServer(block, blockName);

            SpatialBridge.coreGUIService.DisplayToastMessage(
                $"📤 Block {currentBlockIndex + 1}/{dataBlocks.Count} sent as {blockName}."
            );
            currentBlockIndex++;
        }
        else
        {
            SpatialBridge.coreGUIService.DisplayToastMessage("✅ All blocks have been sent.");
            currentBlockIndex = 0;
            dataBlocks = null;
        }
    }

    // ===================================
    // PERIODIC DATA RECORDING COROUTINE
    // ===================================

    // Every 0.5 seconds, append a new line of CSV data until the session ends
    private IEnumerator RecordDataPeriodically()
    {
        while (!sessionEnded)
        {
            Data += 
                Timer_Session.ToString("F2") + "," +
                Response_Count + "," +
                Reinforcement_Count + "," +
                Reinforcements_Eaten_Count + "," +
                X_Participant.ToString("F2") + "," +
                Y_Participant.ToString("F2") + "\n";

            yield return new WaitForSeconds(0.5f);
        }
    }
}
