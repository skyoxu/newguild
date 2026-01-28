extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node

func before() -> void:
	_bus = get_node_or_null("/root/EventBus")
	assert_object(_bus).is_not_null()

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
	assert_str(feed_label.text).contains("Waiting for events")

	_bus.PublishSimple("core.raid.resolved", "ut", "{\"raidId\":\"r1\"}")
	await get_tree().process_frame

	assert_str(status_label.text).contains("Events: 1")
	assert_str(feed_label.text).contains("core.raid.resolved")
	assert_str(feed_label.text).contains("[raid]")
