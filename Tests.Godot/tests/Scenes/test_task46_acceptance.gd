extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const MAIN_SCENE_PATH := "res://Game.Godot/Scenes/Main.tscn"
const MAIN_MENU_SCENE_PATH := "res://Game.Godot/Scenes/UI/MainMenu.tscn"
const START_SCREEN_SCENE_PATH := "res://Game.Godot/Scenes/Screens/StartScreen.tscn"
const GUILD_SCREEN_SCENE_PATH := "res://Game.Godot/Scenes/Screens/GuildScreen.tscn"
const ACTIVITY_SCREEN_SCENE_PATH := "res://Game.Godot/Scenes/Screens/ActivityFeedScreen.tscn"
const MENU_TYPES := preload("res://Game.Godot/Scripts/UI/UiMenuEventTypes.gd")
const AUDIT_FILE_NAME := "security-audit.jsonl"
const SECURITY_AI_LOG_POPUP_EVENT_TYPE := "core.security.ai_log_popup.decision"
const REASON_DEMOS_DISABLED := "demos_disabled"

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

func _load_main_scene() -> Node:
    var root := get_tree().get_root()
    var existing_main := root.get_node_or_null("Main")
    if existing_main != null and is_instance_valid(existing_main):
        existing_main.queue_free()
        await get_tree().process_frame
        await get_tree().process_frame

    var main := preload(MAIN_SCENE_PATH).instantiate()
    root.add_child(main)
    auto_free(main)
    await get_tree().process_frame
    return main

func _current_screen(main: Node) -> Control:
    var root := main.get_node_or_null("ScreenRoot") as Control
    assert_object(root).is_not_null()
    if root == null:
        return Control.new()

    assert_int(root.get_child_count()).is_equal(1)
    if root.get_child_count() != 1:
        return Control.new()

    var screen := root.get_child(0) as Control
    assert_object(screen).is_not_null()
    if screen == null:
        return Control.new()

    return screen

func _switch_to(main: Node, scene_path: String) -> Control:
    var nav = main.get_node_or_null("ScreenNavigator")
    assert_object(nav).is_not_null()
    if nav == null:
        return Control.new()

    nav.UseFadeTransition = false
    var switched: bool = bool(nav.call("SwitchTo", scene_path))
    assert_bool(switched).is_true()
    if not switched:
        return Control.new()

    await get_tree().process_frame
    return _current_screen(main)

func _create_esc_event() -> InputEventKey:
    var event := InputEventKey.new()
    event.pressed = true
    event.keycode = KEY_ESCAPE
    return event

func _create_right_click_event() -> InputEventMouseButton:
    var event := InputEventMouseButton.new()
    event.pressed = true
    event.button_index = MOUSE_BUTTON_RIGHT
    return event

func _dispatch_unhandled_input(screen: Node, event: InputEvent) -> void:
    assert_bool(screen.has_method("_UnhandledInput")).is_true()
    if screen.has_method("_UnhandledInput"):
        screen.call("_UnhandledInput", event)

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

func _has_required_audit_fields(entry: Dictionary) -> bool:
    return entry.has("ts") and entry.has("action") and entry.has("reason") and entry.has("target") and entry.has("caller")

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

