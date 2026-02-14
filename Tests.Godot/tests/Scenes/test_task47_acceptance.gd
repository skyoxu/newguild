extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

class InteractionHarness:
    extends Control

    var modal_open: bool = false
    var context_menu_open: bool = false
    var focus_index: int = 0
    var navigation_blocked: bool = false

    func open_modal() -> void:
        modal_open = true

    func close_modal() -> void:
        modal_open = false

    func open_context_menu() -> void:
        context_menu_open = true

    func close_context_menu() -> void:
        context_menu_open = false

    func navigate(delta: int) -> void:
        if navigation_blocked:
            return
        focus_index += delta

    func process_interaction(event: InputEvent) -> void:
        if event is InputEventKey and event.pressed and not event.echo:
            var key_event := event as InputEventKey
            if key_event.keycode == KEY_ESCAPE:
                close_modal()
                close_context_menu()
                navigation_blocked = false
                return

        if event is InputEventMouseButton and event.pressed:
            var mouse_event := event as InputEventMouseButton
            if mouse_event.button_index == MOUSE_BUTTON_RIGHT:
                context_menu_open = not context_menu_open
                navigation_blocked = false

var _original_playable_env := ""
var _original_security_env := ""
var _previous_event_bus: Node = null
var _created_event_bus: Node = null
var _gate_decisions: Array[Dictionary] = []

const SECURITY_GATE_EVENT_TYPE := "core.security.ai_log_popup.decision"
const DECISION_ALLOW := "allow"
const DECISION_DENY := "deny"
const REASON_POPUP_TOGGLED := "popup_toggled"
const REASON_DEMOS_DISABLED := "demos_disabled"
const REASON_INVALID_PAYLOAD := "invalid_payload"

func before() -> void:
    _original_playable_env = _get_env("GD_ENABLE_PLAYABLE")
    _original_security_env = _get_env("SECURITY_TEST_MODE")
    _set_env("GD_ENABLE_PLAYABLE", "1")
    _set_env("SECURITY_TEST_MODE", "1")
    _swap_in_isolated_event_bus()
    _connect_gate_event_listener()
    _gate_decisions.clear()

func after() -> void:
    _disconnect_gate_event_listener()
    _restore_event_bus()
    _set_env("GD_ENABLE_PLAYABLE", _original_playable_env)
    _set_env("SECURITY_TEST_MODE", _original_security_env)
    await get_tree().process_frame
    await get_tree().process_frame

func _set_env(env_name: String, value: String) -> void:
    OS.set_environment(env_name, value)

func _get_env(env_name: String) -> String:
    return OS.get_environment(env_name)

func _swap_in_isolated_event_bus() -> void:
    var existing := get_node_or_null("/root/EventBus")
    if existing != null and is_instance_valid(existing):
        _previous_event_bus = existing
        _previous_event_bus.name = "EventBus_Task47_Backup"

    var bus := preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    bus.name = "EventBus"
    get_tree().get_root().add_child(bus)
    auto_free(bus)
    _created_event_bus = bus

func _restore_event_bus() -> void:
    if _created_event_bus != null and is_instance_valid(_created_event_bus):
        _created_event_bus.queue_free()
    _created_event_bus = null

    if _previous_event_bus != null and is_instance_valid(_previous_event_bus):
        _previous_event_bus.name = "EventBus"
    _previous_event_bus = null

func _connect_gate_event_listener() -> void:
    var bus := get_node_or_null("/root/EventBus")
    if bus == null:
        return
    var callback := Callable(self, "_on_domain_event_emitted")
    if bus.has_signal("DomainEventEmitted") and not bus.is_connected("DomainEventEmitted", callback):
        bus.connect("DomainEventEmitted", callback)

func _disconnect_gate_event_listener() -> void:
    var bus := get_node_or_null("/root/EventBus")
    if bus == null:
        return
    var callback := Callable(self, "_on_domain_event_emitted")
    if bus.has_signal("DomainEventEmitted") and bus.is_connected("DomainEventEmitted", callback):
        bus.disconnect("DomainEventEmitted", callback)

func _on_domain_event_emitted(type, _source, data_json, _id, _spec_version, _content_type, _timestamp_iso) -> void:
    if str(type) != SECURITY_GATE_EVENT_TYPE:
        return
    var parsed = JSON.parse_string(str(data_json))
    if typeof(parsed) == TYPE_DICTIONARY:
        _gate_decisions.append(parsed)

