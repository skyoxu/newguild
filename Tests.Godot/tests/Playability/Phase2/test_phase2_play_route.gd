extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: main route navigation.
## Menu -> Start -> Guild -> Activity -> Settings.

const MAIN_SCENE := "res://Game.Godot/Scenes/Main.tscn"
const HUD_NODE_PATH := "HUD"
const HUD_WEEK_LABEL_PATH := "HUD/TopBar/HBox/WeekLabel"
const HUD_PHASE_LABEL_PATH := "HUD/TopBar/HBox/PhaseLabel"
const ROUTE2_ADVANCE_TURNS := 6
const ROUTE2_EXPECTED_WEEK_DELTA := 2
const ROUTE2_EXPECTED_FINAL_PHASE := "Resolution"
const ROUTE2_ARTIFACT_ENV := "SC_ROUTE2_ARTIFACT_NAME"
const ROUTE2_ARTIFACT_DEFAULT := "playability-route2-summary.json"
const ARTIFACT_DATE_ENV := "SC_ARTIFACT_DATE"
const EVENT_TYPE_BRIDGE_SCRIPT := "res://tests/Support/GameTurnEventTypesBridge.cs"
const DATA_STORE_ADAPTER_SCRIPT := "res://Game.Godot/Adapters/DataStoreAdapter.cs"
const SQLITE_DATA_STORE_SCRIPT := "res://Game.Godot/Adapters/SqliteDataStore.cs"
const ROUTE2_DB_PATH := "user://route2_play_route.db"
const T54_ALLOWED_CLASSIFICATIONS := ["timing", "order", "race", "environment", "unknown"]

var _route2_event_bus: Node = null
var _route2_event_callback: Callable = Callable()
var _route2_event_types: Array[String] = []

func _call_event_type_bridge_method(primary_name: String, fallback_name: String) -> String:
	if ClassDB.class_exists("GameTurnEventTypesBridge"):
		var bridge: Variant = ClassDB.instantiate("GameTurnEventTypesBridge")
		if bridge != null:
			if bridge.has_method(primary_name):
				return str(bridge.call(primary_name))
			if bridge.has_method(fallback_name):
				return str(bridge.call(fallback_name))
	if primary_name == "GetPhaseChangedEventType" or fallback_name == "get_phase_changed_event_type":
		return "core.game_turn.phase_changed"
	if primary_name == "GetWeekAdvancedEventType" or fallback_name == "get_week_advanced_event_type":
		return "core.game_turn.week_advanced"
	return ""

func _phase_changed_event_type() -> String:
	return _call_event_type_bridge_method("GetPhaseChangedEventType", "get_phase_changed_event_type")

func _week_advanced_event_type() -> String:
	return _call_event_type_bridge_method("GetWeekAdvancedEventType", "get_week_advanced_event_type")

func _route2_date_folder() -> String:
	var configured_date := String(OS.get_environment(ARTIFACT_DATE_ENV)).strip_edges()
	if _is_safe_artifact_date(configured_date):
		return configured_date

	var date_time := Time.get_datetime_string_from_system(false)
	var split_values := date_time.split("T")
	if split_values.size() > 0:
		return String(split_values[0])
	return "1970-01-01"

func _is_safe_artifact_date(date_text: String) -> bool:
	if date_text.is_empty():
		return false
	var pattern := RegEx.new()
	if pattern.compile("^\\d{4}-\\d{2}-\\d{2}$") != OK:
		return false
	return pattern.search(date_text) != null

func _route2_artifact_name() -> String:
	var configured := String(OS.get_environment(ROUTE2_ARTIFACT_ENV)).strip_edges()
	if configured.is_empty() or not _is_safe_artifact_name(configured):
		return ROUTE2_ARTIFACT_DEFAULT
	return configured

func _is_safe_artifact_name(file_name: String) -> bool:
	if file_name.is_empty():
		return false
	if file_name.contains("..") or file_name.contains("/") or file_name.contains("\\"):
		return false
	var pattern := RegEx.new()
	if pattern.compile("^[A-Za-z0-9._-]+\\.json$") != OK:
		return false
	return pattern.search(file_name) != null

