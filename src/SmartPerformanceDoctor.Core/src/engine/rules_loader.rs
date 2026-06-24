use serde::Deserialize;
use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone, Default, Deserialize)]
pub struct DriverCodeRule {
    pub title: String,
    pub recommendation: String,
    pub risk: String,
}

#[derive(Debug, Clone, Default, Deserialize)]
pub struct SystemRuleEntry {
    #[serde(default)]
    pub threshold_free_percent: Option<u8>,
    #[serde(default)]
    pub threshold_used_percent: Option<u8>,
    #[serde(default)]
    pub action: String,
}

#[derive(Debug, Clone, Default, Deserialize)]
pub struct AudioRuleEntry {
    #[serde(default)]
    pub signals: Vec<String>,
    #[serde(default)]
    pub action: String,
}

#[derive(Debug, Clone, Default)]
pub struct RulesCatalog {
    pub driver_codes: HashMap<String, DriverCodeRule>,
    pub system_rules: HashMap<String, SystemRuleEntry>,
    pub audio_rules: HashMap<String, AudioRuleEntry>,
    pub remediation_forbidden: Vec<String>,
    pub remediation_requires_approval: Vec<String>,
}

pub fn load_rules(base_dir: &Path) -> RulesCatalog {
    let rules_dir = base_dir.join("rules");
    let mut catalog = RulesCatalog::default();

    if !rules_dir.exists() {
        return catalog;
    }

    if let Ok(text) = fs::read_to_string(rules_dir.join("driver_problem_codes.json"))
        && let Ok(map) = serde_json::from_str::<HashMap<String, DriverCodeRule>>(&text)
    {
        catalog.driver_codes = map;
    }

    if let Ok(text) = fs::read_to_string(rules_dir.join("system_rules.json"))
        && let Ok(map) = serde_json::from_str::<HashMap<String, SystemRuleEntry>>(&text)
    {
        catalog.system_rules = map;
    }

    if let Ok(text) = fs::read_to_string(rules_dir.join("audio_rules.json"))
        && let Ok(map) = serde_json::from_str::<HashMap<String, AudioRuleEntry>>(&text)
    {
        catalog.audio_rules = map;
    }

    #[derive(Debug, Deserialize, Default)]
    struct RemediationPolicies {
        #[serde(default)]
        forbidden: Vec<String>,
        #[serde(default)]
        requires_approval: Vec<String>,
    }

    if let Ok(text) = fs::read_to_string(rules_dir.join("remediation_policies.json"))
        && let Ok(policy) = serde_json::from_str::<RemediationPolicies>(&text)
    {
        catalog.remediation_forbidden = policy.forbidden;
        catalog.remediation_requires_approval = policy.requires_approval;
    }

    catalog
}

pub fn resolve_rules_base_dir() -> PathBuf {
    if let Ok(cwd) = std::env::current_dir()
        && cwd.join("rules").exists()
    {
        return cwd;
    }

    if let Ok(exe) = std::env::current_exe()
        && let Some(parent) = exe.parent()
    {
        return parent.to_path_buf();
    }

    PathBuf::from(".")
}