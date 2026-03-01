extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const MAIN_SCENE := "res://Game.Godot/Scenes/Main.tscn"
const START_SCREEN_SCENE := "res://Game.Godot/Scenes/Screens/StartScreen.tscn"
const EVENT_RAID_SCHEDULED := "core.raid.scheduled"
const EVENT_RAID_RESOLVED := "core.raid.resolved"
const EVENT_DEMO_SECURITY_DECISION := "core.security.raid_encounter_demo.decision"
const REQUIRED_AUDIT_KEYS := ["ts", "action", "reason", "target", "caller"]

var _prev_enable_playable := ""
var _prev_security_test_mode := ""
var _bus: Node = null
var _bus_cb: Callable = Callable()
var _events: Array[Dictionary] = []

func before() -> void:
	_prev_enable_playable = OS.get_environment("GD_ENABLE_PLAYABLE")
	_prev_security_test_mode = OS.get_environment("SECURITY_TEST_MODE")
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	OS.set_environment("SECURITY_TEST_MODE", "1")
	_ensure_root_node("EventBus", "res://Game.Godot/Adapters/EventBusAdapter.cs")
	_ensure_root_node("DataStore", "res://Game.Godot/Adapters/DataStoreAdapter.cs")
	await get_tree().process_frame

func after() -> void:
	_disconnect_bus()
	_cleanup_main()
	OS.set_environment("GD_ENABLE_PLAYABLE", _prev_enable_playable)
	OS.set_environment("SECURITY_TEST_MODE", _prev_security_test_mode)

func _cleanup_main() -> void:
	var existing := get_tree().root.get_node_or_null("Main")
	if existing != null:
		existing.queue_free()

func _ensure_root_node(node_name: String, script_path: String) -> Node:
	var existing := get_node_or_null("/root/" + node_name)
	if existing != null:
		return existing
	var script_res := load(script_path)
	assert_that(script_res).is_not_null()
	if script_res == null:
		return null
	var node: Node = script_res.new() as Node
	assert_object(node).is_not_null()
	if node == null:
		return null
	node.name = node_name
	get_tree().root.add_child(node)
	return node

func _spawn_start_screen() -> Node:
	_cleanup_main()
	await get_tree().process_frame
	var main_packed := load(MAIN_SCENE)
	assert_that(main_packed).is_not_null()
	var main: Node = main_packed.instantiate()
	main.name = "Main"
	get_tree().root.add_child(main)
	await get_tree().process_frame

	var screen_root := main.get_node_or_null("ScreenRoot")
	assert_object(screen_root).is_not_null()
	if screen_root == null:
		return null

	var start := screen_root.get_node_or_null("StartScreen")
	if start == null:
		var start_packed := load(START_SCREEN_SCENE)
		assert_that(start_packed).is_not_null()
		start = start_packed.instantiate()
		start.name = "StartScreen"
		screen_root.add_child(start)
		await get_tree().process_frame

	return start

func _on_domain_event(event_type: String, _source: String, _data_json: String, _id: String, _spec: String, _ct: String, _ts: String) -> void:
	_events.append({
		"type": event_type,
		"source": _source,
		"data_json": _data_json,
		"id": _id,
		"spec": _spec,
		"ct": _ct,
		"ts": _ts,
	})

func _connect_bus() -> Node:
	_events.clear()
	var bus := get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()
	if bus == null:
		return null
	_disconnect_bus()
	_bus = bus
	_bus_cb = Callable(self, "_on_domain_event")
	if not bus.is_connected("DomainEventEmitted", _bus_cb):
		bus.connect("DomainEventEmitted", _bus_cb)
	return bus

func _disconnect_bus() -> void:
	if _bus != null and _bus_cb.is_valid() and _bus.is_connected("DomainEventEmitted", _bus_cb):
		_bus.disconnect("DomainEventEmitted", _bus_cb)
	_bus = null
	_bus_cb = Callable()

func _count_event_type(event_type: String) -> int:
	var count := 0
	for event_item in _events:
		if str(event_item.get("type", "")) == event_type:
			count += 1
	return count

func _wait_for_event_count(event_type: String, min_count: int, frames: int = 360) -> bool:
	for _i in range(frames):
		if _count_event_type(event_type) >= min_count:
			return true
		await get_tree().process_frame
	return false

func _wait_for_output_contains(label: Label, token: String, frames: int = 360) -> bool:
	for _i in range(frames):
		if label != null and label.text.find(token) >= 0:
			return true
		await get_tree().process_frame
	return false

func _has_required_audit_fields(entry: Dictionary) -> bool:
	for key in REQUIRED_AUDIT_KEYS:
		if not entry.has(key):
			return false
		var value := str(entry.get(key, ""))
		if value.strip_edges().is_empty():
			return false
	return true

func _read_audit_entries(path: String) -> Dictionary:
	var result := {
		"entries": [],
		"parse_errors": 0,
	}
	if not FileAccess.file_exists(path):
		return result

	var text := FileAccess.get_file_as_string(path)
	for line in text.split("\n", false):
		var trimmed := line.strip_edges()
		if trimmed.is_empty():
			continue
		var parsed = JSON.parse_string(trimmed)
		if typeof(parsed) == TYPE_DICTIONARY:
			(result["entries"] as Array).append(parsed)
		else:
			result["parse_errors"] = int(result["parse_errors"]) + 1
	return result