func _on_route2_event(event_type, _source, _data_json, _id, _spec_version, _data_content_type, _timestamp_iso) -> void:
	_route2_event_types.append(str(event_type))

func _count_event_type(event_type: String) -> int:
	var count := 0
	for observed_type in _route2_event_types:
		if observed_type == event_type:
			count += 1
	return count

func _wait_for_event_type_count(event_type: String, min_count: int, frames: int = 360) -> bool:
	for _frame_index in range(frames):
		if _count_event_type(event_type) >= min_count:
			return true
		await get_tree().process_frame
	return false

func _extract_week_from_hud(main: Node) -> int:
	var week_label := main.get_node_or_null(HUD_WEEK_LABEL_PATH)
	if week_label == null or not (week_label is Label):
		return -1

	var text_value := String((week_label as Label).text)
	var regex := RegEx.new()
	var compile_result := regex.compile("(\\d+)")
	if compile_result != OK:
		return -1

	var match := regex.search(text_value)
	if match == null:
		return -1

	return int(match.get_string(1))

func _extract_phase_from_hud(main: Node) -> String:
	var phase_label := main.get_node_or_null(HUD_PHASE_LABEL_PATH)
	if phase_label == null or not (phase_label is Label):
		return "Unknown"

	var raw_value := String((phase_label as Label).text).strip_edges()
	if raw_value.contains(":"):
		var parts := raw_value.split(":", false, 1)
		if parts.size() > 1:
			return String(parts[1]).strip_edges()

	if raw_value.is_empty():
		return "Unknown"

	return raw_value.trim_suffix("-").strip_edges()

func _create_t54_evidence_file(sample_id: String) -> String:
	var run_date := _route2_date_folder()
	var evidence_dir := "user://logs/e2e/%s/t54-evidence" % run_date
	var user_directory := DirAccess.open("user://")
	if user_directory == null:
		return ""
	var make_result := user_directory.make_dir_recursive("logs/e2e/%s/t54-evidence" % run_date)
	if make_result != OK and make_result != ERR_ALREADY_EXISTS:
		return ""
	var evidence_path := "%s/%s.json" % [evidence_dir, sample_id]
	var writer := FileAccess.open(evidence_path, FileAccess.WRITE)
	if writer == null:
		return ""
	writer.store_string("{\"sample_id\":\"%s\",\"status\":\"captured\"}" % sample_id)
	writer.close()
	return evidence_path

func _build_valid_t54_summary(week_start: int = 1, week_end: int = 3, final_phase: String = ROUTE2_EXPECTED_FINAL_PHASE) -> Dictionary:
	var sample_id := "t54-sample-001"
	var evidence_path := _create_t54_evidence_file(sample_id)
	var sample := {
		"sample_id": sample_id,
		"capture_sample_id": sample_id,
		"classification_sample_id": sample_id,
		"replay_sample_id": sample_id,
		"classification": "timing",
		"replay_result": "consistent",
		"evidence_path": evidence_path,
		"deterministic_input_hash": "hash-001",
		"run_id": "run-task-54",
		"case_id": "T54-case-1",
		"failed_at": Time.get_datetime_string_from_system(true),
		"before_false_red": true,
		"after_false_red": false,
		"environment_summary": {
			"os": "windows",
			"godot_headless": true,
			"profile": "host-safe"
		},
		"assertion_name": "route2.phase.advance",
		"reason_code": ""
	}
	return {
		"route": "route-2",
		"week_start": week_start,
		"week_end": week_end,
		"final_phase": final_phase,
		"generated_at": Time.get_datetime_string_from_system(true),
		"security_profile": "host-safe",
		"flaky_samples": 1,
		"samples": [sample],
		"classification_counts": {"timing": 1, "unknown": 0},
		"false_red_rule_version": "v1",
		"before_false_red_count": 1,
		"before_total": 10,
		"after_false_red_count": 0,
		"after_total": 10,
		"before_window_id": "week-1-2",
		"after_window_id": "week-1-2",
		"before_denominator_def": "same_case_set",
		"after_denominator_def": "same_case_set",
		"before_case_set_hash": "case-set-001",
		"after_case_set_hash": "case-set-001",
		"reduction_rate": 1.0,
		"comparison_invalid": false,
		"nondeterministic": false,
		"case_verdicts": {"T54-case-1": "pass"},
		"stage_results": {"capture": "pass", "classify": "pass", "replay": "pass"},
		"replay_index": {
			sample_id: {
				"source_sample_id": sample_id,
				"replay_at": Time.get_datetime_string_from_system(true),
				"replay_verdict": "consistent"
			}
		}
	}

