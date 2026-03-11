extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node

func before() -> void:
	_bus = _ensure_event_bus()
	assert_object(_bus).is_not_null()

func _ensure_event_bus() -> Node:
	var existing := get_node_or_null("/root/EventBus")
	if existing != null and existing.has_method("PublishSimple"):
		return existing
	var bus := preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	bus.name = "EventBus"
	get_tree().root.add_child(bus)
	return bus

func _spawn_activity_feed() -> Node:
	var scene := preload("res://Game.Godot/Scenes/Screens/ActivityFeedScreen.tscn").instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame
	return scene

# ACC:T33.1
func test_activity_feed_updates_on_domain_event() -> void:
	var feed_screen := await _spawn_activity_feed()
	var feed_label: RichTextLabel = feed_screen.get_node("Body/Scroll/Feed")
	var status_label: Label = feed_screen.get_node("Body/Status")
	var before_feed_text := feed_label.get_parsed_text()
	var before_status_text := status_label.text

	_bus.PublishSimple("core.raid.resolved", "ut", "{\"raidId\":\"r1\"}")
	await get_tree().process_frame

	assert_str(status_label.text).contains("Events:")
	assert_str(status_label.text).is_not_equal(before_status_text)
	assert_str(feed_label.get_parsed_text()).is_not_equal(before_feed_text)
	assert_str(feed_label.get_parsed_text()).contains("core.raid.resolved")
	assert_str(feed_label.get_parsed_text()).contains("[raid]")
