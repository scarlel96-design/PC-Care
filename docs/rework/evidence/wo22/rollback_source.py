from pathlib import Path
import sys,hashlib
base=Path(__file__).resolve().parent
if len(sys.argv)!=2:raise SystemExit("Usage: ROLLBACK.sh ABSOLUTE_RESTORE_COPY")
p=Path(sys.argv[1])
if not p.is_absolute() or p.name!="RESTORE_COPY" or not p.parent.name.startswith("wo22-rollback-"):raise SystemExit("Independent source copy required")
for x in [p,*p.parents]:
 if x.is_symlink() or (hasattr(x,"is_junction") and x.is_junction()):raise SystemExit("Reparse boundary rejected")
if p.stat().st_nlink!=1:raise SystemExit("Hardlink rejected")
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
if sha(base/"ORIGINAL_FILE")!="119112b2efa631f5c20262a6d049627405dbbe178540aaea51b1e2140279a1fe" or sha(p)!="b124f2d2c9e6982323bc6a44ae298438b1f9865e30881f37a16cc45b717c7a34":raise SystemExit("Source hash mismatch")
p.write_bytes((base/"ORIGINAL_FILE").read_bytes())
if sha(p)!="119112b2efa631f5c20262a6d049627405dbbe178540aaea51b1e2140279a1fe":raise SystemExit("Source restore failed")
print("SOURCE_COPY_RESTORED; INSTALLATION_UNCHANGED")