# ACC:T46.1
func test_modal_and_context_menu_state_changes_are_observable() -> void:
    var main := await _load_main_scene()
    var start_screen := await _switch_to(main, START_SCREEN_SCENE_PATH)
    assert_str(start_screen.name).is_equal("StartScreen")

    var event_log_popup := start_screen.get_node_or_null("EventLogPopup") as PopupPanel
    assert_object(event_log_popup).is_not_null()
    if event_log_popup == null:
        return

    assert_bool(event_log_popup.visible).is_false()

    _dispatch_unhandled_input(start_screen, _create_right_click_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_true()

    _dispatch_unhandled_input(start_screen, _create_right_click_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_false()

# ACC:T46.2
func test_escape_closes_modal_and_right_click_does_not_block_navigation() -> void:
    var main := await _load_main_scene()
    var start_screen := await _switch_to(main, START_SCREEN_SCENE_PATH)

    var event_log_popup := start_screen.get_node_or_null("EventLogPopup") as PopupPanel
    assert_object(event_log_popup).is_not_null()
    if event_log_popup == null:
        return

    _dispatch_unhandled_input(start_screen, _create_right_click_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_true()

    _dispatch_unhandled_input(start_screen, _create_esc_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_false()

    var open_guild_button := start_screen.get_node_or_null("Center/VBox/BtnOpenGuild") as Button
    assert_object(open_guild_button).is_not_null()
    if open_guild_button == null:
        return

    open_guild_button.emit_signal("pressed")
    var valid_names := ["GuildScreen", "GuildStartScreen"]
    var switched_to_valid_screen := false
    var root := main.get_node_or_null("ScreenRoot") as Control
    assert_object(root).is_not_null()
    if root == null:
        return

    for _i in range(30):
        for child in root.get_children():
            var screen := child as Control
            if screen != null and valid_names.has(screen.name):
                switched_to_valid_screen = true
                break
        if switched_to_valid_screen:
            break
        await get_tree().process_frame
    assert_bool(switched_to_valid_screen).is_true()

# ACC:T46.3
func test_modal_and_context_actions_are_idempotent() -> void:
    var main := await _load_main_scene()
    var start_screen := await _switch_to(main, START_SCREEN_SCENE_PATH)

    var event_log_popup := start_screen.get_node_or_null("EventLogPopup") as PopupPanel
    assert_object(event_log_popup).is_not_null()
    if event_log_popup == null:
        return

    assert_bool(event_log_popup.visible).is_false()

    _dispatch_unhandled_input(start_screen, _create_esc_event())
    await get_tree().process_frame
    _dispatch_unhandled_input(start_screen, _create_esc_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_false()

    _dispatch_unhandled_input(start_screen, _create_right_click_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_true()

    _dispatch_unhandled_input(start_screen, _create_esc_event())
    await get_tree().process_frame
    _dispatch_unhandled_input(start_screen, _create_esc_event())
    await get_tree().process_frame
    assert_bool(event_log_popup.visible).is_false()

# ACC:T46.4
func test_popup_interaction_keeps_screen_switch_deterministic() -> void:
    var main := await _load_main_scene()
    var start_screen := await _switch_to(main, START_SCREEN_SCENE_PATH)

    _dispatch_unhandled_input(start_screen, _create_right_click_event())
    await get_tree().process_frame

    var menu := main.get_node_or_null("MainMenu") as Control
    assert_object(menu).is_not_null()
    if menu != null:
        menu.call("HideMenu")
        assert_bool(menu.visible).is_false()

    var back_button := start_screen.get_node_or_null("Center/VBox/BtnBack") as Button
    assert_object(back_button).is_not_null()
    if back_button == null:
        return

    back_button.emit_signal("pressed")
    await get_tree().process_frame

    if menu != null:
        assert_object(menu).is_not_null()

    var root := main.get_node_or_null("ScreenRoot") as Control
    assert_object(root).is_not_null()
    if root != null:
        assert_bool(root.get_child_count() <= 1).is_true()

# ACC:T46.5
func test_navigation_entries_are_reachable_and_buttons_emit_menu_events() -> void:
    assert_object(_bus).is_not_null()
    if _bus == null:
        return

    var menu := preload(MAIN_MENU_SCENE_PATH).instantiate() as Control
    add_child(auto_free(menu))
    await get_tree().process_frame

    var btn_play := menu.get_node_or_null("VBox/BtnPlay") as Button
    var btn_guild := menu.get_node_or_null("VBox/BtnGuild") as Button
    var btn_activity := menu.get_node_or_null("VBox/BtnActivity") as Button
    var btn_settings := menu.get_node_or_null("VBox/BtnSettings") as Button

    assert_object(btn_play).is_not_null()
    assert_object(btn_guild).is_not_null()
    assert_object(btn_activity).is_not_null()
    assert_object(btn_settings).is_not_null()
    if btn_play == null or btn_guild == null or btn_activity == null or btn_settings == null:
        return

    assert_bool(btn_play.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()
    assert_bool(btn_guild.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()
    assert_bool(btn_activity.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()
    assert_bool(btn_settings.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()

    var baseline_events := _received_event_types.size()

    btn_play.emit_signal("pressed")
    await get_tree().process_frame
    assert_bool(menu.visible).is_false()

    menu.call("ShowMenu")
    btn_guild.emit_signal("pressed")
    await get_tree().process_frame
    assert_bool(menu.visible).is_false()

    menu.call("ShowMenu")
    btn_activity.emit_signal("pressed")
    await get_tree().process_frame
    assert_bool(menu.visible).is_false()

    menu.call("ShowMenu")
    btn_settings.emit_signal("pressed")
    await get_tree().process_frame
    assert_bool(menu.visible).is_true()

    var new_events: Array[String] = []
    for i in range(baseline_events, _received_event_types.size()):
        new_events.append(_received_event_types[i])

    assert_bool(new_events.has(MENU_TYPES.START)).is_true()
    assert_bool(new_events.has(MENU_TYPES.GUILD)).is_true()
    assert_bool(new_events.has(MENU_TYPES.ACTIVITY)).is_true()
    assert_bool(new_events.has(MENU_TYPES.SETTINGS)).is_true()

# ACC:T46.6
func test_right_click_does_not_open_ai_log_when_playable_is_disabled_and_audit_evidence_is_recorded() -> void:
    _set_env("GD_ENABLE_PLAYABLE", "0")
    _set_env("SECURITY_TEST_MODE", "1")

    var audit_path := _audit_path_for_today()
    var before_count := _read_jsonl_entries(audit_path).size()

    var start_screen := preload(START_SCREEN_SCENE_PATH).instantiate() as Control
    add_child(auto_free(start_screen))
    await get_tree().process_frame

    var baseline_events := _received_event_types.size()

    var event_log_popup := start_screen.get_node_or_null("EventLogPopup") as PopupPanel
    assert_object(event_log_popup).is_not_null()
    if event_log_popup != null:
        _dispatch_unhandled_input(start_screen, _create_right_click_event())
        await get_tree().process_frame
        assert_bool(event_log_popup.visible).is_false()

    var new_events: Array[String] = []
    for i in range(baseline_events, _received_event_types.size()):
        new_events.append(_received_event_types[i])
    assert_bool(new_events.has(SECURITY_AI_LOG_POPUP_EVENT_TYPE)).is_true()

    var entry := await _wait_for_audit_entry(
        audit_path,
        SECURITY_AI_LOG_POPUP_EVENT_TYPE,
        REASON_DEMOS_DISABLED,
        "ai-log-popup",
        "StartScreen")

    var after_count := _read_jsonl_entries(audit_path).size()
    assert_bool(after_count > before_count).is_true()
    assert_int(entry.size()).is_greater(0)
    assert_bool(_has_required_audit_fields(entry)).is_true()
    assert_str(str(entry.get("action", ""))).is_equal(SECURITY_AI_LOG_POPUP_EVENT_TYPE)
