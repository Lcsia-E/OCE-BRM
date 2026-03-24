<?php
/*
 * Script: combine_csv_by_base_id.php
 * Purpose: Given a base identifier via GET (?base_id=...), find and combine multiple CSV files
 *          located under a "Data" directory whose filenames follow the pattern:
 *          {BASE_ID}_P{N}.csv (e.g., ABC_P1.csv, ABC_P2.csv, ...).
 *          It concatenates them in ascending numeric order of {N}, keeping the header from the
 *          first file only, and returns a single combined CSV as a downloadable response.
 *
 * Input (HTTP GET):
 *   - base_id: A string used to identify the set of CSV files to combine. Only A–Z, 0–9, and underscore
 *              are allowed after sanitization; anything else is removed. The value is uppercased.
 *
 * File naming contract:
 *   - Files are expected at: {SCRIPT_DIR}/Data/{BASE_ID}_P{NUMBER}.csv
 *     Example: If base_id = "abc", the script looks for: /Data/ABC_P*.csv
 *
 * Output (HTTP response):
 *   - Content-Type: text/csv
 *   - Content-Disposition: attachment; filename={BASE_ID}_COMBINED.csv
 *   - Body: Combined CSV. Header row is taken from the first file in order; for subsequent files,
 *           the header is skipped to avoid duplication.
 *
 * Error handling:
 *   - 400 Bad Request if base_id is missing.
 *   - 404 Not Found if no matching CSV files are found for the sanitized base_id.
 *
 * Notable implementation details:
 *   - Sanitization: The base_id is uppercased and filtered to A–Z, 0–9, and underscore to mitigate
 *     path traversal and glob injection risks.
 *   - Ordering: Files are sorted by the numeric suffix captured by regex "_P(\d+)\.csv$".
 *   - Newlines: FILE_IGNORE_NEW_LINES suppresses native newline retention; the script re-adds a single
 *     "\n" after rtrim() to normalize line endings across files (avoids double or missing newlines).
 *   - Performance: This approach reads all files into memory and builds one large string ($combined).
 *     It is straightforward but may be memory-heavy for very large datasets; a streaming approach
 *     would be more memory-efficient if needed (not implemented here to keep behavior unchanged).
 */

// Absolute path to the "Data" directory based on the directory of this script.
// Using __DIR__ ensures paths are resolved relative to the script location, not the current working directory.
$dataDir = __DIR__ . "/Data/";

// Validate presence of the required GET parameter 'base_id'.
// If it's missing, respond with HTTP 400 and stop execution.
if (!isset($_GET['base_id'])) {
    http_response_code(400);
    echo "Missing base_id parameter.";
    exit;
}

// Sanitize and normalize the base ID:
// 1) strtoupper: forces uppercase so the file lookup is case-consistent.
// 2) preg_replace: remove any character not in the whitelist [A–Z, 0–9, _] to prevent injection and invalid paths.
$baseId = preg_replace("/[^A-Z0-9_]/", "", strtoupper($_GET['base_id']));

// Build a glob pattern like: /path/to/Data/ABC_P*.csv
// This will match ABC_P1.csv, ABC_P2.csv, etc.
$pattern = $dataDir . $baseId . "_P*.csv";

// Resolve files matching the pattern. glob() returns an array of paths or false on error.
$files = glob($pattern);

// If no files are found, return 404 to signal the requested resource is not available for that base ID.
if (!$files || count($files) === 0) {
    http_response_code(404);
    echo "No files found for base ID: $baseId";
    exit;
}

// Sort files by the numeric portion after "_P" to ensure deterministic ascending order
// (e.g., P1, P2, P10 rather than lexicographic P1, P10, P2).
// The comparator extracts the trailing number with a regex and compares integers.
usort($files, function ($a, $b) {
    // Extract numeric run index from filenames like "..._P12.csv"
    preg_match("/_P(\d+)\.csv$/", $a, $matchA);
    preg_match("/_P(\d+)\.csv$/", $b, $matchB);

    // intval() converts the captured strings to integers for proper numeric comparison.
    return intval($matchA[1]) - intval($matchB[1]);
});

// Initialize an accumulator for the combined CSV content.
// Note: This builds a single string in memory, which is simple but not streaming.
$combined = "";

// Iterate over each matched file in the sorted order.
foreach ($files as $index => $file) {
    // Read the file into an array of lines.
    // FILE_IGNORE_NEW_LINES tells file() to strip the trailing newline from each line it reads.
    // We will re-append a single "\n" manually to normalize line endings.
    $lines = file($file, FILE_IGNORE_NEW_LINES); // preserve trailing \n manually

    // If reading failed or the file is empty, skip to the next file.
    if ($lines === false || count($lines) === 0) continue;

    if ($index === 0) {
        // For the very first file:
        // - Include everything (header + data).
        // - rtrim() removes any trailing whitespace, including \r on Windows line endings.
        // - Then we explicitly add "\n" to ensure a single newline terminator per line.
        foreach ($lines as $line) {
            $combined .= rtrim($line) . "\n";
        }
    } else {
        // For subsequent files:
        // - Skip the header line (assumed to be the first line).
        // - Append only data rows to avoid duplicate headers in the combined output.
        foreach (array_slice($lines, 1) as $line) {
            $combined .= rtrim($line) . "\n";
        }
    }
}

// Prepare HTTP response headers to send the combined CSV back to the client as a download.
// Content-Type indicates CSV payload; Content-Disposition suggests a filename to the browser.
header("Content-Type: text/csv");
header("Content-Disposition: attachment; filename={$baseId}_COMBINED.csv");

// Output the combined CSV content and terminate the script.
echo $combined;
exit;
?>
