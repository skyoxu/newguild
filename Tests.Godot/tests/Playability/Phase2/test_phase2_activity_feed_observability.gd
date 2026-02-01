extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: activity feed must show allowlisted events.

const MAIN_SCENE := "res://Game.Godot/Scenes/Main.tscn"

func before() -> void:
	OS.set_environment("SECURITY_TEST_MODE", "1")

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

func _open_activity_feed(main: Node) -> Node:
	var menu: Control = main.get_node("MainMenu")
	var btn_activity: Button = menu.get_node("VBox/BtnActivity")
	btn_activity.emit_signal("pressed")
	await get_tree().process_frame
	var screen_root: Node = main.get_node("ScreenRoot")
	for _i in range(180):
		var activity := screen_root.get_node_or_null("ActivityFeedScreen")
		if activity != null:
			# Ensure the screen had a chance to run _Ready() and subscribe to the EventBus.
			await get_tree().process_frame
			return activity
		await get_tree().process_frame
	return null

func test_activity_feed_displays_allowlisted_event_type() -> void:
	var main := await _spawn_main_on_root()
	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false
	var activity := await _open_activity_feed(main)
	assert_object(activity).is_not_null()

	var feed: RichTextLabel = activity.get_node("Body/Scroll/Feed")
	var bus := get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()

	# Wait until the screen has rendered its initial "waiting" text.
	for _i in range(180):
		await get_tree().process_frame
		if not String(feed.get_parsed_text()).is_empty():
			break
	assert_str(feed.get_parsed_text()).contains("Waiting for events")

	# Publish an allowlisted event and verify it appears in the feed.
	bus.PublishSimple("core.save.requested", "ui", '{"saveId":"playability"}')
	for _i in range(180):
		await get_tree().process_frame
		if feed.get_parsed_text().find("core.save.requested") >= 0:
			assert_bool(true).is_true()
			return

	assert_str(feed.get_parsed_text()).contains("core.save.requested")
