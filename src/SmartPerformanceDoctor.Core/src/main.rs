use smart_performance_doctor_core::engine::orchestrator::EngineOrchestrator;

#[tokio::main]
async fn main() {
    let orchestrator = EngineOrchestrator::new();
    orchestrator.run_stdio_json_lines().await;
}
