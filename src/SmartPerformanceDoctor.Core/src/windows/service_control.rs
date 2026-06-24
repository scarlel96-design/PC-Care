pub struct ServiceController;

impl ServiceController {
    pub fn plan_restart(service_name: &str) -> String {
        format!("restart service plan: {service_name}")
    }
}
