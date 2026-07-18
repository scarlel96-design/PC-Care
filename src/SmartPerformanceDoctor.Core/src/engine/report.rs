use super::types::{EngineEvent, IntelligenceSummary};
use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

pub struct ReportPaths {
    pub folder: PathBuf,
    pub html: PathBuf,
    pub json: PathBuf,
    pub summary: PathBuf,
}

pub fn write_report(
    module: &str,
    status: &str,
    message: &str,
    events: &[EngineEvent],
    intelligence: &IntelligenceSummary,
    signals: &[String],
) -> Option<ReportPaths> {
    let base = report_base_dir()?;
    let stamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0);
    let folder = base.join(format!("Report_{stamp}_{module}"));
    fs::create_dir_all(&folder).ok()?;

    let html = folder.join("report.html");
    let json = folder.join("report.json");
    let summary = folder.join("summary.txt");

    let score = intelligence.score;
    let status_label = &intelligence.status;
    let plain = &intelligence.plain_summary;

    let event_rows: String = events
        .iter()
        .map(|e| {
            format!(
                "<tr><td>{}</td><td>{}</td><td>{}</td><td>{}%</td></tr>",
                escape(&e.event_type),
                escape(&e.severity),
                escape(&e.message),
                e.progress
            )
        })
        .collect();

    let causes: String = intelligence
        .root_causes
        .iter()
        .map(|c| {
            format!(
                "<li><b>{}</b> ({}) — {} <em>→ {}</em></li>",
                escape(&c.evidence),
                escape(&c.severity),
                escape(&c.explanation),
                escape(&c.recommendation)
            )
        })
        .collect();

    let html_body = format!(
        r#"<!DOCTYPE html>
<html lang="ko"><head><meta charset="utf-8"/>
<title>PCCare Report — {module}</title>
<style>
body {{ font-family: "Segoe UI", "Malgun Gothic", system-ui, sans-serif; background:#0b1020; color:#eef3ff; margin:0; padding:28px; line-height:1.55; }}
h1 {{ margin:0 0 8px; }} .meta {{ color:#9fb0d0; margin-bottom:18px; }}
.card {{ background:#141c33; border:1px solid #243154; border-radius:14px; padding:16px 18px; margin:14px 0; }}
.score {{ font-size:42px; font-weight:700; color:#7ee0a8; }}
table {{ width:100%; border-collapse:collapse; }} th,td {{ text-align:left; padding:8px; border-bottom:1px solid #243154; }}
.badge {{ display:inline-block; padding:2px 10px; border-radius:999px; background:#1d2a4a; }}
</style></head><body>
<h1>PC 케어 프로 · {module}</h1>
<div class="meta">{message} · 상태 {status}</div>
<div class="card"><div class="score">{score}</div>
<div class="badge">{status_label}</div>
<p>{plain}</p></div>
<div class="card"><h2>근본 원인</h2><ul>{causes}</ul></div>
<div class="card"><h2>이벤트</h2><table><thead><tr><th>유형</th><th>심각도</th><th>메시지</th><th>진행</th></tr></thead>
<tbody>{event_rows}</tbody></table></div>
</body></html>"#
    );

    let json_value = serde_json::json!({
        "title": format!("PCCare {module}"),
        "createdAt": stamp,
        "module": module,
        "status": status,
        "summary": message,
        "score": score,
        "statusLabel": status_label,
        "plainSummary": plain,
        "signals": signals,
        "events": events,
        "rootCauses": intelligence.root_causes,
        "actions": intelligence.actions,
    });

    fs::write(&html, html_body).ok()?;
    fs::write(&json, serde_json::to_string_pretty(&json_value).ok()?).ok()?;
    fs::write(
        &summary,
        format!(
            "module={module}\nstatus={status}\nscore={score}\nsummary={message}\n{plain}\n"
        ),
    )
    .ok()?;

    Some(ReportPaths {
        folder,
        html,
        json,
        summary,
    })
}

fn report_base_dir() -> Option<PathBuf> {
    // Prefer install-local reports, then Desktop legacy path used by previous builds.
    if let Ok(exe) = std::env::current_exe() {
        if let Some(dir) = exe.parent() {
            let local = dir.join("reports");
            if fs::create_dir_all(&local).is_ok() {
                return Some(local);
            }
        }
    }

    let desktop = std::env::var_os("USERPROFILE").map(PathBuf::from)?.join(
        "Desktop\\SmartPerformanceDoctor\\Reports",
    );
    fs::create_dir_all(&desktop).ok()?;
    Some(desktop)
}

fn escape(input: &str) -> String {
    input
        .replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}
