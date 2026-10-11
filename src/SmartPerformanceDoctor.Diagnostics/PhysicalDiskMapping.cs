using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed record StorageIdentity(int Number, string? UniqueId, string BusType, string MediaType, int PartitionCount);
public sealed record QualifiedDiskIdentity(string CounterInstance, string? Identifier, string DeviceType, string Status, int? PartitionCount);
public static class PhysicalDiskMapping
{
    public static QualifiedDiskIdentity Resolve(string instance, IReadOnlyList<StorageIdentity> disks, byte[] localSecret)
    {
        if (localSecret.Length < 32) throw new ArgumentException("ProtectedLocalIdentityKeyRequired");
        if (!int.TryParse(instance.Split(' ')[0], out var number) || number < 0) return new("Masked", null, "Unknown", "Unmapped", null);
        var matches = disks.Where(d => d.Number == number).ToArray();
        if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].UniqueId)
            || disks.Count(d => d.UniqueId == matches[0].UniqueId) != 1) return new(number.ToString(), null, "Unknown", "AmbiguousOrMissingStorageIdentity", null);
        var disk = matches[0];
        var id = Convert.ToHexString(HMACSHA256.HashData(localSecret, Encoding.UTF8.GetBytes(disk.UniqueId!)))[..24];
        var type = disk.BusType == "NVMe" && disk.MediaType == "SSD" ? "NVMe SSD"
            : disk.BusType == "SATA" && disk.MediaType == "SSD" ? "SATA SSD" : disk.MediaType == "HDD" ? "HDD" : "Unknown";
        return new(number.ToString(), id, type, "UniqueStorageIdMatched;RevalidateEachCollection", disk.PartitionCount);
    }
}
