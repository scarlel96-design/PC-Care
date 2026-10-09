"""Inventory every baseline xUnit Skip with its source provenance."""
from __future__ import annotations

import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TESTS = ROOT / "tests" / "SmartPerformanceDoctor.Tests"
OUT = ROOT / "docs" / "rework" / "WO02_TEST_SKIP_INVENTORY.md"


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=ROOT, check=True, capture_output=True,
        text=True, encoding="utf-8", errors="replace"
    ).stdout


def blame(path: str) -> dict[int, tuple[str, str]]:
    rows = {}
    line_no = None
    sha = ""
    summary = ""
    for line in git("blame", "--line-porcelain", "HEAD", "--", path).splitlines():
        match = re.match(r"^([0-9a-f]{40}) \d+ (\d+)(?: \d+)?$", line)
        if match:
            sha, line_no, summary = match.group(1), int(match.group(2)), ""
        elif line.startswith("summary "):
            summary = line[8:]
        elif line.startswith("\t") and line_no is not None:
            rows[line_no] = (sha[:12], summary)
            line_no = None
    return rows


def main() -> None:
    rows = []
    for path in sorted(TESTS.glob("*.cs")):
        rel = path.relative_to(ROOT).as_posix()
        original = git("show", f"HEAD:{rel}").splitlines()
        current = path.read_text(encoding="utf-8-sig")
        provenance = blame(rel)
        for index, line in enumerate(original):
            match = re.search(r'\[(?:Fact|Theory)\(Skip\s*=\s*"([^"]+)"\)\]', line)
            if not match:
                continue
            method = ""
            for follow in original[index + 1:index + 12]:
                method_match = re.search(r"\b(?:void|Task|ValueTask)\s+(\w+)\s*\(", follow)
                if method_match:
                    method = method_match.group(1)
                    break
            if not method:
                raise ValueError(f"Missing method after {rel}:{index + 1}")
            restored = bool(re.search(
                r"\[Fact\]\s+public\s+(?:void|Task|ValueTask)\s+" + re.escape(method) + r"\s*\(",
                current,
            ))
            category = "RegressionGap"
            risk = "High" if re.search(
                r"Writer|Migration|Anchor|XChaCha|Production|Security|Recovery|Corrupt|Tamper",
                method, re.I
            ) else "Medium"
            sha, commit_summary = provenance.get(index + 1, ("unknown", "unavailable"))
            plan = "Skip 해제, 이번 빌드에서 실행" if restored else "현행 게이트와 문서 기대값 재검토 후 격리 실행"
            rows.append(
                (rel, index + 1, method, match.group(1), sha, commit_summary,
                 category, risk, "합성 테스트 루트 및 Windows x64", plan, restored)
            )
    lines = [
        "# WO-02 Skip 테스트 전수 조사",
        "",
        f"기준: HEAD의 Skip 속성 {len(rows)}개. 분류는 모두 RegressionGap이다. "
        "기존 Skip 사유가 '50.4.0 production GO'였으나 현재 생산 게이트가 NO-GO로 교정되어 "
        "과거 면제 근거가 소멸했다. 일부 테스트는 문서 기반 기대값을 갱신해야 재실행할 수 있다.",
        "",
        "최초 도입 배경 열은 Git blame 커밋 및 제목이다. 커밋은 속성을 마지막으로 변경한 지점이며 "
        "해당 테스트의 최초 작성 시점과 같다고 단정하지 않는다. 현재 유효성은 모두 재검토 대상이다.",
        "",
        "| # | 테스트/위치 | 대상 기능 | Skip 사유 | 도입 근거(커밋/제목) | 현재 유효성/분류 | 실행 조건 | 위험 | 처리 계획 |",
        "|---:|---|---|---|---|---|---|---|---|",
    ]
    for number, (rel, line, method, reason, sha, summary, category, risk, condition, plan, restored) in enumerate(rows, 1):
        escaped = lambda value: str(value).replace("|", "\\|").replace("\n", " ")
        feature = Path(rel).stem
        validity = f"{'재활성화' if restored else '재검토'} / {category}"
        lines.append(
            f"| {number} | `{escaped(method)}`<br>`{rel}:{line}` | {feature} | "
            f"{escaped(reason)} | `{sha}` {escaped(summary)} | {validity} | "
            f"{condition} | {risk} | {plan} |"
        )
    lines.extend([
        "",
        f"재활성화: {sum(row[-1] for row in rows)}개; 잔여 Skip: {sum(not row[-1] for row in rows)}개.",
        "재활성화는 실행 성공을 뜻하지 않는다. 실행 결과는 WO02_SECURITY_VERIFICATION.md 및 TRX에 기록한다.",
        "",
    ])
    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"baseline_skips={len(rows)} reenabled={sum(row[-1] for row in rows)} remaining={sum(not row[-1] for row in rows)}")


if __name__ == "__main__":
    main()
