pub struct DeviceApi;

impl DeviceApi {
    pub fn plan_rescan() -> &'static str {
        "pnputil /scan-devices or SetupAPI re-enumeration"
    }
}
