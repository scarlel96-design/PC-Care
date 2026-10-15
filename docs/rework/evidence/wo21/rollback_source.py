from pathlib import Path
import hashlib,sys
base=Path(__file__).resolve().parent
if len(sys.argv)!=2: raise SystemExit("Usage: ROLLBACK.sh ABSOLUTE_RESTORE_COPY")
target=Path(sys.argv[1])
if not target.is_absolute() or target.name!="RESTORE_COPY" or not target.parent.name.startswith("wo21-rollback-"): raise SystemExit("Independent source copy required")
for path in [target,*target.parents]:
 if path.is_symlink() or (hasattr(path,"is_junction") and path.is_junction()): raise SystemExit("Reparse boundary rejected")
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
if sha(base/"ORIGINAL_FILE")!="d38efc85a7c93975e916a3350fa64c85da191739492d73b2d74b57a864f73881" or sha(target)!="8995c38b9a7c94cc1d5302075dea2d530401b45d2a49e0322af5c318398cc0c3": raise SystemExit("Source hash mismatch")
target.write_bytes((base/"ORIGINAL_FILE").read_bytes())
if sha(target)!="d38efc85a7c93975e916a3350fa64c85da191739492d73b2d74b57a864f73881": raise SystemExit("Restore verification failed")
print("SOURCE_COPY_RESTORED; INSTALLATION_UNCHANGED")
