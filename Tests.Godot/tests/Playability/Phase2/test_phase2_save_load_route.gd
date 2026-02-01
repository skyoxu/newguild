extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: start screen Save+Load entry must work and emit expected events.

const MAIN_SCENE := "res://Game.Godot/Scenes/Main.tscn"

var _events: Array[String] = []

func before() -> void:
	OS.set_environment("SECURITY_TEST_MODE", "1")
	_events = []

func _ensure_autoload_node(script_path: String, node_name: String) -> void:
	var root := get_tree().get_root()
	var existing := root.get_node_or_null(node_name)
	if existing != null:
		return
	var script = load(script_path)
	assert_that(script).is_not_null()
	var created = script.new()
	assert_that(created).is_not_null()
	created.name = node_name
	root.add_child(auto_free(created))
	await get_tree().process_frame

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

func _connect_domain_events() -> void:
	var bus := get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()
	var cb := Callable(self, "_on_domain_event")
	if not bus.is_connected("DomainEventEmitted", cb):
		var err := bus.connect("DomainEventEmitted", cb)
		assert_int(err).is_equal(OK)

func _on_domain_event(type: String, _source: String, _data_json: String, _id: String, _spec: String, _ct: String, _ts: String) -> void:
	_events.append(str(type))

func _wait_for_start_screen(main: Node, max_frames: int = 240) -> Node:
	var screen_root: Node = main.get_node("ScreenRoot")
	for _i in range(max_frames):
		var start := screen_root.get_node_or_null("StartScreen")
		if start != null:
			return start
		await get_tree().process_frame
	return null

func test_save_load_emits_events_and_updates_status() -> void:
	var main := await _spawn_main_on_root()
	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false
	_connect_domain_events()

	# StartScreen expects /root/DataStore; ensure it exists in the test runtime.
	await _ensure_autoload_node("res://Game.Godot/Adapters/DataStoreAdapter.cs", "DataStore")

	var menu: Control = main.get_node("MainMenu")
	var btn_play: Button = menu.get_node("VBox/BtnPlay")
	btn_play.emit_signal("pressed")

	var start := await _wait_for_start_screen(main)
	assert_object(start).is_not_null()

	var btn_save_load: Button = start.get_node("Center/VBox/BtnSaveLoad")
	var output: Label = start.get_node("Center/VBox/Output")

	btn_save_load.emit_signal("pressed")

	var saw_save_requested := false
	var saw_save_completed := false
	var saw_load_requested := false
	var saw_load_completed := false

	for _i in range(300):
		await get_tree().process_frame
		saw_save_requested = saw_save_requested or _events.has("core.save.requested")
		saw_save_completed = saw_save_completed or _events.has("core.save.completed")
		saw_load_requested = saw_load_requested or _events.has("core.load.requested")
		saw_load_completed = saw_load_completed or _events.has("core.load.completed")
		if saw_save_requested and saw_load_requested and (saw_save_completed or saw_load_completed):
			break

	assert_bool(saw_save_requested).is_true()
	assert_bool(saw_load_requested).is_true()
	assert_str(output.text).contains("Save+Load")
