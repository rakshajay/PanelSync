//[09/15/2025]:Raksha- Export ONLY Lines/Polylines/Circles via IGES (array export, no selection needed)

function log(m) { try { print(m); } catch (_) { } }
function j(o) { try { return JSON.stringify(o); } catch (_) { return String(o); } }

// ---------- Params (override via --ScriptParam) ----------
var project = (typeof project !== "undefined" && project) || "C:/Users/raksh/Downloads/Expe1.3dr";
var out = (typeof out !== "undefined" && out) || "C:/Temp/geom_LCP.igs"; // IGES output
var groupPath = (typeof groupPath !== "undefined" && groupPath) || ""; // e.g. "/Geometric Group" or "" for whole project
// ---------------------------------------------------------

function ensureArray(v) { return (v && v.length) ? v : []; }
function pathOf(x) { try { return x.GetPath ? x.GetPath() : ""; } catch (_) { return ""; } }
function isUnder(path, base) {
    if (!base) return true;
    if (!path) return false;
    return path.indexOf(base) >= 0;
}

try {
    log("//[09/15/2025]:Raksha- OpenDoc -> " + project);
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
        var s = obj.toString(); // e.g. "SFeature(Circle)" / "SFeature(Line)"
        if (s.indexOf("Circle") >= 0 || s.indexOf("Line") >= 0 || s.indexOf("Polyline") >= 0 || s.indexOf("Multiline") >= 0 || s.indexOf("Plane") >= 0) {
            seen.add(obj);
            picked.push(obj);
        }
    }

    for (var i = 0; i < feats.length; i++) consider(feats[i]);
    for (var k = 0; k < comps.length; k++) consider(comps[k]);

    // Debug summary
    var counts = {};
    for (var z = 0; z < picked.length; z++) {
        var name = picked[z].toString();
        counts[name] = (counts[name] || 0) + 1;
    }
    log("//[09/15/2025]:Raksha- Scope: " + (groupPath ? ("under '" + groupPath + "'") : "whole project"));
    log("//[09/15/2025]:Raksha- Picked count=" + picked.length);
    for (var key in counts) log("  " + key + " -> " + counts[key]);

    if (picked.length === 0)
        throw new Error("No Lines/Polylines/Circles matched. Check object names in listing.");

    // Convert to CAD shapes
    var shapes = [];
    for (var z = 0; z < picked.length; z++) {
        var conv = SCADUtil.Convert(picked[z]);
        if (conv && conv.ErrorCode === 0 && conv.Shape) {
            shapes.push(conv.Shape);
        } else {
            log("//[09/15/2025]:Raksha- Skipped " + picked[z].toString() + " (convert error)");
        }
    }

    if (shapes.length === 0)
        throw new Error("No convertible shapes for IGES export.");

    // Export using SCADUtil
    var mtx = new SMatrix();
    log("//[09/15/2025]:Raksha- Export IGES -> " + out);
    var rc = SCADUtil.Export(out, shapes, mtx);
    if (!rc || typeof rc.ErrorCode === "undefined" || rc.ErrorCode !== 0)
        throw new Error("SCADUtil.Export failed: " + j(rc));

    log("//[09/15/2025]:Raksha- IGES export complete -> " + out);
}
catch (err) {
    log("//[09/15/2025]:Raksha- ERROR: " + err);
    throw err; //[09/15/2025]:Raksha- ensure non-zero exit for C#
}
