use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReportDocument {
    pub title: String,
    pub created_at: String,
    pub module: String,
    pub status: String,
    pub summary: String,
    #[serde(default)]
    pub scan_findings: Vec<String>,
    pub events: Vec<String>,
    pub root_causes: Vec<String>,
    /// Legacy field kept for tools that still read `actions`.
    #[serde(default)]
    pub actions: Vec<String>,
    #[serde(default)]
    pub actions_taken: Vec<String>,
    #[serde(default)]
    pub recommended_actions: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReportBundle {
    pub dir: String,
    pub html: String,
    pub json: String,
    pub text: String,
}

pub struct ReportWriter;

impl ReportWriter {
    pub fn default_report_dir(module: &str) -> PathBuf {
        let stamp = timestamp_compact();
        let folder = format!("Report_{}_{}", stamp, sanitize(module));

        if let Some(user_profile) = std::env::var_os("USERPROFILE") {
            return PathBuf::from(user_profile)
                .join("Desktop")
                .join("SmartPerformanceDoctor")
                .join("Reports")
                .join(folder);
        }

        std::env::temp_dir()
            .join("SmartPerformanceDoctor")
            .join("Reports")
            .join(folder)
    }

    pub fn write_bundle(output_dir: &Path, report: &ReportDocument) -> std::io::Result<ReportBundle> {
        std::fs::create_dir_all(output_dir)?;

        let json_path = output_dir.join("report.json");
        let html_path = output_dir.join("report.html");
        let text_path = output_dir.join("summary.txt");

        let json = serde_json::to_string_pretty(report).unwrap_or_else(|_| "{}".to_string());
        std::fs::write(&json_path, json)?;
        std::fs::write(&html_path, render_html(report))?;
        std::fs::write(&text_path, render_text(report))?;

        Ok(ReportBundle {
            dir: output_dir.to_string_lossy().to_string(),
            html: html_path.to_string_lossy().to_string(),
            json: json_path.to_string_lossy().to_string(),
            text: text_path.to_string_lossy().to_string(),
        })
    }
}

pub fn timestamp_human() -> String {
    let secs = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0);
    format!("unix:{secs}")
}

fn timestamp_compact() -> String {
    let secs = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0);
    secs.to_string()
}

fn sanitize(input: &str) -> String {
    input
        .chars()
        .map(|c| if c.is_ascii_alphanumeric() || c == '-' || c == '_' { c } else { '_' })
        .collect()
}

fn render_html(report: &ReportDocument) -> String {
    let findings = list_items(&report.scan_findings);
    let roots = list_items(&report.root_causes);
    let taken = list_items(&effective_actions_taken(report));
    let events = list_items(&report.events);
    let reference = list_items(&report.recommended_actions);

    let reference_section = if report.recommended_actions.is_empty() {
        String::new()
    } else {
        format!(
            r#"<div class="card muted-card"><h2>참고 · 필요 시 검토</h2><ul>{reference}</ul><p class="muted">위 항목은 자동 분석 결과이며, 실제 복구는 사용자 승인 후에만 진행됩니다.</p></div>"#
        )
    };

    format!(
        r#"<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
body {{ font-family: "Segoe UI", "Malgun Gothic", system-ui, sans-serif; background:#0b1020; color:#eef3ff; margin:0; padding:28px; line-height:1.55; }}
.wrap {{ max-width: 920px; margin: 0 auto; }}
.card {{ background:#151d31; border:1px solid #2b385a; border-radius:18px; padding:20px 22px; margin:14px 0; }}
.muted-card {{ border-color:#24304f; }}
.muted {{ color:#9ca8bc; font-size: 14px; }}
h1 {{ letter-spacing:-0.03em; margin: 0 0 6px; font-size: 28px; }}
h2 {{ margin: 0 0 10px; font-size: 18px; color:#d9e4ff; }}
.badge {{ display:inline-block; padding:4px 10px; border-radius:999px; background:#22345d; font-size:13px; margin-right:8px; }}
ul {{ margin: 8px 0 0; padding-left: 20px; }}
li {{ margin-bottom: 6px; }}
.status-ok {{ color:#6ee7a8; }}
.status-warn {{ color:#f6c56b; }}
.status-bad {{ color:#ff8f8f; }}
</style>
</head>
<body>
<div class="wrap">
<h1>{title}</h1>
<p class="muted">생성: {created} · 모듈: {module}</p>
<div class="card">
  <h2>상태 요약</h2>
  <p><span class="badge {status_class}">{status}</span></p>
  <p>{summary}</p>
</div>
<div class="card"><h2>정밀 스캔 결과</h2><ul>{findings}</ul></div>
<div class="card"><h2>원인 후보</h2><ul>{roots}</ul></div>
<div class="card"><h2>조치 사항</h2><ul>{taken}</ul></div>
{reference_section}
<div class="card muted-card"><h2>이벤트 기록</h2><ul>{events}</ul></div>
</div>
</body></html>"#,
        title = escape(&report.title),
        created = escape(&report.created_at),
        module = escape(&report.module),
        status = escape(&report.status),
        status_class = status_css_class(&report.status),
        summary = escape(&report.summary),
        findings = findings,
        roots = roots,
        taken = taken,
        reference_section = reference_section,
        events = events,
    )
}

fn render_text(report: &ReportDocument) -> String {
    format!(
        "{}\n{}\n모듈: {}\n\n상태: {}\n요약: {}\n\n정밀 스캔 결과:\n{}\n\n원인 후보:\n{}\n\n조치 사항:\n{}\n\n이벤트:\n{}\n",
        report.title,
        report.created_at,
        report.module,
        report.status,
        report.summary,
        report.scan_findings.iter().map(|x| format!("- {x}")).collect::<Vec<_>>().join("\n"),
        report.root_causes.iter().map(|x| format!("- {x}")).collect::<Vec<_>>().join("\n"),
        effective_actions_taken(report)
            .iter()
            .map(|x| format!("- {x}"))
            .collect::<Vec<_>>()
            .join("\n"),
        report.events.iter().map(|x| format!("- {x}")).collect::<Vec<_>>().join("\n"),
    )
}

fn effective_actions_taken(report: &ReportDocument) -> Vec<String> {
    if !report.actions_taken.is_empty() {
        return report.actions_taken.clone();
    }

    if !report.actions.is_empty() {
        return report.actions.clone();
    }

    vec!["진단 스캔만 수행 · PC 설정 변경 없음".into()]
}

fn list_items(items: &[String]) -> String {
    if items.is_empty() {
        return "<li>해당 없음</li>".to_string();
    }

    items
        .iter()
        .map(|x| format!("<li>{}</li>", escape(x)))
        .collect::<Vec<_>>()
        .join("\n")
}

fn status_css_class(status: &str) -> &'static str {
    let s = status.to_lowercase();
    if s.contains("양호") || s.contains("ok") || s.contains("정상") {
        "status-ok"
    } else if s.contains("위험") || s.contains("critical") || s.contains("fail") {
        "status-bad"
    } else {
        "status-warn"
    }
}

fn escape(input: &str) -> String {
    input
        .replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}