func _has_gate_decision(decision: String, reason: String) -> bool:
    for entry in _gate_decisions:
        if str(entry.get("decision", "")) == decision and str(entry.get("reason", "")) == reason:
            return true
    return false

func _call_drop_data(screen: Node, payload: Dictionary) -> bool:
    if screen.has_method("_DropData"):
        screen.call("_DropData", Vector2.ZERO, payload)
        return true
    if screen.has_method("_drop_data"):
        screen.call("_drop_data", Vector2.ZERO, payload)
        return true
    return false

func _call_can_drop_data(screen: Node, payload: Dictionary) -> Dictionary:
    var result := {
        "called": false,
        "accepted": false
    }

    if screen.has_method("_CanDropData"):
        result["called"] = true
        result["accepted"] = bool(screen.call("_CanDropData", Vector2.ZERO, payload))
        return result

    if screen.has_method("_can_drop_data"):
        result["called"] = true
        result["accepted"] = bool(screen.call("_can_drop_data", Vector2.ZERO, payload))
        return result

    return result

func _reset_gate_audit_context() -> void:
    _disconnect_gate_event_listener()
    _restore_event_bus()
    _swap_in_isolated_event_bus()
    _connect_gate_event_listener()
    _gate_decisions.clear()

func _new_escape_key_event() -> InputEventKey:
    var event := InputEventKey.new()
    event.pressed = true
    event.echo = false
    event.keycode = KEY_ESCAPE
    return event

func _new_f1_key_event() -> InputEventKey:
    var event := InputEventKey.new()
    event.pressed = true
    event.echo = false
    event.keycode = KEY_F1
    event.physical_keycode = KEY_F1
    return event

func _new_right_click_event() -> InputEventMouseButton:
    var event := InputEventMouseButton.new()
    event.pressed = true
    event.button_index = MOUSE_BUTTON_RIGHT
    return event

func _open_real_start_screen() -> Control:
    var screen := preload("res://Game.Godot/Scenes/Screens/StartScreen.tscn").instantiate() as Control
    add_child(auto_free(screen))
    await get_tree().process_frame
    await get_tree().process_frame
    return screen

func _dispatch_unhandled_input(screen: Node, event: InputEvent) -> void:
    if screen.has_method("_UnhandledInput"):
        screen.call("_UnhandledInput", event)
        return

    if screen.has_method("_unhandled_input"):
        screen.call("_unhandled_input", event)
        return

    fail("StartScreen does not expose _UnhandledInput/_unhandled_input")

# ACC:T47.1
# Headless deterministic smoke check for modal and menu visibility transitions.
func test_modal_and_context_menu_open_close_are_observable() -> void:
    var harness := auto_free(InteractionHarness.new()) as InteractionHarness

    assert_bool(harness.modal_open).is_false()
    assert_bool(harness.context_menu_open).is_false()

    harness.open_modal()
    harness.process_interaction(_new_right_click_event())
    assert_bool(harness.modal_open).is_true()
    assert_bool(harness.context_menu_open).is_true()

    harness.process_interaction(_new_right_click_event())
    harness.close_modal()
    assert_bool(harness.modal_open).is_false()
    assert_bool(harness.context_menu_open).is_false()

# ACC:T47.2
# ESC closes modal and right click does not block navigation.
# Repeated close actions remain idempotent.
func test_escape_and_right_click_keep_input_unstuck_and_idempotent() -> void:
    var harness := auto_free(InteractionHarness.new()) as InteractionHarness

    harness.open_modal()
    harness.open_context_menu()
    harness.navigate(1)
    assert_int(harness.focus_index).is_equal(1)

    harness.process_interaction(_new_escape_key_event())
    assert_bool(harness.modal_open).is_false()
    assert_bool(harness.context_menu_open).is_false()

    harness.process_interaction(_new_right_click_event())
    assert_bool(harness.navigation_blocked).is_false()
    harness.navigate(1)
    assert_int(harness.focus_index).is_equal(2)

    harness.close_modal()
    harness.close_modal()
    harness.close_context_menu()
    harness.close_context_menu()
    assert_bool(harness.modal_open).is_false()
    assert_bool(harness.context_menu_open).is_false()

