extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const SCENE_PATH := "res://Scenes/Guild/TacticalCenter.tscn"
const EVENT_RAID_SCHEDULED := "core.raid.scheduled"
const EVENT_RAID_RESOLVED := "core.raid.resolved"

var _events: Array = []

func _spawn_scene() -> Node:
    var packed := load(SCENE_PATH)
    assert_that(packed).is_not_null()
    var instance := packed.instantiate()
    add_child(instance)
    await get_tree().process_frame
    return instance

func _connect_domain_events(scene: Node) -> void:
    _events.clear()
    var signal_names := ["domain_event_emitted", "domain_event_published", "domain_event"]
    var connected := false
    for name in signal_names:
        var err := scene.connect(name, Callable(self, "_on_domain_event"))
        if err == OK:
            connected = true
            break
    assert_that(connected).is_true()

func _on_domain_event(event_type: Variant, payload: Variant = null) -> void:
    if typeof(event_type) == TYPE_STRING:
        _events.append({"type": event_type, "payload": payload})
    else:
        _events.append(event_type)

func _has_event(event_type: String) -> bool:
    for e in _events:
        if typeof(e) == TYPE_DICTIONARY and e.has("type") and e["type"] == event_type:
            return true
        if typeof(e) == TYPE_STRING and e == event_type:
            return true
    return false

func _first_event(event_type: String) -> Variant:
    for e in _events:
        if typeof(e) == TYPE_DICTIONARY and e.get("type", "") == event_type:
            return e
        if typeof(e) == TYPE_STRING and e == event_type:
            return e
    return null

func _apply_minimal_formation(scene: Node, members: Array) -> void:
    var method_name := "set_minimal_formation"
    assert_that(scene.has_method(method_name)).is_true()
    scene.call(method_name, members)

func _apply_domain_event(scene: Node, event_type: String, payload: Dictionary) -> void:
    var method_name := "on_domain_event"
    assert_that(scene.has_method(method_name)).is_true()
    scene.call(method_name, event_type, payload)

func _find_raid_button(root: Node) -> Button:
    var button := _find_button_by_group(root, "raid_trigger")
    if button != null:
        return button
    return _find_button_by_name(root, "raid")

func _find_button_by_group(root: Node, group_name: String) -> Button:
    var tree := root.get_tree()
    if tree == null:
        return null
    for node in tree.get_nodes_in_group(group_name):
        if node is Button:
            return node
    return null

func _find_button_by_name(root: Node, name_hint: String) -> Button:
    var stack: Array = [root]
    var needle := name_hint.to_lower()
    while stack.size() > 0:
        var current: Node = stack.pop_back()
        if current is Button:
            var n := current.name.to_lower()
            if n.find(needle) >= 0:
                return current
        stack.append_array(current.get_children())
    return null

func _find_status_label(root: Node) -> Label:
    var stack: Array = [root]
    while stack.size() > 0:
        var current: Node = stack.pop_back()
        if current is Label:
            var n := current.name.to_lower()
            if n.find("status") >= 0 or n.find("state") >= 0:
                return current
        stack.append_array(current.get_children())
    return null

# ACC:T34.1
func test_entry_is_reachable_and_visible() -> void:
    var scene := await _spawn_scene()
    assert_that(scene.is_inside_tree()).is_true()
    if scene is CanvasItem:
        assert_that(scene.visible).is_true()
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    assert_that(raid_button.visible).is_true()
    assert_that(raid_button.disabled).is_false()
    assert_that(raid_button.mouse_filter).is_not_equal(Control.MOUSE_FILTER_IGNORE)

# ACC:T34.2
func test_minimal_formation_triggers_raid_schedule_event() -> void:
    var scene := await _spawn_scene()
    _connect_domain_events(scene)
    _apply_minimal_formation(scene, ["u1", "u2", "u3"])
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    await get_tree().process_frame
    assert_that(_has_event(EVENT_RAID_SCHEDULED)).is_true()

# ACC:T34.3
func test_invalid_formation_refuses_raid_schedule() -> void:
    var scene := await _spawn_scene()
    _connect_domain_events(scene)
    _apply_minimal_formation(scene, [])
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    await get_tree().process_frame
    assert_that(_has_event(EVENT_RAID_SCHEDULED)).is_false()

# ACC:T34.4
func test_raid_resolved_event_is_observed_by_ui() -> void:
    var scene := await _spawn_scene()
    var status_label := _find_status_label(scene)
    assert_that(status_label).is_not_null()
    var before := status_label.text
    _apply_domain_event(scene, EVENT_RAID_RESOLVED, {"raid_id": "r1"})
    await get_tree().process_frame
    assert_that(status_label.text).is_not_equal(before)

# ACC:T34.5
func test_raid_button_is_clickable_not_blocked() -> void:
    var scene := await _spawn_scene()
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    assert_that(raid_button.visible).is_true()
    assert_that(raid_button.disabled).is_false()
    assert_that(raid_button.mouse_filter).is_not_equal(Control.MOUSE_FILTER_IGNORE)

# ACC:T34.6
func test_domain_event_type_matches_contract_for_scheduled() -> void:
    var scene := await _spawn_scene()
    _connect_domain_events(scene)
    _apply_minimal_formation(scene, ["u1", "u2", "u3"])
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    await get_tree().process_frame
    var evt := _first_event(EVENT_RAID_SCHEDULED)
    assert_that(evt).is_not_null()
    if typeof(evt) == TYPE_DICTIONARY:
        assert_that(evt["type"]).is_equal(EVENT_RAID_SCHEDULED)

# ACC:T34.8
func test_status_updates_on_scheduled_domain_event() -> void:
    var scene := await _spawn_scene()
    var status_label := _find_status_label(scene)
    assert_that(status_label).is_not_null()
    var before := status_label.text
    _apply_domain_event(scene, EVENT_RAID_SCHEDULED, {"raid_id": "r1"})
    await get_tree().process_frame
    assert_that(status_label.text).is_not_equal(before)
    assert_that(status_label.text.to_lower()).contains("scheduled")
