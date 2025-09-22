//[09/22/2025]:Raksha- JobWatcherScript.js (runs inside 3DR session)

// ===== CONFIG =====
var hotfolder = "C:/Users/raksh/Desktop/PanelSyncHot/Inventor/exports/obj";
var project = (typeof project !== "undefined" && project) || "";
var seen = {}; // remember already imported files

function log(m) { try { print(m); } catch (_) { } }

function stableSleep(ms) {
    var end = new Date().getTime() + ms;
    while (new Date().getTime() < end) { /* busy sleep */ }
}

// Try to import an OBJ file
function tryImport(objPath) {
    try {
        log("Detected OBJ: " + objPath);
        var rc = SPoly.FromFile(objPath);
        if (!rc || rc.ErrorCode !== 0) {
            log("SPoly.FromFile failed: " + JSON.stringify(rc));
            return;
        }
        var mesh = rc.PolyTbl[0];
        mesh.AddToDoc();
        log("Added mesh: " + mesh.GetName());

        try { var vs = SViewSet.New(true); vs.Update(true); } catch (_) { }
        SaveDoc(project, true);
        log("Saved project after import: " + project);
    } catch (e) {
        log("Import error: " + e.message);
    }
}

// ===== Main watcher loop =====
log("JobWatcher armed on: " + hotfolder);
while (true) {
    try {
        var files = SFile.ListDir(hotfolder, "*.obj");
        for (var i = 0; i < files.length; i++) {
            var f = files[i];
            if (!seen[f]) {
                seen[f] = true;
                tryImport(f);
            }
        }
    } catch (e) { log("Watcher error: " + e.message); }

    stableSleep(3000); // check every 3 seconds
}
