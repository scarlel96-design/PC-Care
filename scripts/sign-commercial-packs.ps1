param(
    [string]$PackDir = "",
    [string]$Version = "50.0.0",
    [string]$PrivateKeyPath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $PackDir) {
    $PackDir = Join-Path $ProjectRoot "content\data\commercial"
}

if (-not $PrivateKeyPath) {
    $PrivateKeyPath = $env:AEGIS_SIGNING_KEY_PATH
}
if (-not $PrivateKeyPath) {
    $PrivateKeyPath = Join-Path $ProjectRoot "artifacts\signing\aegis-dev-private.pem"
}
if (-not (Test-Path $PrivateKeyPath)) {
    $testKey = Join-Path $ProjectRoot "tests\SmartPerformanceDoctor.Tests\TestAssets\aegis-test-private.pem"
    if (Test-Path $testKey) { $PrivateKeyPath = $testKey }
}

if (-not (Test-Path $PrivateKeyPath)) {
    Write-Host "[SKIP] Pack signing key not found." -ForegroundColor Yellow
    return
}

Add-Type -AssemblyName System.Security
if (-not ("PackCanonicalChecksum" -as [type])) {
    $jsonPath = $null
    $runtimeDir = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeDirectory
    if ($runtimeDir) {
        $candidate = Join-Path $runtimeDir "System.Text.Json.dll"
        if (Test-Path $candidate) { $jsonPath = $candidate }
    }
    if (-not $jsonPath) {
        $hostMajor = [Environment]::Version.Major
        $jsonPath = Get-ChildItem (Join-Path $env:ProgramFiles "dotnet\shared\Microsoft.NETCore.App") -Recurse -Filter System.Text.Json.dll -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Directory.Name -match '^\d+\.\d+' -and
                ([version]$_.Directory.Name).Major -eq $hostMajor
            } |
            Sort-Object { [version]$_.Directory.Name } -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $jsonPath -or -not (Test-Path $jsonPath)) {
        throw "System.Text.Json.dll not found under dotnet shared runtime."
    }
    Add-Type -Path $jsonPath
    Add-Type @"
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

public static class PackCanonicalChecksum
{
    public static string ComputeFromFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalObject(writer, doc.RootElement, omitChecksum: true);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonicalObject(Utf8JsonWriter writer, JsonElement element, bool omitChecksum)
    {
        writer.WriteStartObject();
        foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (omitChecksum && prop.NameEquals("checksum"))
            {
                writer.WriteString("checksum", "");
                continue;
            }
            writer.WritePropertyName(prop.Name);
            WriteValue(writer, prop.Value);
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteCanonicalObject(writer, value, omitChecksum: false);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
"@
}

function Sign-ChecksumHex([string]$checksumHex, [string]$keyPath) {
    $pem = Get-Content $keyPath -Raw
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
    $ecdsa.ImportFromPem($pem)
    $hash = [System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($checksumHex))
    $sig = $ecdsa.SignHash($hash)
    return [Convert]::ToBase64String($sig)
}

function Get-PackChecksumFromFile([string]$path) {
    return [PackCanonicalChecksum]::ComputeFromFile($path)
}

foreach ($packName in @("rules.pack.json", "protocols.pack.json")) {
    $packPath = Join-Path $PackDir $packName
    if (-not (Test-Path $packPath)) {
        Write-Host "[SKIP] $packName not found" -ForegroundColor Yellow
        continue
    }

    $checksum = Get-PackChecksumFromFile $packPath
    $obj = Get-Content $packPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $obj.checksum = $checksum
    $obj.version = $Version
    if ($obj.PSObject.Properties.Name -contains "packVersion") {
        $obj.packVersion = $Version
    }
    if ($obj.PSObject.Properties.Name -contains "productVersion") {
        $obj.productVersion = $Version
    }
    ($obj | ConvertTo-Json -Compress -Depth 30) | Set-Content $packPath -Encoding UTF8 -NoNewline

    $signature = Sign-ChecksumHex $checksum $PrivateKeyPath
    $sigObj = [PSCustomObject]@{
        packFile = $packName
        packVersion = $Version
        productVersion = $Version
        algorithm = "SHA-256"
        checksum = $checksum
        signatureAlgorithm = "ECDSA-P256-SHA256"
        signature = $signature
        signedAt = (Get-Date).ToString("o")
    }
    $sigPath = "$packPath.sig"
    ($sigObj | ConvertTo-Json -Compress) | Set-Content $sigPath -Encoding UTF8
    Write-Host "[OK] Signed $packName ($($checksum.Substring(0,12))...)" -ForegroundColor Green
}