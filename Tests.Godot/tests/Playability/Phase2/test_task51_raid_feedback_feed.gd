extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const MAIN_SCENE := "res://Game.Godot/Scenes/Main.tscn"
const START_SCREEN_SCENE := "res://Game.Godot/Scenes/Screens/StartScreen.tscn"
const ACTIVITY_FEED_SCENE := "res://Game.Godot/Scenes/Screens/ActivityFeedScreen.tscn"
const EVENT_RAID_RESOLVED := "core.raid.resolved"
const EVENT_MEDIA_BEAT_TRIGGERED := "core.media.beat.triggered"
const EVENT_REPUTATION_CHANGED := "core.reputation.changed"

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
	OS.set_environment("GD_ENABLE_PLAYABLE", _prev_enable_playable)
	OS.set_environment("SECURITY_TEST_MODE", _prev_security_test_mode)

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

func _on_domain_event(event_type: String, source: String, data_json: String, id: String, spec: String, ct: String, ts: String) -> void:
	_events.append({
		"type": event_type,
		"source": source,
		"data_json": data_json,
		"id": id,
		"spec": spec,
		"ct": ct,
		"ts": ts,
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

func _count_event_type(event_type: String) -> int:
	var count := 0
	for event_item in _events:
		if str(event_item.get("type", "")) == event_type:
			count += 1
	return count

func _wait_for_event_count(event_type: String, min_count: int, frames: int = 480) -> bool:
	for _i in range(frames):
		if _count_event_type(event_type) >= min_count:
			return true
		await get_tree().process_frame
	return false

func _wait_feed_contains_tokens(feed: RichTextLabel, tokens: Array[String], frames: int = 480) -> bool:
	for _i in range(frames):
		var text := feed.get_parsed_text()
		var all_hit := true
		for token in tokens:
			if text.find(token) < 0:
				all_hit = false
				break
		if all_hit:
			return true
		await get_tree().process_frame
	return false

func _extract_result_from_payload(data_json: String) -> String:
	var parser := JSON.new()
	if parser.parse(data_json) != OK:
		return ""
	var parsed = parser.get_data()
	if parsed is Dictionary and parsed.has("result"):
		return str(parsed.get("result", ""))
	return ""

# ACC:T51.3
func test_acc_t51_3_feed_observes_raid_result_event() -> void:
	var main := await _spawn_main_on_root()
	var bus := _connect_bus()
	assert_object(bus).is_not_null()
	if bus == null:
		return

	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false

	var screen_root := main.get_node_or_null("ScreenRoot")
	assert_object(screen_root).is_not_null()
	if screen_root == null:
		return

	var activity := preload(ACTIVITY_FEED_SCENE).instantiate()
	activity.name = "ActivityFeedScreen"
	screen_root.add_child(activity)
	await get_tree().process_frame

	var start := preload(START_SCREEN_SCENE).instantiate()
	start.name = "StartScreen"
	screen_root.add_child(start)
	await get_tree().process_frame

	var btn_demo_raid: Button = start.get_node("Center/VBox/BtnDemoRaid")
	var btn_demo_media: Button = start.get_node("Center/VBox/BtnDemoMedia")
	var btn_demo_rep: Button = start.get_node("Center/VBox/BtnDemoReputation")
	var feed: RichTextLabel = activity.get_node("Body/Scroll/Feed")
	var output: Label = start.get_node("Center/VBox/Output")

	btn_demo_raid.emit_signal("pressed")
	assert_bool(await _wait_for_event_count(EVENT_RAID_RESOLVED, 1, 480)).is_true()

	var resolved_payload := ""
	for event_item in _events:
		if str(event_item.get("type", "")) == EVENT_RAID_RESOLVED:
			resolved_payload = str(event_item.get("data_json", ""))
			break
	assert_bool(resolved_payload.find("\"result\"") >= 0).is_true()
	var resolved_result := _extract_result_from_payload(resolved_payload)
	assert_bool(resolved_result == "success" or resolved_result == "failed").is_true()
	assert_bool(output.text.find("result=" + resolved_result) >= 0).is_true()
	var resolved_count_after_raid := _count_event_type(EVENT_RAID_RESOLVED)

	btn_demo_media.emit_signal("pressed")
	btn_demo_rep.emit_signal("pressed")

	assert_bool(await _wait_feed_contains_tokens(
		feed,
		[EVENT_MEDIA_BEAT_TRIGGERED, EVENT_REPUTATION_CHANGED, EVENT_RAID_RESOLVED],
		480)).is_true()
	assert_int(_count_event_type(EVENT_RAID_RESOLVED)).is_equal(resolved_count_after_raid)

	var final_text := feed.get_parsed_text()
	assert_str(final_text).contains(EVENT_MEDIA_BEAT_TRIGGERED)
	assert_str(final_text).contains(EVENT_REPUTATION_CHANGED)
	assert_str(final_text).contains(EVENT_RAID_RESOLVED)
