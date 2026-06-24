pub fn redact(input: &str) -> String {
    let mut output = input.to_string();

    let keys = [
        "password",
        "passwd",
        "token",
        "secret",
        "api_key",
        "apikey",
        "authorization",
        "bearer",
    ];

    for key in keys {
        output = output.replace(key, "[redacted-key]");
        output = output.replace(&key.to_uppercase(), "[redacted-key]");
    }

    // Simple sk-* masking without external regex dependency.
    let mut masked = Vec::new();
    for part in output.split_whitespace() {
        if part.starts_with("sk-") && part.len() > 14 {
            masked.push("sk-[redacted]".to_string());
        } else {
            masked.push(part.to_string());
        }
    }

    masked.join(" ")
}
