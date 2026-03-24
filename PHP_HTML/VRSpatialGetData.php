<?php
// ================================
// INITIAL CONFIGURATION
// ================================

// Define the folder where CSV data will be saved
// __DIR__ means "the same folder where this script is located"
// Using an absolute path derived from __DIR__ avoids issues where the PHP
// process might have a different working directory (e.g., when invoked by FPM/CLI).
// The trailing "/Data/" keeps all persisted artifacts grouped and avoids cluttering
// the web root. Ensure the PHP process user has write permissions here.
$dataDir = __DIR__ . "/Data/";

// Create the "Data" directory if it does not exist yet
// is_dir() checks for existence and that it is indeed a directory (not a file).
// mkdir(..., 0777, true) attempts to create the directory with full RWX permissions
// for owner/group/others and will create intermediate directories if missing.
// Note: In production, you may want stricter permissions (e.g., 0755) and/or rely
// on umask, but we keep behavior unchanged here.
if (!is_dir($dataDir)) mkdir($dataDir, 0777, true);

// ================================
// READING DATA SENT VIA URL
// ================================

// Check if 'session_data' has been received via GET (?session_data=...)
// This script expects the CSV payload to be Base64-encoded in that parameter,
// which makes it safe to transmit over URLs without breaking reserved characters.
// Example caller-side pseudo-flow:
//   const csv = "col1,col2\nA,B\n";
//   const b64 = btoa(csv);
//   GET /upload.php?session_data=<b64>&block_name=MY_BLOCK
if (isset($_GET['session_data'])) {

    // Decode the Base64 string into normal text (CSV)
    // base64_decode returns the decoded string or false on failure (e.g., invalid Base64, wrong padding).
    // It does not validate CSV content; it merely transforms from Base64 to bytes.
    // If the CSV may include non-UTF-8 bytes, make sure your downstream consumers can handle them.
    $sessionData = base64_decode($_GET['session_data']);
    if (!$sessionData) {
        // If decoding fails, stop execution and show error page
        // renderPage() prints a full HTML document with the provided status and message.
        // die(...) outputs the returned string and terminates the script immediately.
        die(renderPage("✖", "Failed to decode session data.", null, false));
    }

    // Optionally, a block name can also be sent (?block_name=...)
    // If present and valid, it will be used for the file name; otherwise we derive a fallback.
    // Note: Filenames derived from untrusted input must be sanitized to prevent path traversal,
    // special characters, or filesystem-specific issues.
    $blockName = isset($_GET['block_name']) ? $_GET['block_name'] : null;

    // Clean up the block name: only allow A–Z, numbers, and underscores
    // strtoupper() normalizes to uppercase, then preg_replace() strips anything
    // outside the whitelist. This prevents '../../', spaces, unicode trickery, etc.
    // If input is null, strtoupper(null) yields an empty string, which then sanitizes to "".
    $blockName = preg_replace("/[^A-Z0-9_]/", "", strtoupper($blockName));

    // If no block name was provided, use the first line of data as fallback
    // This is helpful when the client didn't specify a name: e.g., first CSV line might be a header
    // or an ID. We also trim whitespace. If that is empty, we default to "SESSION_BLOCK".
    // explode(..., 2) ensures we don't split the entire file—just the first newline boundary.
    if (!$blockName) {
        $lines = explode("\n", $sessionData, 2);
        $blockName = trim($lines[0]) ?: "SESSION_BLOCK";
    }

    // Define the file path where the CSV will be saved
    // The final file lives under the Data/ directory and carries a ".csv" extension.
    // Example: Data/MY_BLOCK.csv
    // Note: This path is deterministic from sanitized $blockName to avoid arbitrary write locations.
    $filename = $dataDir . $blockName . ".csv";

    // Try to save the decoded session data as a CSV file
    // file_put_contents returns the number of bytes written or false on failure (e.g., permissions).
    // It overwrites existing files by default (same behavior kept). If you need append-only semantics
    // or versioning, you could use FILE_APPEND or include a timestamp in the filename (not changed here).
    // Consider file locking (LOCK_EX) to avoid concurrent write interleaving in high-traffic scenarios.
    if (file_put_contents($filename, $sessionData) !== false) {
        // On success, render a page with a green status and a direct download link to the saved file.
        echo renderPage("✔", "Session block saved successfully", $blockName, true);
    } else {
        // On failure, render a red status page indicating the save did not complete.
        // Common causes: directory not writable, disk full, SELinux/AppArmor restrictions, etc.
        echo renderPage("✖", "Failed to save session block", $blockName, false);
    }
    exit; // Ensure we do not fall through to the generic error below.
}

