using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services.Update;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public void ReleaseManifest_DeserializesReleaseNotes()
    {
        const string json = """
            {
              "version": "51.0.1",
              "releaseNotes": "- 업데이트 화면에 수정 사항 표시"
            }
            """;

        var manifest = JsonSerializer.Deserialize<RemoteReleaseManifestDocument>(json);

        Assert.Equal("- 업데이트 화면에 수정 사항 표시", manifest?.ReleaseNotes);
    }

    [Fact]
    public void NormalizeReleaseNotes_ReturnsOnlyUserFacingChanges()
    {
        const string notes = """
            # PC 케어 프로 v51.0.1
            ## 변경 사항
            - 업데이트 화면에 수정 사항 표시
            - 확인 결과의 가독성 개선

            ---
            **배포 파일**
            - 설치: `PCCare_Setup_v51.0.1.exe`
            - 업데이트: `PCCare_Update_v51.0.1.spdup`
            update-sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            """;

        var result = GitHubReleaseUpdateService.NormalizeReleaseNotes(notes);

        Assert.Equal(
            new[] { "업데이트 화면에 수정 사항 표시", "확인 결과의 가독성 개선" },
            result);
    }
}