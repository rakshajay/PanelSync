//[09/17/2025]:Raksha- IGES export: ExportAll vs Visible-only (Manual default)

function log(m) { try { print(m); } catch (_) { } }
function j(o) { try { return JSON.stringify(o); } catch (_) { return String(o); } }

// ---------- Params (override via --ScriptParam) ----------
var project = (typeof project !== "undefined" && project) || "C:/Users/raksh/Downloads/Expe1.3dr";
var out = (typeof out !== "undefined" && out) || "C:/Temp/geom_LCP.igs";
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
    log("//[09/17/2025]:Raksha- OpenDoc -> " + project);
    var rcOpen = OpenDoc(project);
    if (!rcOpen || typeof rcOpen.ErrorCode === "undefined" || rcOpen.ErrorCode !== 0)
        throw new Error("OpenDoc failed: " + j(rcOpen));

    // Get comps according to mode
    var comps;
    if (exportAll) {
        comps = ensureArray(SComp.All(SComp.ANY_VISIBILITY)); //[09/17/2025]:Raksha- All objects
    } else {
        comps = ensureArray(SComp.All(SComp.VISIBLE_ONLY));   //[09/17/2025]:Raksha- Visible only
    }

    var picked = [];
    var seen = new WeakSet();

    function consider(obj) {
        if (!obj || seen.has(obj)) return;
        var s = obj.toString();
        if (isWantedString(s)) {
            seen.add(obj);
            picked.push(obj);
        }
    }

    for (var k = 0; k < comps.length; k++) consider(comps[k]);

    // Debug summary
    var counts = {};
    for (var z = 0; z < picked.length; z++) {
        var name = picked[z].toString();
        counts[name] = (counts[name] || 0) + 1;
    }
    log("//[09/17/2025]:Raksha- Picked count=" + picked.length + " (mode=" + (exportAll ? "ExportAll" : "VisibleOnly") + ")");
    for (var key in counts) log("  " + key + " -> " + counts[key]);

    if (picked.length === 0)
        throw new Error("No objects matched filter.");

    // Convert to CAD shapes
    var shapes = [];
    for (var q = 0; q < picked.length; q++) {
        var conv = SCADUtil.Convert(picked[q]);
        if (conv && conv.ErrorCode === 0 && conv.Shape) {
            shapes.push(conv.Shape);
        } else {
            log("//[09/17/2025]:Raksha- Skipped " + picked[q].toString() + " (convert error)");
        }
    }

    if (shapes.length === 0)
        throw new Error("No convertible shapes for IGES export.");

    //[09/22/2025]:Raksha- Force IGES global units to mm
    // This is another way to achieve the same result.
    var scalingFactor = 0.001;

    // Create a new SMatrix object.
    var mtx = SMatrix.New();

    // Initialize it with a uniform scale.
    mtx.InitScale(SPoint.New(0, 0, 0), scalingFactor, scalingFactor, scalingFactor);
    log("//[09/17/2025]:Raksha- Export IGES -> " + out);
    var rc = SCADUtil.Export(out, shapes, mtx);
    if (!rc || typeof rc.ErrorCode === "undefined" || rc.ErrorCode !== 0)
        throw new Error("SCADUtil.Export failed: " + j(rc));

    log("//[09/17/2025]:Raksha- IGES export complete -> " + out);
}
catch (err) {
    log("//[09/17/2025]:Raksha- ERROR: " + err);
    throw err;
}