// If no data was received, show default error page
// This branch covers requests missing ?session_data=..., which the script requires.
// The response still returns a friendly HTML page indicating the problem.
echo renderPage("✖", "No session data received.", null, false);

// ================================
// FUNCTION TO RENDER HTML RESPONSE
// ================================

function renderPage($statusSymbol, $message, $blockName = null, $success = false) {
    // Create a download link only if we have a block name
    // The link points to the relative path "Data/<blockName>.csv", assuming that the "Data" directory
    // is web-accessible. If your server blocks direct access to /Data, you might need a proxy endpoint.
    // htmlspecialchars() prevents HTML injection via the filename shown in the link (defense-in-depth).
    $downloadLink = $blockName ? "Data/" . htmlspecialchars($blockName) . ".csv" : "#";

    // Decide the color of the status icon based on success/failure.
    // Colors are used inline in CSS below.
    $successColor = $success ? 'green' : 'red';

    // Return an HTML page as a string (using heredoc syntax <<<HTML)
    // The page is self-contained: inline CSS, semantic headings, and a simple "card" layout.
    // It shows:
    //   - The main message (H1)
    //   - The block name (bolded line)
    //   - A large status symbol (✔ / ✖) colored by success/failure
    //   - If success, buttons to download the file and navigate to a "Download Page"
    // Security note:
    //   - We interpolate $message, $blockName, and $statusSymbol directly. In a hostile environment,
    //     consider escaping $message as well if it can contain user input. Here, messages are static.
    //   - $blockName, when used as text, is not escaped below because the heredoc places it as content.
    //     If $blockName could contain HTML-sensitive characters, wrap it with htmlspecialchars()
    //     where it's displayed (not only in $downloadLink). We keep behavior unchanged here.
    return <<<HTML
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Data Upload</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            background-color: #444;
            color: #000;
            margin: 0;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
        }
        .card {
            background-color: #fff;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 0 10px rgba(0,0,0,0.3);
            text-align: center;
        }
        h1 {
            font-size: 1.8rem;
            color: #222;
            margin-bottom: 10px;
        }
        .block-name {
            font-weight: bold;
            margin-bottom: 25px;
            color: #333;
        }
        .status {
            font-size: 32px;
            margin-bottom: 20px;
            color: {$successColor};
        }
        .button-group {
            margin-top: 10px;
            display: flex;
            justify-content: center;
            gap: 15px;
        }
        .button-link {
            background-color: #333;
            color: #fff;
            text-decoration: none;
            padding: 10px 20px;
            border-radius: 6px;
            font-size: 14px;
            font-weight: bold;
        }
        .button-link:hover {
            background-color: #111;
        }
    </style>
</head>
<body>
    <div class="card">
        <h1>{$message}</h1>
        <div class="block-name">{$blockName}</div>
        <div class="status">{$statusSymbol}</div>
HTML
. ($success ? "
// If the operation succeeded, we render two action buttons:
// 1) A direct download link to the freshly stored CSV file, with the HTML5 'download' attribute
//    suggesting a download instead of navigation.
// 2) A link to 'DownDataVR.html' which might serve as an index or help page (implementation-specific).
        <div class='button-group'>
            <a class='button-link' href='{$downloadLink}' download>Download File</a>
            <a class='button-link' href='DownDataVR.html'>Download Page</a>
        </div>
" : "") .
<<<HTML
    </div>
</body>
</html>
HTML;
}
?>
