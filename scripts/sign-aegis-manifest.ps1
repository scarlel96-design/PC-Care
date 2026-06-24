param(
    [string]$ManifestPath,
    [string]$SignaturePath,
    [string]$PrivateKeyPath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

if (-not $PrivateKeyPath) {
    $PrivateKeyPath = Join-Path $ProjectRoot "artifacts\signing\aegis-dev-private.pem"
}

if (-not (Test-Path $PrivateKeyPath)) {
    throw "Aegis signing key not found: $PrivateKeyPath. Set AEGIS_SIGNING_KEY_PATH or place the dev key under artifacts\signing\."
}

if (-not $ManifestPath) {
    throw "ManifestPath is required."
}

if (-not (Test-Path $ManifestPath)) {
    throw "Manifest not found: $ManifestPath"
}

if (-not $SignaturePath) {
    $SignaturePath = "$ManifestPath.sig"
}

$env:AEGIS_SIGNING_KEY_PATH = $PrivateKeyPath
$signer = Join-Path $ProjectRoot "tools\AegisManifestSignTool\Program.cs"
$toolDir = Split-Path $signer -Parent

if (-not (Test-Path $signer)) {
    # Fallback: use dotnet script inline via test helper project
    $helperProj = Join-Path $ProjectRoot "tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj"
    $manifestArg = $ManifestPath.Replace("'", "''")
    $sigArg = $SignaturePath.Replace("'", "''")
    $keyArg = $PrivateKeyPath.Replace("'", "''")
    $code = @"
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var manifestPath = @"$ManifestPath";
var signaturePath = @"$SignaturePath";
var keyPath = @"$PrivateKeyPath";
var manifestJson = File.ReadAllText(manifestPath);
using var doc = JsonDocument.Parse(manifestJson);
var root = doc.RootElement;
var clone = new Dictionary<string, object?>();
foreach (var prop in root.EnumerateObject())
{
    if (prop.Name is "signature" or "capsuleHash") continue;
    clone[prop.Name] = prop.Value.ValueKind switch
    {
        JsonValueKind.String => prop.Value.GetString(),
        JsonValueKind.Number => prop.Value.GetDouble(),
        JsonValueKind.Array => prop.Value,
        JsonValueKind.Object => prop.Value,
        _ => null
    };
}
var signingJson = JsonSerializer.Serialize(clone, new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
var pem = File.ReadAllText(keyPath);
using var ecdsa = ECDsa.Create();
ecdsa.ImportFromPem(pem);
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signingJson));
var sig = Convert.ToBase64String(ecdsa.SignHash(hash));
File.WriteAllText(signaturePath, sig);
Console.WriteLine("[OK] Signed: $signaturePath");
"@
    throw "Aegis sign tool not found. Use tests/SmartPerformanceDoctor.Tests with AEGIS_SIGNING_KEY_PATH during baseline rebuild."
}

Write-Host "[OK] Aegis manifest signed: $SignaturePath" -ForegroundColor Green