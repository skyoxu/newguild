extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const START_SCREEN_SCENE_PATH := "res://Game.Godot/Scenes/Screens/StartScreen.tscn"
const AUDIT_FILE_NAME := "security-audit.jsonl"
const SECURITY_AI_LOG_POPUP_EVENT_TYPE := "core.security.ai_log_popup.decision"
const REASON_POPUP_OPENED := "popup_opened"

var _bus = null
var _owns_bus := false
var _received_event_types: Array[String] = []
var _original_playable_env := ""
var _original_security_env := ""

func before() -> void:
    _received_event_types.clear()
    _original_playable_env = _get_env("GD_ENABLE_PLAYABLE")
    _original_security_env = _get_env("SECURITY_TEST_MODE")
    _set_env("GD_ENABLE_PLAYABLE", "1")
    _set_env("SECURITY_TEST_MODE", "1")
    _ensure_event_bus()

func after() -> void:
    _set_env("GD_ENABLE_PLAYABLE", _original_playable_env)
    _set_env("SECURITY_TEST_MODE", _original_security_env)

    if _bus != null and is_instance_valid(_bus):
        var callback := Callable(self, "_on_domain_event_emitted")
        if _bus.has_signal("DomainEventEmitted") and _bus.is_connected("DomainEventEmitted", callback):
            _bus.disconnect("DomainEventEmitted", callback)

    if _owns_bus and _bus != null and is_instance_valid(_bus):
        _bus.queue_free()

    _bus = null
    _owns_bus = false

    await get_tree().process_frame
    await get_tree().process_frame

func _ensure_event_bus() -> void:
    var existing = get_node_or_null("/root/EventBus")
    if existing != null:
        _bus = existing
    else:
        var has_adapter := ClassDB.class_exists("EventBusAdapter")
        assert_bool(has_adapter).is_true()
        if not has_adapter:
            return

        _bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
        _bus.name = "EventBus"
        get_tree().get_root().add_child(_bus)
        auto_free(_bus)
        _owns_bus = true

    assert_object(_bus).is_not_null()
    if _bus == null:
        return

    var callback := Callable(self, "_on_domain_event_emitted")
    if _bus.has_signal("DomainEventEmitted") and not _bus.is_connected("DomainEventEmitted", callback):
        _bus.connect("DomainEventEmitted", callback)

func _on_domain_event_emitted(type, _source, _data_json, _id, _spec_version, _content_type, _timestamp_iso) -> void:
    _received_event_types.append(str(type))

func _set_env(name: String, value: String) -> void:
    OS.set_environment(name, value)

func _get_env(name: String) -> String:
    return OS.get_environment(name)

func _audit_path_for_today() -> String:
    var date_utc := Time.get_date_string_from_system(true)
    return "user://logs/ci/%s/%s" % [date_utc, AUDIT_FILE_NAME]

func _read_jsonl_entries(path: String) -> Array[Dictionary]:
    var entries: Array[Dictionary] = []
    if not FileAccess.file_exists(path):
        return entries

    var file := FileAccess.open(path, FileAccess.READ)
    if file == null:
        return entries

    while not file.eof_reached():
        var line := file.get_line().strip_edges()
        if line == "":
            continue
        var parsed = JSON.parse_string(line)
        if typeof(parsed) == TYPE_DICTIONARY:
            entries.append(parsed)

    return entries

func _entry_matches(entry: Dictionary, action: String, reason: String, target: String, caller: String) -> bool:
    if str(entry.get("action", "")) != action:
        return false
    var entry_reason := str(entry.get("reason", ""))
    if entry_reason.begins_with("claim:"):
        entry_reason = entry_reason.substr(6)
    if reason != "" and entry_reason != reason:
        return false
    var entry_target := str(entry.get("target", ""))
    if entry_target.begins_with("claim:"):
        entry_target = entry_target.substr(6)
    if target != "" and entry_target != target:
        return false
    var entry_caller := str(entry.get("caller", ""))
    if entry_caller.begins_with("claim:"):
        entry_caller = entry_caller.substr(6)
    if caller != "" and entry_caller != caller:
        return false
    return true

func _wait_for_audit_entry(path: String, action: String, reason: String = "", target: String = "", caller: String = "", max_frames: int = 240) -> Dictionary:
    for _i in range(max_frames):
        var entries := _read_jsonl_entries(path)
        for j in range(entries.size() - 1, -1, -1):
            var entry := entries[j]
            if _entry_matches(entry, action, reason, target, caller):
                return entry
        await get_tree().process_frame

    return {}

# ACC:T46.7
func test_ai_log_button_path_emits_gate_decision_audit_when_demos_are_enabled() -> void:
    _set_env("GD_ENABLE_PLAYABLE", "1")
    _set_env("SECURITY_TEST_MODE", "1")

    var audit_path := _audit_path_for_today()
    var before_count := _read_jsonl_entries(audit_path).size()

    var start_screen := preload(START_SCREEN_SCENE_PATH).instantiate() as Control
    add_child(auto_free(start_screen))
    await get_tree().process_frame

    var baseline_events := _received_event_types.size()

    var ai_log_button := start_screen.get_node_or_null("Center/VBox/BtnAiLog") as Button
    assert_object(ai_log_button).is_not_null()
    if ai_log_button == null:
        return

    assert_bool(ai_log_button.visible).is_true()

    ai_log_button.emit_signal("pressed")
    await get_tree().process_frame

    var new_events: Array[String] = []
    for i in range(baseline_events, _received_event_types.size()):
        new_events.append(_received_event_types[i])
    assert_bool(new_events.has(SECURITY_AI_LOG_POPUP_EVENT_TYPE)).is_true()

    var entry := await _wait_for_audit_entry(
        audit_path,
        SECURITY_AI_LOG_POPUP_EVENT_TYPE,
        REASON_POPUP_OPENED,
        "ai-log-popup",
        "StartScreen")

    var after_count := _read_jsonl_entries(audit_path).size()
    assert_bool(after_count > before_count).is_true()
    assert_int(entry.size()).is_greater(0)
    assert_bool(entry.has("ts") and entry.has("action") and entry.has("reason") and entry.has("target") and entry.has("caller")).is_true()