func _wait_for_audit_entry(path: String, action: String, frames: int = 360) -> Dictionary:
	for _i in range(frames):
		var read_result := _read_audit_entries(path)
		if int(read_result.get("parse_errors", 0)) > 0:
			await get_tree().process_frame
			continue
		var entries = read_result.get("entries", []) as Array
		for entry in entries:
			if str(entry.get("action", "")) == action and _has_required_audit_fields(entry):
				return entry
		await get_tree().process_frame
	return {}

# ACC:T51.2
func test_acc_t51_2_startscreen_button_triggers_real_raid_and_updates_summary() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	var start := await _spawn_start_screen()
	assert_object(start).is_not_null()
	if start == null:
		return

	_connect_bus()
	var output := start.get_node("Center/VBox/Output") as Label
	var button := start.get_node("Center/VBox/BtnDemoRaid") as Button
	assert_object(output).is_not_null()
	assert_object(button).is_not_null()
	if output == null or button == null:
		return

	var before_text := output.text
	button.emit_signal("pressed")

	assert_bool(await _wait_for_event_count(EVENT_RAID_SCHEDULED, 1)).is_true()
	assert_bool(await _wait_for_event_count(EVENT_RAID_RESOLVED, 1, 480)).is_true()
	assert_bool(await _wait_for_output_contains(output, "Raid demo completed result=", 480)).is_true()

	assert_bool(output.text != before_text).is_true()
	assert_bool(output.text.find("Raid demo completed result=") >= 0).is_true()

func test_acc_t51_2_does_not_show_resolved_summary_when_demo_disabled() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "0")
	var start := await _spawn_start_screen()
	assert_object(start).is_not_null()
	if start == null:
		return

	_connect_bus()
	var output := start.get_node("Center/VBox/Output") as Label
	var button := start.get_node("Center/VBox/BtnDemoRaid") as Button
	assert_object(output).is_not_null()
	assert_object(button).is_not_null()
	if output == null or button == null:
		return

	button.emit_signal("pressed")
	await get_tree().process_frame
	assert_bool(output.text.find("Demos disabled.") >= 0).is_true()

	var resolved_before := _count_event_type(EVENT_RAID_RESOLVED)
	for _i in range(120):
		await get_tree().process_frame
	assert_int(_count_event_type(EVENT_RAID_RESOLVED)).is_equal(resolved_before)

# ACC:T51.8
func test_acc_t51_8_entry_button_is_reachable_visible_and_clickable() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	var start := await _spawn_start_screen()
	assert_object(start).is_not_null()
	if start == null:
		return

	var button := start.get_node("Center/VBox/BtnDemoRaid") as Button
	var output := start.get_node("Center/VBox/Output") as Label
	assert_object(button).is_not_null()
	assert_object(output).is_not_null()
	if button == null or output == null:
		return

	assert_bool(button.visible).is_true()
	assert_bool(button.disabled).is_false()
	assert_int(button.mouse_filter).is_not_equal(Control.MOUSE_FILTER_IGNORE)
	assert_bool(output.visible).is_true()

# ACC:T51.9
func test_acc_t51_9_output_changes_follow_domain_event_progression() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	var start := await _spawn_start_screen()
	assert_object(start).is_not_null()
	if start == null:
		return

	var bus := _connect_bus()
	var output := start.get_node("Center/VBox/Output") as Label
	var button := start.get_node("Center/VBox/BtnDemoRaid") as Button
	assert_object(bus).is_not_null()
	assert_object(output).is_not_null()
	assert_object(button).is_not_null()
	if bus == null or output == null or button == null:
		return

	var baseline := output.text
	bus.PublishSimple("core.guild.member.joined", "ut", "{\"guildId\":\"g1\"}")
	await get_tree().process_frame
	assert_str(output.text).is_equal(baseline)

	button.emit_signal("pressed")
	assert_bool(await _wait_for_event_count(EVENT_RAID_RESOLVED, 1, 480)).is_true()
	assert_bool(await _wait_for_output_contains(output, "Raid demo completed result=", 480)).is_true()

# ACC:T51.4
# ACC:T51.10
func test_acc_t51_4_and_t51_10_produces_replayable_logs_evidence() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	var start := await _spawn_start_screen()
	assert_object(start).is_not_null()
	if start == null:
		return

	_connect_bus()
	var button := start.get_node("Center/VBox/BtnDemoRaid") as Button
	assert_object(button).is_not_null()
	if button == null:
		return

	button.emit_signal("pressed")
	assert_bool(await _wait_for_event_count(EVENT_DEMO_SECURITY_DECISION, 1, 480)).is_true()
	assert_bool(await _wait_for_event_count(EVENT_RAID_RESOLVED, 1, 480)).is_true()

	var date_utc := Time.get_date_string_from_system(true)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	var entry := await _wait_for_audit_entry(audit_path, EVENT_DEMO_SECURITY_DECISION, 480)

	assert_bool(FileAccess.file_exists(audit_path)).is_true()
	assert_int(entry.size()).is_greater(0)
	assert_bool(_has_required_audit_fields(entry)).is_true()
	assert_str(str(entry.get("action", ""))).is_equal(EVENT_DEMO_SECURITY_DECISION)