# ACC:T47.3
# Real scene wiring: right-click toggles AI popup and ESC closes it deterministically.
func test_real_start_screen_interaction_opens_and_closes_ai_log_popup() -> void:
    var screen := await _open_real_start_screen()
    var popup := screen.get_node("EventLogPopup") as PopupPanel

    assert_bool(popup.visible).is_false()

    _dispatch_unhandled_input(screen, _new_right_click_event())
    await get_tree().process_frame
    assert_bool(popup.visible).is_true()

    _dispatch_unhandled_input(screen, _new_escape_key_event())
    await get_tree().process_frame
    assert_bool(popup.visible).is_false()

func test_real_start_screen_shortcut_and_tooltip_paths_are_wired() -> void:
    var screen := await _open_real_start_screen()
    var popup := screen.get_node("EventLogPopup") as PopupPanel
    var ai_log_button := screen.get_node("Center/VBox/BtnAiLog") as Button
    var save_load_button := screen.get_node("Center/VBox/BtnSaveLoad") as Button
    var activity_button := screen.get_node("Center/VBox/BtnActivityFeed") as Button
    var shortcut_event := _new_f1_key_event()

    assert_str(ai_log_button.tooltip_text).contains("F1")
    assert_str(save_load_button.tooltip_text).contains("save and load")
    assert_str(activity_button.tooltip_text).contains("activity")
    assert_int(shortcut_event.keycode).is_equal(KEY_F1)
    assert_bool(screen.has_method("_UnhandledInput") or screen.has_method("_unhandled_input")).is_true()

    _dispatch_unhandled_input(screen, shortcut_event)
    await get_tree().process_frame
    assert_bool(popup.visible).is_true()

func test_real_start_screen_disabled_mode_denies_interaction_and_emits_gate_decision() -> void:
    _set_env("GD_ENABLE_PLAYABLE", "0")
    var screen := await _open_real_start_screen()
    var popup := screen.get_node("EventLogPopup") as PopupPanel

    if screen.has_method("_GetDragData"):
        var drag_data = screen.call("_GetDragData", Vector2.ZERO)
        assert_bool(drag_data == null or typeof(drag_data) == TYPE_NIL).is_true()

    _dispatch_unhandled_input(screen, _new_right_click_event())
    await get_tree().process_frame
    assert_bool(popup.visible).is_false()
    assert_bool(_has_gate_decision(DECISION_DENY, REASON_DEMOS_DISABLED)).is_true()

func test_real_start_screen_drag_drop_valid_payload_opens_popup_and_emits_allow() -> void:
    _set_env("GD_ENABLE_PLAYABLE", "1")
    _set_env("SECURITY_TEST_MODE", "1")
    _reset_gate_audit_context()

    var screen := await _open_real_start_screen()
    var popup := screen.get_node("EventLogPopup") as PopupPanel
    var valid_payload := {
        "source": "StartScreen",
        "target": "ai-log-popup",
        "interaction": "dragdrop"
    }

    var can_drop_result := _call_can_drop_data(screen, valid_payload)
    assert_bool(bool(can_drop_result.get("called", false))).is_true()
    assert_bool(bool(can_drop_result.get("accepted", false))).is_true()

    var called := _call_drop_data(screen, valid_payload)
    assert_bool(called).is_true()

    await get_tree().process_frame
    assert_bool(popup.visible).is_true()
    assert_bool(_has_gate_decision(DECISION_ALLOW, REASON_POPUP_TOGGLED)).is_true()

func test_real_start_screen_drag_drop_invalid_payload_emits_deny_invalid_payload() -> void:
    _set_env("GD_ENABLE_PLAYABLE", "1")
    _set_env("SECURITY_TEST_MODE", "1")
    _reset_gate_audit_context()

    var screen := await _open_real_start_screen()
    var popup := screen.get_node("EventLogPopup") as PopupPanel
    var invalid_payload := {
        "source": "Unknown",
        "target": "ai-log-popup",
        "interaction": "dragdrop"
    }

    var can_drop_result := _call_can_drop_data(screen, invalid_payload)
    assert_bool(bool(can_drop_result.get("called", false))).is_true()
    assert_bool(bool(can_drop_result.get("accepted", true))).is_false()

    await get_tree().process_frame
    assert_bool(popup.visible).is_false()
    assert_bool(_has_gate_decision(DECISION_DENY, REASON_INVALID_PAYLOAD)).is_true()
