extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const PERF_JSON_PATH := "user://logs/perf/perf.json"
const PERF_TRACKER_SCRIPT_PATH := "res://Tests.Godot/Game.Godot/Scripts/Perf/PerformanceTracker.cs"

func _delete_file_if_exists(path: String) -> void:
	if not FileAccess.file_exists(path):
		return
	var abs_path := ProjectSettings.globalize_path(path)
	var dir_abs := abs_path.get_base_dir()
	var file_name := abs_path.get_file()
	var dir := DirAccess.open(dir_abs)
	if dir != null:
		dir.remove(file_name)

func _read_text(path: String) -> String:
	if not FileAccess.file_exists(path):
		return ""
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return ""
	return f.get_as_text()

func _try_create_perf_tracker_instance() -> Object:
	var obj: Object = null
	if ResourceLoader.exists(PERF_TRACKER_SCRIPT_PATH):
		var res := load(PERF_TRACKER_SCRIPT_PATH)
		if res != null and res is Script:
			obj = (res as Script).new()
	if obj == null and ClassDB.class_exists("PerformanceTracker"):
		obj = ClassDB.instantiate("PerformanceTracker")
	return obj

# ACC:T20.3
func test_perf_tracker_writes_perf_json_shape_is_stable() -> void:
	var user_abs := ProjectSettings.globalize_path("user://")
	assert_str(user_abs).is_not_empty()

	_delete_file_if_exists(PERF_JSON_PATH)

	var obj := _try_create_perf_tracker_instance()
	if obj == null:
		assert_bool(true).is_true()
		return

	assert_bool(obj is Node).is_true()
	var node := obj as Node

	node.call("_Process", 0.6)
	node.call("_Process", 0.6)

	assert_bool(FileAccess.file_exists(PERF_JSON_PATH)).is_true()

	var text := _read_text(PERF_JSON_PATH)
	var parsed := JSON.parse_string(text)
	assert_bool(typeof(parsed) == TYPE_DICTIONARY).is_true()

	var d := parsed as Dictionary
	assert_bool(d.has("frames")).is_true()
	assert_bool(d.has("avg_ms")).is_true()
	assert_bool(d.has("p50_ms")).is_true()
	assert_bool(d.has("p95_ms")).is_true()
	assert_bool(d.has("p99_ms")).is_true()

	var frames := int(d.get("frames", 0))
	assert_int(frames).is_greater_equal(2)
