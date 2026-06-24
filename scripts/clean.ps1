Get-ChildItem -Recurse -Directory -Include bin,obj,target | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path ".\artifacts") {
    Remove-Item ".\artifacts" -Recurse -Force
}
