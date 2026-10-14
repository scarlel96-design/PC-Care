using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public sealed record ProtectedStorageRoot(string Path, string Identity, string Kind);

/// <summary>Protect registered paths even when their current identity changes.</summary>
public sealed class StorageProtectionRegistry(string store)
{
    private static readonly byte[] Entropy = "PC-Care-Protected-Stores-v1"u8.ToArray();
    public static string DefaultStore => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PC-Care","protection");

    public void Register(string root, string kind)
    {
        if (kind is not ("Vault" or "Recovery" or "Anchor" or "Audit" or "Backup" or "Keys" or "Updater" or "User"))
            throw new ArgumentException("UnknownProtectedStoreKind");
        using var pin=WindowsFileBoundary.PinDirectory(root);
        SecureDeleteAuditChain.PrepareDirectory(store);
        var initialized=File.Exists(System.IO.Path.Combine(store,"registry.lock")) || File.Exists(System.IO.Path.Combine(store,"stores.dpapi"));
        using var registrationLock=WindowsFileBoundary.OpenExclusive(store,"registry.lock",!File.Exists(System.IO.Path.Combine(store,"registry.lock")));
        var entries=ReadCore(!initialized).Where(e=>!string.Equals(e.Path,pin.Root,StringComparison.OrdinalIgnoreCase)).ToList();
        if (entries.Count>=128 || pin.Root.Length>4096) throw new InvalidDataException("ProtectionRegistryBudget");
        entries.Add(new(pin.Root,pin.Identity,kind));
        var data=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(entries),Entropy,DataProtectionScope.CurrentUser);
        var temporary="protected-"+Guid.NewGuid().ToString("N")+".tmp";
        using var storePin=WindowsFileBoundary.PinDirectory(store);
        using (var output=WindowsFileBoundary.OpenExclusive(store,temporary,true))
        {output.Stream.Write(data);output.Stream.Flush(true);output.Validate();}
        var destination=System.IO.Path.Combine(store,"stores.dpapi");
        if(File.Exists(destination)) File.Replace(System.IO.Path.Combine(store,temporary),destination,null);
        else File.Move(System.IO.Path.Combine(store,temporary),destination);
        storePin.Validate();pin.Validate();
    }

    public IReadOnlyList<ProtectedStorageRoot> Read() => ReadCore(false);
    private IReadOnlyList<ProtectedStorageRoot> ReadCore(bool allowMissing)
    {
        if(!File.Exists(System.IO.Path.Combine(store,"stores.dpapi")))
        {
            if(!allowMissing && File.Exists(System.IO.Path.Combine(store,"registry.lock")))throw new InvalidDataException("ProtectionRegistryMissing");
            return [];
        }
        using var file=WindowsFileBoundary.OpenRead(store,"stores.dpapi");
        if(file.Stream.Length is <=0 or >1024*1024)throw new InvalidDataException("ProtectionRegistrySize");
        using var stream=new MemoryStream();file.Stream.CopyTo(stream);file.Validate();
        var data=ProtectedData.Unprotect(stream.ToArray(),Entropy,DataProtectionScope.CurrentUser);
        try
        {
            var entries=JsonSerializer.Deserialize<List<ProtectedStorageRoot>>(data)??throw new InvalidDataException("ProtectionRegistryInvalid");
            if(entries.Count>128 || entries.Any(e=>!System.IO.Path.IsPathFullyQualified(e.Path)||string.IsNullOrEmpty(e.Identity)))
                throw new InvalidDataException("ProtectionRegistryInvalid");
            return entries;
        }
        finally{CryptographicOperations.ZeroMemory(data);}
    }

    public bool IsProtected(string path)
    {
        if(!System.IO.Path.IsPathFullyQualified(path))return false;
        var full=System.IO.Path.GetFullPath(path).TrimEnd('\\');
        return Read().Any(e=>full.Equals(e.Path,StringComparison.OrdinalIgnoreCase)
            ||full.StartsWith(e.Path.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase));
    }

    internal static bool DefaultContains(string path)
    {
        try{return new StorageProtectionRegistry(DefaultStore).IsProtected(path);}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or JsonException)
        {return true;}
    }
}

public sealed record CleanupProvider(string Id,string Root,string Explanation)
{
    public bool ExecutionEnabled=>false;
}

public static class CleanupAllowlist
{
    // A Temp location grants observation only. Its contents can contain user data.
    public static IReadOnlyList<CleanupProvider> ReadOnlyProviders()=>
        [new("UserTemp",System.IO.Path.GetTempPath(),"임시 경로 후보입니다. 사용자 자료와 사용 중인 파일을 별도로 확인해야 합니다.")];
    public static bool CanObserve(CleanupProvider provider,string target,StorageProtectionRegistry? registry=null)
    {
        if(provider.Id!="UserTemp" || provider.ExecutionEnabled
            || !System.IO.Path.GetFullPath(provider.Root).Equals(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))return false;
        using var pin=WindowsFileBoundary.PinDirectory(provider.Root);
        var relative=System.IO.Path.GetRelativePath(pin.Root,System.IO.Path.GetFullPath(target));
        if(relative==".."||relative.StartsWith("..\\",StringComparison.Ordinal)||System.IO.Path.IsPathRooted(relative))return false;
        if(StorageTargetPolicy.IsProtected(target)||registry?.IsProtected(target)==true)return false;
        using var file=WindowsFileBoundary.OpenRead(pin.Root,relative);file.Validate();return true;
    }
}
