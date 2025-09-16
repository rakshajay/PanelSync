//[09/14/2025]:Raksha- Export Lines/Circles/Rectangles/Polylines/Planes to IGES

function log(m) { try { print(m); } catch (_) { } }
function j(o) { try { return JSON.stringify(o); } catch (_) { return String(o); } }
for (var key in this) {
    try {
        if (typeof this[key] === "object" || typeof this[key] === "function") {
            log("Available API: " + key);
        }
    } catch (e) { }
}
// ---------- Params (override via --ScriptParam) ----------
var project = (typeof project !== "undefined" && project) || "C:/Users/raksh/Downloads/Expe1.3dr";
var out = (typeof out !== "undefined" && out) || "C:/Temp/geom_out.igs";
var groupPath = (typeof groupPath !== "undefined" && groupPath) || ""; // e.g. "/Geometric Group"
// ---------------------------------------------------------

//[09/14/2025]:Raksha- Normalize Windows paths to forward slashes
function normPath(p) { return (p || "").replace(/\\/g, "/"); }
project = normPath(project);
out = normPath(out);
groupPath = groupPath || "";

//[09/14/2025]:Raksha- Entity types we want
var WANT = {
    "SLINE": 1,
    "SCIRCLE": 1,
    "SMULTILINE": 1,
    "SPOLYLINE": 1,
    "POLYLINE": 1,
    "SRECTANGLE": 1,
    "SPLANE": 1
};

function ensureArray(v) { return (v && v.length) ? v : []; }
function safe(fn, d) { try { return fn(); } catch (_) { return d; } }
function typeOf(x) {
    var t = safe(function () { return x.GetTypeName && x.GetTypeName(); }, null);
    if (t) return String(t);
    if (x && x.constructor && x.constructor.name) return String(x.constructor.name);
    return "Unknown";
}
function pathOf(x) { return safe(function () { return x.GetPath && x.GetPath(); }, ""); }
function isUnder(path, base) {
    if (!base) return true; // whole project
    if (!path || typeof path !== "string") return false;
    if (path === base) return true;
    var b = base.endsWith("/") ? base : (base + "/");
    return path.indexOf(b) === 0;
}

try {
    log("//[09/14/2025]:Raksha- OpenDoc -> " + project);
    var rcOpen = OpenDoc(project);
    if (!rcOpen || typeof rcOpen.ErrorCode === "undefined" || rcOpen.ErrorCode !== 0)
        throw new Error("OpenDoc failed: " + j(rcOpen));

    var feats = ensureArray(SFeature.All());
    var comps = ensureArray(SComp.All());

    var picked = [];
    var seen = new WeakSet();

    function consider(obj) {
        if (!obj || seen.has(obj)) return;
        var p = pathOf(obj);
        if (!isUnder(p, groupPath)) return;
        var tU = typeOf(obj).toUpperCase();
        if (WANT[tU]) { seen.add(obj); picked.push(obj); }
    }

    for (var i = 0; i < feats.length; i++) consider(feats[i]);
    for (var k = 0; k < comps.length; k++) consider(comps[k]);

    // Debug summary
    var counts = {};
    for (var z = 0; z < picked.length; z++) {
        var tt = typeOf(picked[z]);
        counts[tt] = (counts[tt] || 0) + 1;
    }
    log("//[09/14/2025]:Raksha- Scope: " + (groupPath ? ("under '" + groupPath + "'") : "whole project"));
    log("//[09/14/2025]:Raksha- Picked count=" + picked.length);
    for (var key in counts) log("  " + key + " -> " + counts[key]);

    if (picked.length === 0)
        throw new Error("No matching entities found (Line/Circle/Rectangle/Plane).");

    // Export using IGES
    log("//[09/14/2025]:Raksha- ExportProject(IGES) -> " + out);
    var rc = SExchangeFormat.ExportProject(out, picked, "IGES");
    if (!rc || typeof rc.ErrorCode === "undefined" || rc.ErrorCode !== 0)
        throw new Error("IGES ExportProject failed: " + j(rc));

    log("//[09/14/2025]:Raksha- IGES export complete -> " + out);
}
catch (err) {
    log("//[09/14/2025]:Raksha- ERROR: " + err);
    throw err;
}