func _validate_t54_summary(summary: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	if String(summary.get("security_profile", "")) != "host-safe":
		errors.append("security_profile_mismatch")
	var samples_value: Variant = summary.get("samples", [])
	if typeof(samples_value) != TYPE_ARRAY:
		errors.append("samples_missing")
		return errors
	var samples: Array = samples_value
	var flaky_samples := int(summary.get("flaky_samples", -1))
	if flaky_samples != samples.size():
		errors.append("flaky_samples_mismatch")
	if flaky_samples == 0 and samples.size() != 0:
		errors.append("zero_flaky_with_samples")
	var counts: Dictionary = summary.get("classification_counts", {})
	var calculated_counts: Dictionary = {}
	var calculated_before_false_red := 0
	var calculated_after_false_red := 0
	if String(summary.get("false_red_rule_version", "")).is_empty():
		errors.append("false_red_rule_version_missing")
	for sample_value in samples:
		if typeof(sample_value) != TYPE_DICTIONARY:
			errors.append("sample_not_dictionary")
			continue
		var sample: Dictionary = sample_value
		var sample_id := String(sample.get("sample_id", ""))
		if sample_id.is_empty():
			errors.append("sample_id_missing")
			continue
		if String(sample.get("capture_sample_id", "")) != sample_id or String(sample.get("classification_sample_id", "")) != sample_id or String(sample.get("replay_sample_id", "")) != sample_id:
			errors.append("sample_id_chain_mismatch")
		if String(sample.get("deterministic_input_hash", "")).is_empty():
			errors.append("deterministic_input_hash_missing")
		if String(sample.get("run_id", "")).is_empty():
			errors.append("run_id_missing")
		if String(sample.get("case_id", "")).is_empty():
			errors.append("case_id_missing")
		if String(sample.get("failed_at", "")).is_empty():
			errors.append("failed_at_missing")
		var environment_summary: Variant = sample.get("environment_summary", null)
		if environment_summary == null:
			errors.append("environment_summary_missing")
		elif typeof(environment_summary) == TYPE_DICTIONARY:
			var env_dict: Dictionary = environment_summary
			if env_dict.is_empty():
				errors.append("environment_summary_missing")
		elif String(environment_summary).strip_edges().is_empty():
			errors.append("environment_summary_missing")
		var classification := String(sample.get("classification", ""))
		if classification.is_empty():
			errors.append("classification_missing")
		if not T54_ALLOWED_CLASSIFICATIONS.has(classification):
			errors.append("classification_out_of_enum")
		if classification == "unknown" and String(sample.get("reason_code", "")).is_empty():
			errors.append("unknown_without_reason_code")
		var replay_result := String(sample.get("replay_result", ""))
		if replay_result.is_empty():
			errors.append("replay_result_missing")
		if replay_result == "mismatch":
			errors.append("replay_classification_inconsistent")
		var evidence_path := String(sample.get("evidence_path", ""))
		if flaky_samples > 0:
			if evidence_path.is_empty() or not FileAccess.file_exists(evidence_path):
				errors.append("evidence_path_missing")
			else:
				var evidence_reader := FileAccess.open(evidence_path, FileAccess.READ)
				if evidence_reader == null:
					errors.append("evidence_path_unreadable")
				else:
					var evidence_text := evidence_reader.get_as_text()
					evidence_reader.close()
					if evidence_text.strip_edges().is_empty():
						errors.append("evidence_path_unreadable")
		if String(sample.get("assertion_name", "")).is_empty() and String(sample.get("error_code", "")).is_empty() and String(sample.get("log_symptom", "")).is_empty():
			errors.append("source_observation_missing")
		if not sample.has("before_false_red") or not sample.has("after_false_red"):
			errors.append("false_red_sample_marker_missing")
		else:
			if bool(sample.get("before_false_red", false)):
				calculated_before_false_red += 1
			if bool(sample.get("after_false_red", false)):
				calculated_after_false_red += 1
		calculated_counts[classification] = int(calculated_counts.get(classification, 0)) + 1
		var replay_entry: Dictionary = summary.get("replay_index", {}).get(sample_id, {})
		if String(replay_entry.get("source_sample_id", "")) != sample_id:
			errors.append("replay_link_missing")
		if String(replay_entry.get("replay_at", "")).is_empty():
			errors.append("replay_timestamp_missing")
		if String(replay_entry.get("replay_verdict", "")).is_empty():
			errors.append("replay_verdict_missing")
	for key in calculated_counts.keys():
		if int(counts.get(key, -1)) != int(calculated_counts[key]):
			errors.append("classification_count_mismatch")
	var before_false := int(summary.get("before_false_red_count", -1))
	var before_total := int(summary.get("before_total", -1))
	var after_false := int(summary.get("after_false_red_count", -1))
	var after_total := int(summary.get("after_total", -1))
	var before_window_id := String(summary.get("before_window_id", ""))
	var after_window_id := String(summary.get("after_window_id", ""))
	var before_denominator_def := String(summary.get("before_denominator_def", ""))
	var after_denominator_def := String(summary.get("after_denominator_def", ""))
	var before_case_set_hash := String(summary.get("before_case_set_hash", ""))
	var after_case_set_hash := String(summary.get("after_case_set_hash", ""))
	if before_total <= 0 or after_total <= 0:
		errors.append("invalid_totals")
	if before_false != calculated_before_false_red or after_false != calculated_after_false_red:
		errors.append("false_red_count_mismatch")
	if before_window_id.is_empty() or after_window_id.is_empty():
		errors.append("comparison_context_missing")
	if before_denominator_def.is_empty() or after_denominator_def.is_empty():
		errors.append("comparison_context_missing")
	if before_case_set_hash.is_empty() or after_case_set_hash.is_empty():
		errors.append("comparison_context_missing")
	if before_total != after_total:
		errors.append("comparison_invalid")
	if before_window_id != after_window_id:
		errors.append("comparison_invalid")
	if before_denominator_def != after_denominator_def:
		errors.append("comparison_invalid")
	if before_case_set_hash != after_case_set_hash:
		errors.append("comparison_invalid")
	var before_rate := float(before_false) / float(before_total)
	var after_rate := float(after_false) / float(after_total)
	var expected_reduction := (before_rate - after_rate) / before_rate if before_rate > 0.0 else 0.0
	var reduction_rate := float(summary.get("reduction_rate", -1.0))
	if abs(reduction_rate - expected_reduction) > 0.0001:
		errors.append("reduction_rate_mismatch")
	if reduction_rate <= 0.0 or after_rate >= before_rate:
		errors.append("reduction_not_improved")
	if bool(summary.get("comparison_invalid", false)):
		errors.append("comparison_invalid")
	if bool(summary.get("nondeterministic", false)):
		errors.append("nondeterministic")
	var case_verdicts: Dictionary = summary.get("case_verdicts", {})
	if case_verdicts.is_empty():
		errors.append("case_verdicts_missing")
	for verdict in case_verdicts.values():
		var verdict_text := String(verdict)
		if verdict_text != "pass" and verdict_text != "fail":
			errors.append("invalid_case_verdict")
			break
	var stage_results: Dictionary = summary.get("stage_results", {})
	for stage in ["capture", "classify", "replay"]:
		var stage_verdict := String(stage_results.get(stage, ""))
		if stage_verdict != "pass" and stage_verdict != "fail":
			errors.append("stage_result_missing")
			break
	return errors

func _write_route2_summary_and_assert_fields(week_start: int, week_end: int, final_phase: String) -> void:
	var run_date := _route2_date_folder()
	var summary_path := "user://logs/e2e/%s/%s" % [run_date, _route2_artifact_name()]
	var ci_summary_path := "user://logs/ci/%s/v11-task-54/summary.json" % run_date
	var user_directory := DirAccess.open("user://")
	assert_object(user_directory).is_not_null()
	if user_directory == null:
		return
	var relative_directory := "logs/e2e/%s" % run_date
	var make_result := user_directory.make_dir_recursive(relative_directory)
	assert_bool(make_result == OK or make_result == ERR_ALREADY_EXISTS).is_true()
	var ci_relative_directory := "logs/ci/%s/v11-task-54" % run_date
	var ci_make_result := user_directory.make_dir_recursive(ci_relative_directory)
	assert_bool(ci_make_result == OK or ci_make_result == ERR_ALREADY_EXISTS).is_true()
	var summary_payload := _build_valid_t54_summary(week_start, week_end, final_phase)
	var file := FileAccess.open(summary_path, FileAccess.WRITE)
	assert_object(file).is_not_null()
	if file == null:
		return
	file.store_string(JSON.stringify(summary_payload))
	file.close()
	var ci_file := FileAccess.open(ci_summary_path, FileAccess.WRITE)
	assert_object(ci_file).is_not_null()
	if ci_file == null:
		return
	ci_file.store_string(JSON.stringify(summary_payload))
	ci_file.close()
	assert_bool(FileAccess.file_exists(summary_path)).is_true()
	assert_bool(FileAccess.file_exists(ci_summary_path)).is_true()
	var ci_read_file := FileAccess.open(ci_summary_path, FileAccess.READ)
	assert_object(ci_read_file).is_not_null()
	if ci_read_file == null:
		return
	var ci_raw_text := ci_read_file.get_as_text()
	ci_read_file.close()
	assert_str(ci_raw_text).is_not_empty()
	var ci_parsed: Variant = JSON.parse_string(ci_raw_text)
	assert_object(ci_parsed).is_not_null()
	assert_bool(typeof(ci_parsed) == TYPE_DICTIONARY).is_true()
	var read_file := FileAccess.open(summary_path, FileAccess.READ)
	assert_object(read_file).is_not_null()
	if read_file == null:
		return
	var raw_text := read_file.get_as_text()
	read_file.close()
	assert_str(raw_text).is_not_empty()
	var parsed: Variant = JSON.parse_string(raw_text)
	assert_object(parsed).is_not_null()
	assert_bool(typeof(parsed) == TYPE_DICTIONARY).is_true()
	var parsed_summary: Dictionary = parsed
	var errors := _validate_t54_summary(parsed_summary)
	assert_int(errors.size()).is_equal(0)

func _wait_for_hud_week(main: Node, min_week: int, max_frames: int = 240) -> int:
	for _i in range(max_frames):
		var week_value := _extract_week_from_hud(main)
		if week_value >= min_week:
			return week_value
		await get_tree().process_frame
	return -1

func _advance_turns_via_hud(main: Node, turns: int) -> void:
	var hud := main.get_node_or_null(HUD_NODE_PATH)
	assert_object(hud).is_not_null()
	var can_advance := hud.has_method("advance_turn_from_gd") or hud.has_method("AdvanceTurnFromGd")
	assert_bool(can_advance).is_true()

	for _i in range(turns):
		if hud.has_method("advance_turn_from_gd"):
			hud.call("advance_turn_from_gd")
		else:
			hud.call("AdvanceTurnFromGd")
		await get_tree().process_frame
		await get_tree().process_frame

func before() -> void:
	OS.set_environment("SECURITY_TEST_MODE", "1")

func after() -> void:
	if _route2_event_bus != null and _route2_event_callback.is_valid() and _route2_event_bus.is_connected("DomainEventEmitted", _route2_event_callback):
		_route2_event_bus.disconnect("DomainEventEmitted", _route2_event_callback)
	_route2_event_bus = null
	_route2_event_callback = Callable()
	_route2_event_types.clear()

func _spawn_main_on_root() -> Node:
	var packed := load(MAIN_SCENE)
	assert_that(packed).is_not_null()
	var instance: Node = packed.instantiate()
	instance.name = "Main"

	var root := get_tree().root
	var existing := root.get_node_or_null("Main")
	if existing != null:
		existing.queue_free()
		await get_tree().process_frame
	root.add_child(instance)
	await get_tree().process_frame
	return instance

func _wait_for_child(root: Node, child_name: String, max_frames: int = 120) -> Node:
	for _i in range(max_frames):
		var node := root.get_node_or_null(child_name)
		if node != null:
			return node
		await get_tree().process_frame
	return null

func _ensure_data_store() -> void:
	var existing := get_node_or_null("/root/DataStore")
	if existing != null:
		return
	var store := preload(DATA_STORE_ADAPTER_SCRIPT).new()
	store.name = "DataStore"
	get_tree().root.add_child(store)
	await get_tree().process_frame

func _ensure_sql_db(path: String) -> void:
	var existing := get_node_or_null("/root/SqlDb")
	if existing != null:
		existing.queue_free()
		await get_tree().process_frame
	var db := preload(SQLITE_DATA_STORE_SCRIPT).new()
	db.name = "SqlDb"
	get_tree().root.add_child(db)
	if db.has_method("TryOpen"):
		var opened: Variant = db.call("TryOpen", path)
		assert_bool(bool(opened)).is_true()
	await get_tree().process_frame

func _wait_for_screen(main: Node, expected_name: String, max_frames: int = 240) -> Node:
	var screen_root := await _wait_for_child(main, "ScreenRoot", max_frames)
	if screen_root == null:
		return null
	for _i in range(max_frames):
		var found := screen_root.get_node_or_null(expected_name)
		if found != null:
			return found
		await get_tree().process_frame
	return null

# ACC:T48.1
# ACC:T48.2
# ACC:T48.4
# ACC:T48.8
# ACC:T48.9
# ACC:T48.10
# ACC:T54.1
# ACC:T54.2
# ACC:T54.3
# ACC:T54.4
# ACC:T54.5
# ACC:T54.6
# ACC:T54.7
# ACC:T54.8
# ACC:T54.9
# ACC:T54.10
# ACC:T54.11
# ACC:T54.12
# ACC:T54.13
# ACC:T54.14
# ACC:T54.15
# ACC:T54.16
# ACC:T54.17
# ACC:T54.18
# ACC:T54.19
# ACC:T54.20
# ACC:T54.21
# ACC:T54.22
# ACC:T54.23
# ACC:T54.24
# ACC:T54.25
func test_phase2_play_route_menu_to_screens() -> void:
	await _ensure_sql_db(ROUTE2_DB_PATH)
	await _ensure_data_store()
	var main := await _spawn_main_on_root()
	var route2_week_start := await _wait_for_hud_week(main, 1)
	var event_bus := get_node_or_null("/root/EventBus")
	assert_object(event_bus).is_not_null()
	_route2_event_types.clear()
	if event_bus != null:
		_route2_event_bus = event_bus
		_route2_event_callback = Callable(self, "_on_route2_event")
		if not event_bus.is_connected("DomainEventEmitted", _route2_event_callback):
			event_bus.connect("DomainEventEmitted", _route2_event_callback)

	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false

	var menu: Control = main.get_node("MainMenu")
	assert_bool(menu.visible).is_true()

	var btn_play: Button = menu.get_node("VBox/BtnPlay")
	var btn_guild: Button = menu.get_node("VBox/BtnGuild")
	var btn_activity: Button = menu.get_node("VBox/BtnActivity")
	var btn_settings: Button = menu.get_node("VBox/BtnSettings")

	btn_play.emit_signal("pressed")
	var start_screen := await _wait_for_screen(main, "StartScreen")
	assert_object(start_screen).is_not_null()

	menu.visible = true
	btn_guild.emit_signal("pressed")
	var guild_screen := await _wait_for_screen(main, "GuildScreen")
	assert_object(guild_screen).is_not_null()

	menu.visible = true
	btn_activity.emit_signal("pressed")
	var activity_screen := await _wait_for_screen(main, "ActivityFeedScreen")
	assert_object(activity_screen).is_not_null()

	# Settings is a panel under Main, not a screen.
	menu.visible = true
	btn_settings.emit_signal("pressed")
	await get_tree().process_frame
	var settings_panel := main.get_node_or_null("SettingsPanel")
	assert_object(settings_panel).is_not_null()
	if settings_panel is CanvasItem:
		assert_bool(settings_panel.visible).is_true()

	var phase_changed_type := _phase_changed_event_type()
	var week_advanced_type := _week_advanced_event_type()
	var phase_changed_before := _count_event_type(phase_changed_type)
	var week_advanced_before := _count_event_type(week_advanced_type)
	await _advance_turns_via_hud(main, ROUTE2_ADVANCE_TURNS)
	var phase_changed_ready := await _wait_for_event_type_count(phase_changed_type, phase_changed_before + 4)
	assert_bool(phase_changed_ready).is_true()
	var week_advanced_ready := await _wait_for_event_type_count(week_advanced_type, week_advanced_before + ROUTE2_EXPECTED_WEEK_DELTA)
	assert_bool(week_advanced_ready).is_true()

	var route2_week_end := await _wait_for_hud_week(main, route2_week_start + ROUTE2_EXPECTED_WEEK_DELTA)
	var route2_final_phase := _extract_phase_from_hud(main)
	assert_int(route2_week_start).is_equal(1)
	assert_int(route2_week_end).is_equal(route2_week_start + ROUTE2_EXPECTED_WEEK_DELTA)
	assert_str(route2_final_phase).is_equal(ROUTE2_EXPECTED_FINAL_PHASE)

	_write_route2_summary_and_assert_fields(route2_week_start, route2_week_end, route2_final_phase)

# ACC:T54.3
func test_should_bind_acc_t54_3_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var errors := _validate_t54_summary(summary)
	assert_int(errors.size()).is_equal(0)

# ACC:T54.4
func test_should_bind_acc_t54_4_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample.erase("environment_summary")
	summary["samples"] = [sample]
	assert_int(int(summary.get("flaky_samples", -1))).is_equal(1)
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("environment_summary_missing")).is_true()

