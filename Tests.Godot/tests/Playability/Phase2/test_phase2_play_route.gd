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

var _route2_event_bus: Node = null
var _route2_event_callback: Callable = Callable()
var _route2_event_types: Array[String] = []

func _call_event_type_bridge_method(primary_name: String, fallback_name: String) -> String:
	var bridge := preload(EVENT_TYPE_BRIDGE_SCRIPT).new()
	assert_object(bridge).is_not_null()
	if bridge == null:
		return ""
	if bridge.has_method(primary_name):
		return str(bridge.call(primary_name))
	if bridge.has_method(fallback_name):
		return str(bridge.call(fallback_name))
	assert_bool(false).is_true()
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

	return raw_value

func _write_route2_summary_and_assert_fields(week_start: int, week_end: int, final_phase: String) -> void:
	var run_date := _route2_date_folder()
	var summary_path := "user://logs/e2e/%s/%s" % [run_date, _route2_artifact_name()]
	var user_directory := DirAccess.open("user://")
	assert_object(user_directory).is_not_null()
	if user_directory == null:
		return
	var relative_directory := "logs/e2e/%s" % run_date
	var make_result := user_directory.make_dir_recursive(relative_directory)
	assert_bool(make_result == OK or make_result == ERR_ALREADY_EXISTS).is_true()

	var summary_payload := {
		"route": "route-2",
		"week_start": week_start,
		"week_end": week_end,
		"final_phase": final_phase,
		"generated_at": Time.get_datetime_string_from_system(true)
	}

	var file := FileAccess.open(summary_path, FileAccess.WRITE)
	assert_object(file).is_not_null()
	if file == null:
		return
	file.store_string(JSON.stringify(summary_payload))
	file.close()

	assert_bool(FileAccess.file_exists(summary_path)).is_true()

	var read_file := FileAccess.open(summary_path, FileAccess.READ)
	assert_object(read_file).is_not_null()
	if read_file == null:
		return
	var raw_text := read_file.get_as_text()
	read_file.close()

	var parsed := JSON.parse_string(raw_text)
	assert_object(parsed).is_not_null()
	assert_bool(typeof(parsed) == TYPE_DICTIONARY).is_true()

	var parsed_summary: Dictionary = parsed
	assert_bool(parsed_summary.has("route")).is_true()
	assert_bool(parsed_summary.has("week_start")).is_true()
	assert_bool(parsed_summary.has("week_end")).is_true()
	assert_bool(parsed_summary.has("final_phase")).is_true()
	assert_bool(parsed_summary.has("generated_at")).is_true()

	assert_str(String(parsed_summary["route"])).is_equal("route-2")
	assert_int(int(parsed_summary["week_start"])).is_equal(week_start)
	assert_int(int(parsed_summary["week_end"])).is_equal(week_end)
	assert_bool(week_end >= week_start).is_true()
	assert_str(String(parsed_summary["final_phase"])).is_equal(final_phase)
	assert_str(String(parsed_summary["generated_at"])).is_not_empty()

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
func test_phase2_play_route_menu_to_screens() -> void:
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

