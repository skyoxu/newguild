extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: main route navigation.
## Menu -> Start -> Guild -> Activity -> Settings.

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

func _wait_for_child(root: Node, child_name: String, max_frames: int = 120) -> Node:
	for _i in range(max_frames):
		var node := root.get_node_or_null(child_name)
		if node != null:
			return node
		await get_tree().process_frame
	return null

func _wait_for_screen(main: Node, expected_name: String, max_frames: int = 240) -> Node:
	var screen_root := await _wait_for_child(main, "ScreenRoot", max_frames)
	if screen_root == null:
		return null
	for _i in range(max_frames):
		var found := screen_root.get_node_or_null(expected_name)
		if found != null:
			return found
		await get_tree().process_frame
	return null

func test_phase2_play_route_menu_to_screens() -> void:
	var main := await _spawn_main_on_root()

	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false

	var menu: Control = main.get_node("MainMenu")
	assert_bool(menu.visible).is_true()

	var btn_play: Button = menu.get_node("VBox/BtnPlay")
	var btn_guild: Button = menu.get_node("VBox/BtnGuild")
	var btn_activity: Button = menu.get_node("VBox/BtnActivity")
	var btn_settings: Button = menu.get_node("VBox/BtnSettings")

	btn_play.emit_signal("pressed")
	var start_screen := await _wait_for_screen(main, "StartScreen")
	assert_object(start_screen).is_not_null()

	menu.visible = true
	btn_guild.emit_signal("pressed")
	var guild_screen := await _wait_for_screen(main, "GuildScreen")
	assert_object(guild_screen).is_not_null()

	menu.visible = true
	btn_activity.emit_signal("pressed")
	var activity_screen := await _wait_for_screen(main, "ActivityFeedScreen")
	assert_object(activity_screen).is_not_null()

	# Settings is a panel under Main, not a screen.
	menu.visible = true
	btn_settings.emit_signal("pressed")
	await get_tree().process_frame
	var settings_panel := main.get_node_or_null("SettingsPanel")
	assert_object(settings_panel).is_not_null()
	if settings_panel is CanvasItem:
		assert_bool(settings_panel.visible).is_true()

