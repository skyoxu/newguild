extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: Guild UI reacts to domain events (minimum viable wiring).

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

func _open_guild_screen(main: Node) -> Node:
	var menu: Control = main.get_node("MainMenu")
	var btn_guild: Button = menu.get_node("VBox/BtnGuild")
	btn_guild.emit_signal("pressed")
	await get_tree().process_frame
	var screen_root: Node = main.get_node("ScreenRoot")
	for _i in range(180):
		var guild := screen_root.get_node_or_null("GuildScreen")
		if guild != null:
			return guild
		await get_tree().process_frame
	return null

func test_guild_panel_updates_on_created_and_officer_assigned_events() -> void:
	var main := await _spawn_main_on_root()
	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false
	var guild_screen := await _open_guild_screen(main)
	assert_object(guild_screen).is_not_null()

	var panel: Node = guild_screen.get_node("Scroll/GuildPanel")
	var status_label: Label = panel.get_node("Scroll/Margin/VBox/GuildInfo/StatusPanel/Root/Message")
	var bus := get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()

	bus.PublishSimple("core.guild.created", "GuildManager", '{"guildId":"g-play","creatorId":"u1","guildName":"PlayGuild","createdAt":"2025-01-01T00:00:00Z"}')
	await get_tree().process_frame
	assert_str(status_label.text).contains("Created")

	bus.PublishSimple("core.guild.officer.assigned", "GuildManager", '{"guildId":"g-play","userId":"u2","slot":"marshal"}')
	await get_tree().process_frame
	assert_str(status_label.text.to_lower()).contains("officer assigned")