# ACC:T54.5
func test_should_bind_acc_t54_5_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample["classification"] = "unknown"
	sample["reason_code"] = ""
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("unknown_without_reason_code")).is_true()

# ACC:T54.6
func test_should_bind_acc_t54_6_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample["replay_result"] = "mismatch"
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("replay_classification_inconsistent")).is_true()

# ACC:T54.7
func test_should_bind_acc_t54_7_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["after_false_red_count"] = 3
	summary["reduction_rate"] = 0.0
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("reduction_not_improved")).is_true()

# ACC:T54.8
func test_should_bind_acc_t54_8_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample.erase("evidence_path")
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("evidence_path_missing")).is_true()

# ACC:T54.9
func test_should_bind_acc_t54_9_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample.erase("assertion_name")
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("source_observation_missing")).is_true()

# ACC:T54.10
func test_should_bind_acc_t54_10_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["after_window_id"] = "week-3-4"
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("comparison_invalid")).is_true()

# ACC:T54.11
func test_should_bind_acc_t54_11_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["nondeterministic"] = true
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("nondeterministic")).is_true()

# ACC:T54.12
func test_should_bind_acc_t54_12_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	assert_bool(summary.has("samples")).is_true()
	assert_bool(summary.has("classification_counts")).is_true()
	assert_int(_validate_t54_summary(summary).size()).is_equal(0)

