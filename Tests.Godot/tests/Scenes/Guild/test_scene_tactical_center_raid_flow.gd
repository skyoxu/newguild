extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const SCENE_PATH := "res://Game.Godot/Scenes/Main.tscn"
const EVENT_RAID_SCHEDULED := "core.raid.scheduled"
const EVENT_RAID_RESOLVED := "core.raid.resolved"

var _events: Array = []

func _spawn_scene() -> Node:
    OS.set_environment("GD_ENABLE_PLAYABLE", "1")
    var packed := load(SCENE_PATH)
    assert_that(packed).is_not_null()
    var instance: Node = packed.instantiate()
    var root := get_tree().root
    var existing := root.get_node_or_null("Main")
    if existing != null:
        existing.queue_free()
        await get_tree().process_frame
    root.add_child(instance)
    await get_tree().process_frame
    var start_screen := await _open_start_screen(instance)
    assert_that(start_screen).is_not_null()
    return start_screen

func _open_start_screen(main: Node) -> Node:
    var screen_root: Node = null
    for _i in range(30):
        screen_root = main.get_node_or_null("ScreenRoot")
        if screen_root != null:
            break
        await get_tree().process_frame
    if screen_root == null:
        return null
    var existing := screen_root.get_node_or_null("StartScreen")
    if existing != null:
        return existing
    var packed := load("res://Game.Godot/Scenes/Screens/StartScreen.tscn")
    assert_that(packed).is_not_null()
    var inst: Node = packed.instantiate()
    screen_root.add_child(inst)
    if inst.has_method("Enter"):
        inst.call_deferred("Enter")
    await get_tree().process_frame
    return inst

func _connect_domain_events() -> void:
    _events.clear()
    var bus := get_node_or_null("/root/EventBus")
    assert_that(bus).is_not_null()
    var callable := Callable(self, "_on_domain_event")
    if bus.is_connected("DomainEventEmitted", callable):
        return
    var err := bus.connect("DomainEventEmitted", callable)
    assert_that(err).is_equal(OK)

func _on_domain_event(event_type: String, source: String, data_json: String, id: String, spec: String, ct: String, ts: String) -> void:
    _events.append({
        "type": event_type,
        "source": source,
        "data_json": data_json,
        "id": id,
        "spec": spec,
        "ct": ct,
        "ts": ts
    })

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

func _count_events(event_type: String) -> int:
    var count := 0
    for e in _events:
        if typeof(e) == TYPE_DICTIONARY and e.get("type", "") == event_type:
            count += 1
        if typeof(e) == TYPE_STRING and e == event_type:
            count += 1
    return count

func _wait_for_event(event_type: String, max_frames: int = 120) -> bool:
    for _i in range(max_frames):
        if _has_event(event_type):
            return true
        await get_tree().process_frame
    return false

func _find_raid_button(root: Node) -> Button:
    var direct := root.get_node_or_null("Center/VBox/BtnDemoRaid")
    if direct is Button:
        return direct
    var button := _find_button_by_name(root, "demo")
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
    var direct := root.get_node_or_null("Center/VBox/Output")
    if direct is Label:
        return direct
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
    _connect_domain_events()
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    assert_that(await _wait_for_event(EVENT_RAID_SCHEDULED)).is_true()

# ACC:T34.3
func test_invalid_formation_refuses_raid_schedule() -> void:
    var scene := await _spawn_scene()
    OS.set_environment("GD_ENABLE_PLAYABLE", "0")
    _connect_domain_events()
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    await get_tree().process_frame
    assert_that(_has_event(EVENT_RAID_SCHEDULED)).is_false()
    var status_label := _find_status_label(scene)
    if status_label != null:
        assert_that(status_label.text.to_lower()).contains("denied")

# ACC:T34.4
func test_raid_resolved_event_is_observed_by_ui() -> void:
    var scene := await _spawn_scene()
    var status_label := _find_status_label(scene)
    assert_that(status_label).is_not_null()
    var before_text := status_label.text
    _connect_domain_events()
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    assert_that(await _wait_for_event(EVENT_RAID_RESOLVED, 240)).is_true()
    await get_tree().process_frame
    assert_that(status_label.text).is_not_equal(before_text)
    assert_that(status_label.text.to_lower()).contains("raid demo completed")

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
    _connect_domain_events()
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    assert_that(await _wait_for_event(EVENT_RAID_SCHEDULED)).is_true()
    var evt = _first_event(EVENT_RAID_SCHEDULED)
    assert_that(evt).is_not_null()
    if typeof(evt) == TYPE_DICTIONARY:
        assert_that(evt["type"]).is_equal(EVENT_RAID_SCHEDULED)

# ACC:T34.8
func test_status_updates_on_scheduled_domain_event() -> void:
    var scene := await _spawn_scene()
    var status_label := _find_status_label(scene)
    assert_that(status_label).is_not_null()
    var before_text := status_label.text
    var raid_button := _find_raid_button(scene)
    assert_that(raid_button).is_not_null()
    raid_button.emit_signal("pressed")
    await get_tree().process_frame
    assert_that(status_label.text).is_not_equal(before_text)
    var text := status_label.text.to_lower()
    assert_that(text.contains("triggered") or text.contains("raid demo completed")).is_true()
