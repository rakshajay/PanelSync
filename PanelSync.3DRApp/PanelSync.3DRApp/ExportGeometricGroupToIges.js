//[09/23/2025]:Raksha- IGES export with two modes: ExportAll vs Visible-only (Manual default)

function log(m) { try { print(m); } catch (_) { } }
function j(o) { try { return JSON.stringify(o); } catch (_) { return String(o); } }

// ---------- Params (override via --ScriptParam) ----------
// Supply via: --ScriptParam="project='C:/x.3dr'; out='C:/x.igs'; exportAll=1;"
var project = (typeof project !== "undefined" && project) || "";
var out = (typeof out !== "undefined" && out) || "";
var exportAll = (typeof exportAll !== "undefined" && (exportAll === 1 || exportAll === "1" || exportAll === true));
// ---------------------------------------------------------

function ensureArray(v) { return (v && v.length) ? v : []; }
function isWantedString(s) {
    return s.indexOf("Circle") >= 0 ||
        s.indexOf("Line") >= 0 ||
        s.indexOf("Polyline") >= 0 ||
        s.indexOf("Multiline") >= 0 ||
        s.indexOf("Plane") >= 0 ||
        s.indexOf("Rectangle") >= 0;
}

try {
    if (!project || !out) throw new Error("Missing 'project' or 'out' param.");

    log("OpenDoc -> " + project);
    var rcOpen = OpenDoc(project);
    if (!rcOpen || typeof rcOpen.ErrorCode === "undefined" || rcOpen.ErrorCode !== 0)
        throw new Error("OpenDoc failed: " + j(rcOpen));

    // Mode selection
    var comps = exportAll ? ensureArray(SComp.All(SComp.ANY_VISIBILITY))
        : ensureArray(SComp.All(SComp.VISIBLE_ONLY));

    var picked = [];
    var seen = new WeakSet();
    function consider(obj) {
        if (!obj || seen.has(obj)) return;
        var s = obj.toString();
        if (isWantedString(s)) { seen.add(obj); picked.push(obj); }
    }
    for (var k = 0; k < comps.length; k++) consider(comps[k]);

    var counts = {};
    for (var z = 0; z < picked.length; z++) {
        var name = picked[z].toString();
        counts[name] = (counts[name] || 0) + 1;
    }
    log("Picked count=" + picked.length + " (mode=" + (exportAll ? "ExportAll" : "VisibleOnly") + ")");
    for (var key in counts) log("  " + key + " -> " + counts[key]);
    if (picked.length === 0) throw new Error("No objects matched filter.");

    // Convert to CAD shapes
    var shapes = [];
    for (var q = 0; q < picked.length; q++) {
        var conv = SCADUtil.Convert(picked[q]);
        if (conv && conv.ErrorCode === 0 && conv.Shape) shapes.push(conv.Shape);
        else log("Skipped " + picked[q].toString() + " (convert error)");
    }
    if (shapes.length === 0) throw new Error("No convertible shapes for IGES export.");

    // Units handling: apply a matrix if needed (keep set to identity now)
    var scalingFactor = 1;
    // Create a new SMatrix object.
    var mtx = SMatrix.New();
    // Initialize it with a uniform scale.
    mtx.InitScale(SPoint.New(0, 0, 0), scalingFactor, scalingFactor, scalingFactor);

    log("Export IGES -> " + out);
    var rc = SCADUtil.Export(out, shapes, mtx);
    if (!rc || typeof rc.ErrorCode === "undefined" || rc.ErrorCode !== 0)
        throw new Error("SCADUtil.Export failed: " + j(rc));

    log("IGES export complete -> " + out);
}
catch (err) {
    log("ERROR: " + err);
    throw err;
}