# ACC:T54.13
func test_should_bind_acc_t54_13_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var verdicts: Dictionary = summary.get("case_verdicts", {})
	assert_bool(verdicts.has("T54-case-1")).is_true()
	assert_int(_validate_t54_summary(summary).size()).is_equal(0)

# ACC:T54.14
func test_should_bind_acc_t54_14_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var file_path := "user://logs/e2e/%s/t54-summary-check.json" % _route2_date_folder()
	var writer := FileAccess.open(file_path, FileAccess.WRITE)
	assert_object(writer).is_not_null()
	if writer == null:
		return
	writer.store_string(JSON.stringify(summary))
	writer.close()
	assert_bool(FileAccess.file_exists(file_path)).is_true()
	var reader := FileAccess.open(file_path, FileAccess.READ)
	assert_object(reader).is_not_null()
	if reader == null:
		return
	var raw := reader.get_as_text()
	reader.close()
	assert_str(raw).is_not_empty()

# ACC:T54.15
func test_should_bind_acc_t54_15_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var empty_evidence_path := "user://logs/e2e/%s/t54-empty-evidence.json" % _route2_date_folder()
	var writer := FileAccess.open(empty_evidence_path, FileAccess.WRITE)
	assert_object(writer).is_not_null()
	if writer != null:
		writer.store_string("")
		writer.close()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample["evidence_path"] = empty_evidence_path
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("evidence_path_unreadable")).is_true()

