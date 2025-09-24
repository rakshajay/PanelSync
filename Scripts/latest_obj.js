//[09/22/2025]:Raksha- Auto-generated script

var objPath = "C:/Users/raksh/OneDrive/Desktop/PanelSyncHot/Inventor/exports/obj/Expe1_P001_rA.obj";

function log(m) { try { print(m); } catch (_) { } }

if (!objPath) { throw "objPath is missing!"; }

log("Importing OBJ: " + objPath);
var rc = SPoly.FromFile(objPath);
if (!rc || rc.ErrorCode !== 0) { throw "SPoly.FromFile failed: " + JSON.stringify(rc); }
var mesh = rc.PolyTbl[0];
mesh.AddToDoc();
log("Added mesh: " + mesh.GetName());
try { var vs = SViewSet.New(true); vs.Update(true); } catch(_) {}
SaveDoc("", true);
log("Saved project after import.");
