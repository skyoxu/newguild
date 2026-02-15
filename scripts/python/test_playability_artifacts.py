import datetime as dt
from pathlib import Path

from playability_artifacts import (
    collect_playability_artifacts,
    decide_route2_requirement,
    detect_route2_execution,
    is_route2_suite_requested,
    resolve_route2_artifact_requirement,
)
from validate_acceptance_refs import _is_allowed_test_path


def test_resolve_route2_artifact_requirement_modes() -> None:
    assert resolve_route2_artifact_requirement("always")[0] == "always"
    assert resolve_route2_artifact_requirement("never")[0] == "never"
    assert resolve_route2_artifact_requirement("auto")[0] == "auto"
    assert resolve_route2_artifact_requirement("1")[0] == "always"
    assert resolve_route2_artifact_requirement("0")[0] == "never"


def test_detect_route2_execution_from_junit_xml(tmp_path: Path) -> None:
    reports_dir = tmp_path / "reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    xml_path = reports_dir / "results.xml"
    xml_path.write_text(
        """
<testsuite>
  <testcase classname="tests.Playability.Phase2.test_phase2_play_route" name="test_phase2_play_route_menu_to_screens" />
</testsuite>
        """.strip(),
        encoding="utf-8",
    )

    console_path = tmp_path / "console.txt"
    console_path.write_text("", encoding="utf-8")

    executed, error_text = detect_route2_execution(
        str(reports_dir),
        str(console_path),
        "phase2_play_route",
        run_started_at_utc=dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1),
        max_xml_files=50,
        max_xml_bytes=1024 * 1024,
        max_console_bytes=1024,
    )
    assert executed is True
    assert error_text is None


def test_detect_route2_execution_reports_error_when_inputs_unreadable(tmp_path: Path) -> None:
    reports_dir = tmp_path / "reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    (reports_dir / "broken.xml").write_text("<testsuite><testcase>", encoding="utf-8")

    executed, error_text = detect_route2_execution(
        str(reports_dir),
        str(tmp_path / "missing-console.txt"),
        "phase2_play_route",
        run_started_at_utc=dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1),
        max_xml_files=50,
        max_xml_bytes=1024 * 1024,
        max_console_bytes=1024,
    )
    assert executed is False
    assert error_text is not None


def test_collect_playability_artifacts_rejects_oversized_artifact(tmp_path: Path) -> None:
    date_text = "2026-02-15"
    source_root = tmp_path / "source-root"
    source_day_dir = source_root / "logs" / "e2e" / date_text
    source_day_dir.mkdir(parents=True, exist_ok=True)
    artifact_name = "playability-route2-summary.json"
    artifact_path = source_day_dir / artifact_name
    artifact_path.write_text("{\"large\":true}", encoding="utf-8")

    out_dir = tmp_path / "out"
    out_dir.mkdir(parents=True, exist_ok=True)

    payload, errors, failed = collect_playability_artifacts(
        artifact_names=[artifact_name],
        artifact_source_roots=[source_root],
        artifact_source_root_labels=["userdir"],
        date=date_text,
        run_started_at_utc=dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1),
        out_dir=out_dir,
        repo_root=tmp_path,
        artifact_name_errors=[],
        require_route2_artifact=True,
        route2_artifact_name=artifact_name,
        route2_requirement_mode="always",
        route2_requirement_reason="explicit-enabled",
        route2_test_executed=True,
        artifact_max_mb=0,
        artifact_max_bytes=4,
    )

    assert failed is True
    assert any("artifact too large" in message for message in errors)
    assert payload["rc_after_artifact_check"] == 1


def test_is_route2_suite_requested_with_windows_path() -> None:
    requested = is_route2_suite_requested([
        r"Tests.Godot\tests\Playability\Phase2",
    ], "phase2_play_route")
    assert requested is True


def test_is_route2_suite_requested_negative_case() -> None:
    requested = is_route2_suite_requested([
        "tests/Playability/Phase1",
    ], "phase2_play_route")
    assert requested is False


def test_decide_route2_requirement_auto_limit_only_warning() -> None:
    require_route2_artifact, reason, warnings, errors = decide_route2_requirement(
        route2_requirement_mode="auto",
        route2_requirement_base_reason="auto-deferred",
        route2_test_executed=False,
        route2_detection_error="xml_file_limit_exceeded:count=300:limit=200",
        route2_suite_requested=True,
    )

    assert require_route2_artifact is True
    assert reason == "auto-requested-detection-limit-warning"
    assert len(warnings) == 1
    assert len(errors) == 0


def test_decide_route2_requirement_auto_non_limit_error_fail_closed() -> None:
    require_route2_artifact, reason, warnings, errors = decide_route2_requirement(
        route2_requirement_mode="auto",
        route2_requirement_base_reason="auto-deferred",
        route2_test_executed=False,
        route2_detection_error="xml_parse_failed:results.xml:ParseError",
        route2_suite_requested=True,
    )

    assert require_route2_artifact is True
    assert reason == "auto-requested-detection-error-fail-closed"
    assert len(warnings) == 0
    assert len(errors) == 1


def test_decide_route2_requirement_auto_requested_not_detected_fail_closed() -> None:
    require_route2_artifact, reason, warnings, errors = decide_route2_requirement(
        route2_requirement_mode="auto",
        route2_requirement_base_reason="auto-deferred",
        route2_test_executed=False,
        route2_detection_error=None,
        route2_suite_requested=True,
    )

    assert require_route2_artifact is True
    assert reason == "auto-requested-not-detected-fail-closed"
    assert len(warnings) == 0
    assert len(errors) == 0


def test_is_allowed_test_path_rejects_runtime_script_and_allows_playability_python_test() -> None:
    assert _is_allowed_test_path("scripts/python/run_gdunit.py") is False
    assert _is_allowed_test_path("scripts/python/test_playability_artifacts.py") is True


if __name__ == "__main__":
    import pytest

    raise SystemExit(pytest.main([__file__, "-q"]))
