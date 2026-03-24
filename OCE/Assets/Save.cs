using System;
using System.Text;
using System.Collections;
using UnityEngine;
using SpatialSys.UnitySDK; // Para guardar variables y abrir URL en Spatial

public class Save : MonoBehaviour
{
    // How many seconds to wait before saving and sending the data
    public float delayBeforeSend = 5f;

    // Your server endpoint (expects block_name and session_data in Base64)
    public string serverUrl = "https://lcsia.com.mx/VR/VRSpatialGetData.php";

    // A simple unique ID for this session (e.g., "Test_123456")
    private string sessionID;

    // Start() runs once, at the beginning
    void Start()
    {
        // Create a simple random session ID
		string dateStamp = System.DateTime.Now.ToString("ddMMyy");
        sessionID = "Test_" + dateStamp + "_" + UnityEngine.Random.Range(100000, 999999);

        // Start a coroutine that waits some seconds, then saves and sends the data
        StartCoroutine(SaveAndSendAfterDelay(delayBeforeSend));
    }

    // Coroutine: waits for a delay, then builds the CSV, encodes it, and sends it
    private IEnumerator SaveAndSendAfterDelay(float delay)
    {
        // Wait the chosen number of seconds
        yield return new WaitForSeconds(delay);

        // Read the global pellet counter from Response (must be accessible there)
        int collected = Response.pelletsCollected;

        // Build a very small CSV string with two fields
        // Header row + data row (session_id, pellets_collected)
        string csv = "session_id,pellets_collected\n" + sessionID + "," + collected + "\n";

        // Encode the CSV to Base64 (the server expects session_data in Base64)
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(csv));

        // Build the URL with the exact parameter names your PHP expects:
        // block_name = sessionID, session_data = Base64(CSV)
        string url = serverUrl
            + "?block_name=" + Uri.EscapeDataString(sessionID)
            + "&session_data=" + Uri.EscapeDataString(encoded);

        // Open the URL (GET request) so your server receives the data
        SpatialBridge.spaceService.OpenURL(url);

        //Show a small toast on screen as confirmation
        SpatialBridge.coreGUIService.DisplayToastMessage(
            "Sent: " + sessionID + " (" + collected + ")", 
            2f // visible for 2 seconds
        );
    }
}
