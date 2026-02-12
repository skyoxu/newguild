extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE_PATH: String = "res://Game.Godot/Scenes/UI/HUD.tscn"
const EVENT_WEEK_ADVANCED: String = "core.game_turn.week_advanced"
const EVENT_PHASE_CHANGED: String = "core.game_turn.phase_changed"

var _previous_secure_mode: String = ""
var _previous_security_test_mode: String = ""
var _previous_enable_playable: String = ""
var _received_event_types: Array[String] = []


func _on_event(_type, _source, _data_json, _id, _spec_version, _data_content_type, _timestamp_iso) -> void:
	_received_event_types.append(str(_type))


func _count_event_type(event_type: String) -> int:
	var count := 0
	for current_type in _received_event_types:
		if current_type == event_type:
			count += 1
	return count


func _wait_for_event_count(event_type: String, min_count: int, frames: int = 240) -> bool:
	for _index in range(frames):
		if _count_event_type(event_type) >= min_count:
			return true
		await get_tree().process_frame
	return false


func _has_required_audit_fields(entry: Dictionary) -> bool:
	var required_keys := ["ts", "action", "reason", "target", "caller"]
	for key in required_keys:
		if not entry.has(key):
			return false
		var value: Variant = entry[key]
		if typeof(value) != TYPE_STRING:
			return false
		if str(value).strip_edges().is_empty():
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


func _wait_for_audit_entry(path: String, action: String, frames: int = 240) -> Dictionary:
	var parse_errors := 0
	for _index in range(frames):
		var read_result := _read_audit_entries(path)
		parse_errors = int(read_result.get("parse_errors", 0))
		var entries = read_result.get("entries", []) as Array
		if parse_errors > 0:
			await get_tree().process_frame
			continue
		for entry in entries:
			if str(entry.get("action", "")) == action and _has_required_audit_fields(entry):
				return entry
		await get_tree().process_frame
	if parse_errors > 0:
		return {"__parse_errors__": parse_errors}
	return {}


func _count_audit_entries(path: String, action: String) -> int:
	var read_result := _read_audit_entries(path)
	var entries = read_result.get("entries", []) as Array
	var count := 0
	for entry in entries:
		if str(entry.get("action", "")) == action and _has_required_audit_fields(entry):
			count += 1
	return count


func before() -> void:
	_previous_secure_mode = OS.get_environment("GD_SECURE_MODE")
	_previous_security_test_mode = OS.get_environment("SECURITY_TEST_MODE")
	_previous_enable_playable = OS.get_environment("GD_ENABLE_PLAYABLE")

	OS.set_environment("GD_SECURE_MODE", "1")
	OS.set_environment("SECURITY_TEST_MODE", "1")
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")


func after() -> void:
	OS.set_environment("GD_SECURE_MODE", _previous_secure_mode)
	OS.set_environment("SECURITY_TEST_MODE", _previous_security_test_mode)
	OS.set_environment("GD_ENABLE_PLAYABLE", _previous_enable_playable)


# ACC:T44.7
func test_entry_point_is_reachable_and_next_turn_button_is_clickable() -> void:
	var bus := get_node_or_null("/root/EventBus")
	if bus == null:
		bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
		bus.name = "EventBus"
		get_tree().get_root().add_child(auto_free(bus))

	var hud := preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(auto_free(hud))
	await get_tree().process_frame

	assert_bool(hud.visible).is_true()

	var next_turn_button := hud.get_node_or_null("TopBar/HBox/NextTurnButton") as Button
	assert_object(next_turn_button).is_not_null()
	if next_turn_button == null:
		return

	assert_bool(next_turn_button.disabled).is_false()
	assert_int(next_turn_button.mouse_filter).is_equal(Control.MOUSE_FILTER_STOP)

	var week_label := hud.get_node_or_null("TopBar/HBox/WeekLabel") as Label
	assert_object(week_label).is_not_null()
	if week_label == null:
		return

	next_turn_button.emit_signal("pressed")
	await get_tree().process_frame

	assert_str(week_label.text).contains("Week:")


