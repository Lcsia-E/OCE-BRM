using System.Collections;
using UnityEngine;
using SpatialSys.UnitySDK; 

public class Response : MonoBehaviour
{
    // How long (in seconds) the pellet will stay hidden before reappearing
    public float respawnDelay = 1f;

    // Keeps track of how many pellets have been collected
    public static int pelletsCollected = 0;

    // A reference to the Pellet object in the scene
    private GameObject pellet;

    // Start() runs once, at the beginning
    void Start()
    {
        // Find the object named "pellet" in the scene and store it
        pellet = GameObject.Find("pellet");
    }

    // Public method: can be called from a button, UnityEvent, or another script
    public void CollectPellet()
    {
        // If the pellet was not found, do nothing
        if (pellet == null) return;

        // Hide the pellet temporarily
        pellet.SetActive(false);

        // Increase the count of collected pellets
        pelletsCollected++;

        // Show a small message on screen with the updated count
        SpatialBridge.coreGUIService.DisplayToastMessage(
            "Pellets collected: " + pelletsCollected.ToString(),
            2f // message will stay visible for 2 seconds
        );

        // Start a coroutine to wait before reactivating the pellet
        StartCoroutine(WaitPellet());
    }

    // Coroutine: waits for a delay, then makes the pellet visible again
    private IEnumerator WaitPellet()
    {
        // Pause for the chosen number of seconds
        yield return new WaitForSeconds(respawnDelay);

        // Reactivate the pellet so it appears again in the scene
        pellet.SetActive(true);
    }
}
