//[09/17/2025]:Raksha- OBJ import script

function log(m) { try { print(m); } catch (_) { } }
function j(o) { try { return JSON.stringify(o); } catch (_) { return String(o); } }

var project = (typeof project !== "undefined" && project) || "";
var obj = (typeof obj !== "undefined" && obj) || "";

try {
    log("// OpenDoc -> " + project);
    var rcOpen = OpenDoc(project);
    if (!rcOpen || rcOpen.ErrorCode !== 0) throw new Error("OpenDoc failed: " + j(rcOpen));

    log("// Import OBJ -> " + obj);
    var rc = SCADUtil.Import(obj);
    if (!rc || rc.ErrorCode !== 0) throw new Error("Import failed: " + j(rc));

    log("// OBJ import complete -> " + obj);
}
catch (err) {
    log("// ERROR: " + err);
    throw err;
}
