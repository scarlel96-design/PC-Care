pub struct ReportRenderer;

impl ReportRenderer {
    pub fn render_html() -> String {
        "<!doctype html><html lang=\"ko\"><meta charset=\"utf-8\"><title>Smart Performance Doctor</title><body>v16 report</body></html>".to_string()
    }
}
