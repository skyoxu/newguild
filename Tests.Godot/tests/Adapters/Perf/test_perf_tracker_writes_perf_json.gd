extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const PERF_TRACKER_CS := "res://Game.Godot/Scripts/Perf/PerformanceTracker.cs"
const PERF_JSON_USER := "user://logs/perf/perf.json"

func _delete_perf_json_if_exists() -> void:
	var dir := DirAccess.open("user://logs/perf")
	if dir == null:
		return
	if dir.file_exists("perf.json"):
		dir.remove("perf.json")

func _read_all_text(path: String) -> String:
	if not FileAccess.file_exists(path):
		return ""
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return ""
	var text := file.get_as_text()
	file.close()
	return text

func _wait_frames(frames: int) -> void:
	for _i in range(frames):
		await get_tree().process_frame

# ACC:T20.3
# ACC:T27.8
# ACC:T36.5
func test_perf_tracker_writes_perf_json_with_expected_shape() -> void:
	assert_bool(FileAccess.file_exists(PERF_TRACKER_CS)).is_true()

	_delete_perf_json_if_exists()

	var script := load(PERF_TRACKER_CS)
	assert_object(script).is_not_null()
	var tracker = script.new()
	if tracker == null:
		tracker = Node.new()
		tracker.set_script(script)

	tracker.set("Enabled", true)
	tracker.set("WindowFrames", 5)
	tracker.set("FlushIntervalSec", 0.0)

	get_tree().get_root().add_child(auto_free(tracker))
	await _wait_frames(10)

	assert_bool(FileAccess.file_exists(PERF_JSON_USER)).is_true()
	var json_text := _read_all_text(PERF_JSON_USER)
	assert_str(json_text).is_not_empty()

	var parsed = JSON.parse_string(json_text)
	assert_object(parsed).is_not_null()
	assert_bool(typeof(parsed) == TYPE_DICTIONARY).is_true()
	var dict: Dictionary = parsed

	assert_bool(dict.has("frames")).is_true()
	assert_bool(dict.has("avg_ms")).is_true()
	assert_bool(dict.has("p50_ms")).is_true()
	assert_bool(dict.has("p95_ms")).is_true()
	assert_bool(dict.has("p99_ms")).is_true()

	assert_bool(int(dict["frames"]) >= 2).is_true()

func test_user_logs_perf_path_is_globalizable() -> void:
	var abs_path := ProjectSettings.globalize_path("user://logs/perf")
	assert_str(abs_path).is_not_empty()
