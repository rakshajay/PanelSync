// Auto-generated script

var objPath = "C:/Users/raksh/OneDrive/Desktop/PanelSyncHot/Inventor/exports/obj/CHECK2_P001_rA.obj";

function log(m) { try { print(m); } catch (_) { } }

if (!objPath) { throw "objPath is missing!"; }

log("Importing OBJ: " + objPath);
var rc = SPoly.FromFile(objPath);
if (!rc || rc.ErrorCode !== 0) { throw "SPoly.FromFile failed: " + JSON.stringify(rc); }
if (!rc.PolyTbl || rc.PolyTbl.length === 0) { throw "No meshes found in OBJ"; }

for (var i = 0; i < rc.PolyTbl.length; i++) {
    var mesh = rc.PolyTbl[i];
    mesh.AddToDoc();
    log("Added mesh: " + mesh.GetName() + " (" + i + ")");
}

try { var vs = SViewSet.New(true); vs.Update(true); } catch(_) {}
SaveDoc("", true);
log("Saved project after import.");
