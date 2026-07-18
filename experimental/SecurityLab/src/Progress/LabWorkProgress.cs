namespace SmartPerformanceDoctor.SecurityLab.Progress;

/// <summary>
/// Work-order progress for SecurityLab isolation track.
/// Product merge is intentionally blocked until approval.
/// </summary>
public static class LabWorkProgress
{
    public sealed class Snapshot
    {
        public IReadOnlyList<Item> Items { get; init; } = Array.Empty<Item>();
        public double InstructionPercent { get; init; }
        public double LabImplementationPercent { get; init; }
        public double ProductShipPercent { get; init; }
        public double OverallDesignBlendPercent { get; init; }
        public double ShippingTrackPercent { get; init; }
        public double DesignSClassPercent { get; init; }
        public string SummaryLine { get; init; } = "";
        public string DesignVerdict { get; init; } = "";

        public string ToHumanSummary()
        {
            var design = DesignProgressScore.Calculate();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(design.ToHumanSummary());
            sb.AppendLine("=== 1차 지시서 트랙 (참고) ===");
            sb.AppendLine($"지시서 항목 기준:     {InstructionPercent:0.#}%");
            sb.AppendLine($"Lab 구현 완성도:      {LabImplementationPercent:0.#}%");
            sb.AppendLine($"제품 탑재(플래그):     {ProductShipPercent:0.#}%");
            sb.AppendLine();
            foreach (var i in Items)
            {
                var mark = i.Status switch
                {
                    ItemStatus.Done => "[x]",
                    ItemStatus.Partial => "[~]",
                    _ => "[ ]"
                };
                sb.AppendLine($"{mark} {i.WeightPercent,5:0.#}%  {i.Id}. {i.Title} — {i.Detail}");
            }

            sb.AppendLine();
            sb.AppendLine(SummaryLine);
            return sb.ToString();
        }
    }

    public sealed class Item
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required ItemStatus Status { get; init; }
        public required double WeightPercent { get; init; }
        public string Detail { get; init; } = "";
    }

    public enum ItemStatus
    {
        Pending,
        Partial,
        Done
    }

    public static Snapshot Calculate()
    {
        // Weighted work order (sums to 100 for instruction track).
        var items = new List<Item>
        {
            new()
            {
                Id = "1",
                Title = "VaultV4 독립 csproj (App 비참조)",
                Status = ItemStatus.Done,
                WeightPercent = 15,
                Detail = "SmartPerformanceDoctor.SecurityLab.csproj"
            },
            new()
            {
                Id = "2",
                Title = "청크 AEAD + objects/ + 복구코드 + LabVaultService",
                Status = ItemStatus.Done,
                WeightPercent = 25,
                Detail = "create/unlock/import/export/crypto-shred"
            },
            new()
            {
                Id = "3",
                Title = "ShredNext dry-run / 확인문구 / 삭제 API",
                Status = ItemStatus.Done,
                WeightPercent = 15,
                Detail = "LabShredEngine"
            },
            new()
            {
                Id = "4",
                Title = "통합 테스트 하네스",
                Status = ItemStatus.Done,
                WeightPercent = 15,
                Detail = "SecurityLab.Tests"
            },
            new()
            {
                Id = "5",
                Title = "astra-vault Rust 포맷 정렬 문서",
                Status = ItemStatus.Done,
                WeightPercent = 10,
                Detail = "docs/FORMAT_ALIGNMENT.md"
            },
            new()
            {
                Id = "6a",
                Title = "v3→v4 마이그레이션 dry-run 도구",
                Status = ItemStatus.Done,
                WeightPercent = 10,
                Detail = "V3MigrationDryRun + SecurityLab.Cli migrate-dry-run"
            },
            new()
            {
                Id = "6b",
                Title = "마이그레이션 실행기 (re-import 자동화)",
                Status = ItemStatus.Done,
                WeightPercent = 5,
                Detail = "V3ToLabMigrator + ProductV3Reader (App 비참조)"
            },
            new()
            {
                Id = "7",
                Title = "제품 병합 PR / 플래그 탑재",
                Status = ItemStatus.Done,
                WeightPercent = 5,
                Detail = "50.3.0 App 참조 · ProductHost 플래그 ON · SecureVaultLabBackend · ShredNext 연동"
            },
            new()
            {
                Id = "8",
                Title = "보안·암호·정책 하드닝 라운드",
                Status = ItemStatus.Done,
                WeightPercent = 0,
                Detail =
                    "rate-limit, password/session policy, path/UNC, audit chain, rule-pack HMAC, " +
                    "recovery one-time, stream AEAD, constant-time compare, product gate, obfuscation policy"
            }
        };

        var instructionDone = items.Where(i => i.Status == ItemStatus.Done).Sum(i => i.WeightPercent);
        var instructionPartial = items.Where(i => i.Status == ItemStatus.Partial).Sum(i => i.WeightPercent * 0.5);
        // Item 8 is zero-weight informational; total weight remains 100 from 1–7.
        var instructionPercent = instructionDone + instructionPartial;

        var labItems = items.Where(i => i.Id is not "7" and not "8").ToArray();
        var labTotal = labItems.Sum(i => i.WeightPercent);
        var labDone = labItems.Where(i => i.Status == ItemStatus.Done).Sum(i => i.WeightPercent);
        var labPercent = labTotal <= 0 ? 0 : 100.0 * labDone / labTotal;

        // Product ship track: item 7 done ⇒ flag-wired; package released ⇒ 100.
        var productShip = items.Any(i => i.Id == "7" && i.Status == ItemStatus.Done)
            ? (ProductBridge.LabReleaseState.InstallerPackageReleased ? 100.0 : 85.0)
            : 0.0;
        var design = DesignProgressScore.Calculate();
        var pkgLabel = ProductBridge.LabReleaseState.InstallerPackageReleased
            ? ProductBridge.LabReleaseState.SetupFileName
            : "패키지 보류";

        return new Snapshot
        {
            Items = items,
            InstructionPercent = instructionPercent,
            LabImplementationPercent = labPercent,
            ProductShipPercent = productShip,
            OverallDesignBlendPercent = design.OverallPercent,
            ShippingTrackPercent = design.ShippingTrackPercent,
            DesignSClassPercent = design.DesignSClassPercent,
            DesignVerdict = design.Verdict,
            SummaryLine =
                $"종합 {design.OverallPercent:0.#}% · 출시트랙 {design.ShippingTrackPercent:0.#}% · " +
                $"S급설계 {design.DesignSClassPercent:0.#}% · 지시서 {instructionPercent:0.#}% · " +
                $"제품플래그 {productShip:0.#}% · {pkgLabel}. {design.Verdict}"
        };
    }
}