# ACC:T44.8
func test_ui_state_changes_are_event_driven_by_real_event_bus() -> void:
	_received_event_types.clear()

	var bus := get_node_or_null("/root/EventBus")
	if bus == null:
		bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
		bus.name = "EventBus"
		get_tree().get_root().add_child(auto_free(bus))

	var callback := Callable(self, "_on_event")
	if not bus.is_connected("DomainEventEmitted", callback):
		bus.connect("DomainEventEmitted", callback)

	var hud := preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(auto_free(hud))
	await get_tree().process_frame

	var week_label := hud.get_node_or_null("TopBar/HBox/WeekLabel") as Label
	assert_object(week_label).is_not_null()
	if week_label == null:
		if bus.is_connected("DomainEventEmitted", callback):
			bus.disconnect("DomainEventEmitted", callback)
		return

	var before_text := week_label.text
	assert_bool(hud.has_method("AdvanceTurnFromGd")).is_true()
	if not hud.has_method("AdvanceTurnFromGd"):
		if bus.is_connected("DomainEventEmitted", callback):
			bus.disconnect("DomainEventEmitted", callback)
		return

	hud.AdvanceTurnFromGd()
	await get_tree().process_frame
	hud.AdvanceTurnFromGd()
	await get_tree().process_frame
	hud.AdvanceTurnFromGd()
	await get_tree().process_frame

	var got_week_advanced := await _wait_for_event_count(EVENT_WEEK_ADVANCED, 1)
	var got_phase_changed := await _wait_for_event_count(EVENT_PHASE_CHANGED, 1)
	assert_bool(got_week_advanced).is_true()
	assert_bool(got_phase_changed).is_true()

	var after_text := week_label.text
	assert_bool(after_text.begins_with("Week:")).is_true()
	assert_bool(after_text != before_text).is_true()
	assert_bool(_received_event_types.has(EVENT_WEEK_ADVANCED)).is_true()
	assert_bool(_received_event_types.has(EVENT_PHASE_CHANGED)).is_true()

	var date_utc := Time.get_date_string_from_system(true)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	var week_entry := await _wait_for_audit_entry(audit_path, EVENT_WEEK_ADVANCED)
	assert_bool(not week_entry.has("__parse_errors__")).is_true()
	assert_int(week_entry.size()).is_greater(0)
	assert_bool(_has_required_audit_fields(week_entry)).is_true()

	var phase_entry := await _wait_for_audit_entry(audit_path, EVENT_PHASE_CHANGED)
	assert_bool(not phase_entry.has("__parse_errors__")).is_true()
	assert_int(phase_entry.size()).is_greater(0)
	assert_bool(_has_required_audit_fields(phase_entry)).is_true()

	if bus.is_connected("DomainEventEmitted", callback):
		bus.disconnect("DomainEventEmitted", callback)


# ACC:T44.10
func test_eventbus_publishsimple_writes_week_advanced_audit_jsonl_with_required_fields() -> void:
	_received_event_types.clear()

	var bus := get_node_or_null("/root/EventBus")
	if bus == null:
		bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
		bus.name = "EventBus"
		get_tree().get_root().add_child(auto_free(bus))

	var callback := Callable(self, "_on_event")
	if not bus.is_connected("DomainEventEmitted", callback):
		bus.connect("DomainEventEmitted", callback)

	await get_tree().process_frame

	var date_utc := Time.get_date_string_from_system(true)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	var before_count := _count_audit_entries(audit_path, EVENT_WEEK_ADVANCED)

	var payload := {
		"reason": "week_advanced_event_published",
		"target": "save:task44_direct;week:2",
		"caller": "Task44DirectPublish",
	}
	var payload_json := JSON.stringify(payload)
	bus.PublishSimple(EVENT_WEEK_ADVANCED, "Task44DirectPublish", payload_json)

	var got_week_advanced := await _wait_for_event_count(EVENT_WEEK_ADVANCED, 1)
	assert_bool(got_week_advanced).is_true()

	var week_entry := await _wait_for_audit_entry(audit_path, EVENT_WEEK_ADVANCED)
	assert_bool(not week_entry.has("__parse_errors__")).is_true()
	assert_int(week_entry.size()).is_greater(0)
	assert_bool(_has_required_audit_fields(week_entry)).is_true()
	assert_str(str(week_entry.get("action", ""))).is_equal(EVENT_WEEK_ADVANCED)

	var after_count := _count_audit_entries(audit_path, EVENT_WEEK_ADVANCED)
	assert_bool(after_count > before_count).is_true()

	if bus.is_connected("DomainEventEmitted", callback):
		bus.disconnect("DomainEventEmitted", callback)
