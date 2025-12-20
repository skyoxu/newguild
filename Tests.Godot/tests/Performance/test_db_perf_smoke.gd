extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

# DB performance smoke test (SOFT gate).
# Produces a structured JSON summary with required metrics in:
#   logs/perf/<YYYY-MM-DD>/db/db-perf-summary.json
#
# This suite is designed to prevent "missing perf data" regressions.

func _out_dir_abs() -> String:
	var v := str(OS.get_environment("PERF_DB_OUT_DIR"))
	return v.strip_edges()


func test_db_perf_smoke_writes_required_metrics() -> void:
	var out_dir := _out_dir_abs()
	if out_dir == "":
		push_warning("PERF_DB_OUT_DIR not set; skipping db perf smoke test.")
		assert_bool(true).is_true()
		return

	var script = load("res://Game.Godot/Adapters/Db/DbPerfProbe.cs")
	assert_object(script).is_not_null()

	var probe: Node = script.new()
	get_tree().get_root().add_child(auto_free(probe))
	await get_tree().process_frame

	var result = probe.RunAndWriteSummary("user://perf_smoke.db", out_dir)
	assert_bool(bool(result.get("ok", false))).is_true()

	var summary_path := out_dir.path_join("db-perf-summary.json")
	assert_bool(FileAccess.file_exists(summary_path)).is_true()

	var txt := FileAccess.get_file_as_string(summary_path)
	assert_str(txt).is_not_empty()
	var parsed = JSON.parse_string(txt)
	assert_object(parsed).is_not_null()

	# Required blocker metrics
	for k in [
		"DB_CONNECTION_TIME",
		"DB_QUERY_P95",
		"DB_MEMORY_LEAK",
		"DB_CONCURRENCY",
		"DB_LARGE_RESULT",
		"DB_STARTUP_IMPACT"
	]:
		assert_bool(parsed.has(k)).is_true()

	# Minimal sanity checks (SOFT gate: do not assert thresholds here)
	assert_bool(int(parsed.get("DB_QUERY_P95", {}).get("samples", 0)) > 0).is_true()
	assert_bool(float(parsed.get("DB_QUERY_P95", {}).get("p95_ms", 0.0)) > 0.0).is_true()
	assert_bool(int(parsed.get("DB_LARGE_RESULT", {}).get("rows", 0)) > 0).is_true()
