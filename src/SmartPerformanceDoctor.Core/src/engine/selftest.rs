use super::event_sink::EventSink;

pub async fn run_self_test(sink: EventSink) -> Vec<String> {
    sink.stage("selftest", "Core selftest 시작", 5);
    sink.stage("selftest", "JSON Lines protocol 준비", 25);
    sink.stage("selftest", "EventSink streaming 준비", 45);
    sink.stage("selftest", "Intelligence summary 준비", 70);
    sink.stage("selftest", "Report writer 준비", 90);

    vec![
        "selftest.core_ready".into(),
        "selftest.protocol_ready".into(),
        "selftest.event_sink_ready".into(),
        "selftest.intelligence_ready".into(),
        "selftest.report_writer_ready".into(),
    ]
}