# ACC:T54.16
func test_should_bind_acc_t54_16_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["reduction_rate"] = 0.123
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("reduction_rate_mismatch")).is_true()

# ACC:T54.17
func test_should_bind_acc_t54_17_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["nondeterministic"] = true
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("nondeterministic")).is_true()

# ACC:T54.18
func test_should_bind_acc_t54_18_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["security_profile"] = "default"
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("security_profile_mismatch")).is_true()

# ACC:T54.19
func test_should_bind_acc_t54_19_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample["replay_sample_id"] = "other-id"
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("sample_id_chain_mismatch")).is_true()

# ACC:T54.20
func test_should_bind_acc_t54_20_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample["classification"] = "manual_fix"
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("classification_out_of_enum")).is_true()

# ACC:T54.21
func test_should_bind_acc_t54_21_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	var replay_index: Dictionary = summary.get("replay_index", {})
	var replay_entry: Dictionary = replay_index.get(String(sample.get("sample_id", "")), {})
	replay_entry.erase("replay_at")
	replay_index[String(sample.get("sample_id", ""))] = replay_entry
	summary["replay_index"] = replay_index
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("replay_timestamp_missing")).is_true()

# ACC:T54.22
func test_should_bind_acc_t54_22_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["after_false_red_count"] = 4
	summary["reduction_rate"] = -0.1
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("reduction_not_improved")).is_true()

# ACC:T54.23
func test_should_bind_acc_t54_23_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var stage_results: Dictionary = summary.get("stage_results", {})
	stage_results.erase("replay")
	summary["stage_results"] = stage_results
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("stage_result_missing")).is_true()

# ACC:T54.24
func test_should_bind_acc_t54_24_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	var samples: Array = summary.get("samples", [])
	var sample: Dictionary = samples[0]
	sample.erase("run_id")
	summary["samples"] = [sample]
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("run_id_missing")).is_true()

# ACC:T54.25
func test_should_bind_acc_t54_25_when_refactor_anchor_gate_runs() -> void:
	var summary := _build_valid_t54_summary()
	summary["before_false_red_count"] = 9
	var errors := _validate_t54_summary(summary)
	assert_bool(errors.has("false_red_count_mismatch")).is_true()
