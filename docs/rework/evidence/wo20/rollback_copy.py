import hashlib,pathlib,sys
e=pathlib.Path(__file__).resolve().parent
t=pathlib.Path(sys.argv[1]).resolve()
assert t.name=='RESTORE_COPY' and t.parent.name.startswith('wo20-rollback-'), 'IndependentCopyRequired'
assert t.is_file() and not t.is_symlink(), 'RegularCopyRequired'
original=e/'ORIGINAL_FILE'; modified=e/'MODIFIED_FILE'
assert hashlib.sha256(original.read_bytes()).hexdigest()=='f93f7c73fb1872360f04acd1810dcaac161e4ea3b2680be709933e2e5bac7c63'
assert hashlib.sha256(t.read_bytes()).digest()==hashlib.sha256(modified.read_bytes()).digest(), 'ModifiedHashRequired'
t.write_bytes(original.read_bytes())
assert t.read_bytes()==original.read_bytes()
print('SOURCE_COPY_RESTORED; ORIGINAL_SHA256='+hashlib.sha256(t.read_bytes()).hexdigest())
