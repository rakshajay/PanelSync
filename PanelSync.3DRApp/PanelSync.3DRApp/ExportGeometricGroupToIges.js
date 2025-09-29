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
        var obj = picked[q];
        var s = obj.toString();

        try {
            if (s.indexOf("Multiline") >= 0) {
                // Expand multiline into segments
                var n = (obj.GetNumber ? obj.GetNumber() : 0);
                if (n >= 2) {
                    for (var i = 0; i < n - 1; i++) {
                        var p1 = obj.GetPoint(i);
                        var p2 = obj.GetPoint(i + 1);
                        var line = SLine.New(p1, p2);
                        var convLine = SCADUtil.Convert(line);
                        if (convLine && convLine.ErrorCode === 0 && convLine.Shape) {
                            shapes.push(convLine.Shape);
                            log("   Added multiline segment " + i);
                        } else {
                            log("   Failed to convert multiline segment " + i);
                        }
                    }
                    // close loop if flagged
                    if (obj.IsClosed && obj.IsClosed()) {
                        var p1 = obj.GetPoint(n - 1);
                        var p2 = obj.GetPoint(0);
                        var line = SLine.New(p1, p2);
                        var convLine = SCADUtil.Convert(line);
                        if (convLine && convLine.ErrorCode === 0 && convLine.Shape) {
                            shapes.push(convLine.Shape);
                            log("   Added closing segment for multiline.");
                        }
                    }
                } else {
                    log("   Multiline has insufficient points.");
                }
            } else {
                // Normal conversion
                var conv = SCADUtil.Convert(obj);
                if (conv && conv.ErrorCode === 0 && conv.Shape) {
                    shapes.push(conv.Shape);
                } else {
                    log("Skipped " + s + " (convert error)");
                }
            }
        } catch (ex) {
            log("Exception converting " + s + ": " + ex);
        }
    }

    if (shapes.length === 0) throw new Error("No convertible shapes for IGES export.");

    var scalingFactor = 1;
    var mtx = SMatrix.New();
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
