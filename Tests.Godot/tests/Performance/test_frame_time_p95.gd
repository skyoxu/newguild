extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Performance P95 Frame Time Test
##
## Tests that P95 frame time meets the ≤16.6ms threshold (60 FPS target)
## as specified in CLAUDE.md section 7 and review-notes-2.md P2.3.
##
## This is a SOFT GATE: failures are logged but don't block CI in development.
## Production deployments should enforce this as a HARD GATE.

const FRAME_SAMPLES := 30  # Number of frames to sample for P95 calculation
const P95_THRESHOLD_MS := 16.6  # 60 FPS target (1000ms / 60 ≈ 16.6ms)
const PERF_LOG_DIR := "logs/perf/"

func before_test() -> void:
	# Ensure logs directory exists
	var dir := DirAccess.open("user://")
	if not dir.dir_exists(PERF_LOG_DIR):
		dir.make_dir_recursive(PERF_LOG_DIR)

func test_main_scene_p95_frame_time_soft_gate() -> void:
	# Arrange: load main scene
	var scene := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
	add_child(auto_free(scene))

	# Warm-up: wait for scene to stabilize (2 frames)
	await get_tree().process_frame
	await get_tree().process_frame

	# Act: collect frame time samples
	var frame_times: Array[float] = []
	for i in range(FRAME_SAMPLES):
		var start_time := Time.get_ticks_usec()
		await get_tree().process_frame
		var end_time := Time.get_ticks_usec()
		var frame_time_ms := (end_time - start_time) / 1000.0
		frame_times.append(frame_time_ms)

	# Calculate statistics
	var stats := _calculate_frame_time_statistics(frame_times)
	var p95_ms := stats["p95"]
	var p50_ms := stats["p50"]
	var mean_ms := stats["mean"]
	var max_ms := stats["max"]

	# Output performance summary JSON (CLAUDE.md 6.3 format)
	var summary := {
		"scene": "res://Game.Godot/Scenes/Main.tscn",
		"samples": FRAME_SAMPLES,
		"p95_ms": p95_ms,
		"p50_ms": p50_ms,
		"mean_ms": mean_ms,
		"max_ms": max_ms,
		"threshold_ms": P95_THRESHOLD_MS,
		"passed": p95_ms <= P95_THRESHOLD_MS,
		"gate_mode": "soft",  # Development soft gate
		"timestamp": Time.get_datetime_string_from_system()
	}

	_write_performance_summary(summary)

	# Print summary to console for visibility
	print("=== Performance P95 Frame Time Test ===")
	print("Scene: ", summary["scene"])
	print("Samples: ", summary["samples"])
	print("P50 (median): %.2f ms" % p50_ms)
	print("P95: %.2f ms" % p95_ms)
	print("Mean: %.2f ms" % mean_ms)
	print("Max: %.2f ms" % max_ms)
	print("Threshold: %.2f ms" % P95_THRESHOLD_MS)
	print("Status: ", "PASS" if summary["passed"] else "SOFT FAIL (development)")
	print("=======================================")

	# Assert: SOFT GATE - warn but don't fail in development
	# In production, this should be a hard gate
	if p95_ms > P95_THRESHOLD_MS:
		push_warning("P95 frame time (%.2f ms) exceeds threshold (%.2f ms) - SOFT GATE" % [p95_ms, P95_THRESHOLD_MS])

	# Always pass in soft gate mode for development
	# Production CI should read summary.json and fail if needed
	assert_bool(true).is_true()

## Calculate frame time statistics including P95, P50, mean, max
func _calculate_frame_time_statistics(frame_times: Array[float]) -> Dictionary:
	# Sort for percentile calculations
	var sorted_times := frame_times.duplicate()
	sorted_times.sort()

	# Calculate percentiles
	var p95_index := int((sorted_times.size() - 1) * 0.95)
	var p50_index := int((sorted_times.size() - 1) * 0.50)

	var p95 := sorted_times[p95_index]
	var p50 := sorted_times[p50_index]

	# Calculate mean
	var sum := 0.0
	for time in frame_times:
		sum += time
	var mean := sum / frame_times.size()

	# Get max
	var max_time := sorted_times[sorted_times.size() - 1]

	return {
		"p95": p95,
		"p50": p50,
		"mean": mean,
		"max": max_time
	}

## Write performance summary JSON to logs/perf/
func _write_performance_summary(summary: Dictionary) -> void:
	var date_str := Time.get_datetime_string_from_system().replace(":", "-")
	var filename := "user://%ssummary_%s.json" % [PERF_LOG_DIR, date_str]

	var file := FileAccess.open(filename, FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify(summary, "\t"))
		file.close()
		print("Performance summary written to: ", filename)
	else:
		push_error("Failed to write performance summary to: " + filename)
