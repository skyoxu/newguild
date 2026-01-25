extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

# Task 30 - Stage 2: UI responsive layout and clickability closure (Scroll/Anchor/MouseFilter)
# Intentional RED-first suite: at least one test is expected to fail until the UI policy is implemented.

const TASK_ID := 30
const TASK_TITLE := "Stage2: UI responsive layout and clickability closure (Scroll/Anchor/MouseFilter)"

var _tracked_nodes: Array = []

const SCENE_MAIN := "res://Game.Godot/Scenes/Main.tscn"
const SCENE_GUILD_PANEL := "res://Game.Godot/Scenes/UI/GuildPanel.tscn"

func after_test() -> void:
	for tracked_node in _tracked_nodes:
		if is_instance_valid(tracked_node):
			tracked_node.queue_free()
	_tracked_nodes.clear()

func _track(node: Node) -> Node:
	_tracked_nodes.append(node)
	return node

func _date_yyyy_mm_dd() -> String:
	var date_dict := Time.get_datetime_dict_from_system()
	return "%04d-%02d-%02d" % [int(date_dict.year), int(date_dict.month), int(date_dict.day)]

func _get_default_viewport_size() -> Vector2:
	var tree := get_tree()
	if tree == null:
		return Vector2(1280, 720)

	var root_window := tree.root
	if root_window != null and root_window is Window:
		var window_size: Vector2i = root_window.size
		if window_size.x > 0 and window_size.y > 0:
			return Vector2(float(window_size.x), float(window_size.y))

	return Vector2(1280, 720)

func _await_gui_settle(frames: int = 2) -> void:
	var tree := get_tree()
	if tree == null:
		return
	for frame_index in range(frames):
		await tree.process_frame

func _make_ui_root() -> Control:
	var viewport_size := _get_default_viewport_size()

	var ui_root := Control.new()
	ui_root.name = "UiRoot"
	ui_root.size = viewport_size
	ui_root.custom_minimum_size = viewport_size
	ui_root.mouse_filter = Control.MOUSE_FILTER_IGNORE

	get_tree().root.add_child(_track(ui_root))	
	return ui_root

func _instantiate_under(parent: Node, packed_scene_path: String) -> Node:
	var packed_scene := load(packed_scene_path)
	assert(packed_scene != null, "Cannot load scene: %s" % packed_scene_path)
	assert(packed_scene is PackedScene, "Expected PackedScene at: %s" % packed_scene_path)
	var node := (packed_scene as PackedScene).instantiate()
	parent.add_child(_track(node))
	return node

func _has_scroll_container(node: Node) -> bool:
	for child_node in node.get_children():
		if child_node is ScrollContainer:
			return true
		if _has_scroll_container(child_node):
			return true
	return false

func _find_first_scroll_container(node: Node) -> ScrollContainer:
	for child_node in node.get_children():
		if child_node is ScrollContainer:
			return child_node as ScrollContainer
		var nested := _find_first_scroll_container(child_node)
		if nested != null:
			return nested
	return null

func _collect_controls(node: Node, out_controls: Array[Control]) -> void:
	if node is Control:
		out_controls.append(node as Control)
	for child_node in node.get_children():
		_collect_controls(child_node, out_controls)

func _find_mouse_blocker_at_point(root: Control, global_point: Vector2, click_target: Control) -> Control:
	var controls: Array[Control] = []
	_collect_controls(root, controls)

	for control_node in controls:
		if control_node == click_target:
			continue
		if not control_node.visible:
			continue
		if control_node.mouse_filter != Control.MOUSE_FILTER_STOP:
			continue
		# Ancestors/descendants should not be treated as "overlay blockers" here.
		if control_node.is_ancestor_of(click_target) or click_target.is_ancestor_of(control_node):
			continue
		var rect := control_node.get_global_rect()
		if rect.has_point(global_point):
			return control_node

	return null

# ACC:T30.1
# Responsive operation: overflow content must be operable inside the default window size.
# The Guild UI must be scrollable when content exceeds the viewport.
func test_responsive_layout_requires_scroll_when_overflow() -> void:
	var ui_root := _make_ui_root()

	var guild_panel := _instantiate_under(ui_root, SCENE_GUILD_PANEL)
	assert(_has_scroll_container(guild_panel), "Expected GuildPanel to include a ScrollContainer for overflow content")

# ACC:T30.2
# Responsive layout: resizable panels should use full-rect anchors and zero offsets.
func test_responsive_layout_full_rect_anchors_for_resizable_panel() -> void:
	var ui_root := _make_ui_root()

	var guild_panel := _instantiate_under(ui_root, SCENE_GUILD_PANEL)
	await _await_gui_settle(2)

	var sc := _find_first_scroll_container(guild_panel)
	assert(sc != null, "Expected ScrollContainer in GuildPanel for anchor validation")
	assert(is_equal_approx(sc.anchor_left, 0.0), "Expected ScrollContainer anchor_left == 0")
	assert(is_equal_approx(sc.anchor_top, 0.0), "Expected ScrollContainer anchor_top == 0")
	assert(is_equal_approx(sc.anchor_right, 1.0), "Expected ScrollContainer anchor_right == 1")
	assert(is_equal_approx(sc.anchor_bottom, 1.0), "Expected ScrollContainer anchor_bottom == 1")
	assert(is_equal_approx(sc.offset_left, 0.0), "Expected ScrollContainer offset_left == 0")
	assert(is_equal_approx(sc.offset_top, 0.0), "Expected ScrollContainer offset_top == 0")
	assert(is_equal_approx(sc.offset_right, 0.0), "Expected ScrollContainer offset_right == 0")
	assert(is_equal_approx(sc.offset_bottom, 0.0), "Expected ScrollContainer offset_bottom == 0")

# ACC:T30.3
# Clickability: a click target must not be blocked by overlays using MOUSE_FILTER_STOP.
func test_clickability_button_not_blocked_by_overlay() -> void:
	var ui_root := _make_ui_root()
	var main_scene := _instantiate_under(ui_root, SCENE_MAIN)
	await _await_gui_settle(3)

	var next_turn: Button = main_scene.get_node("HUD/TopBar/HBox/NextTurnButton")
	assert_object(next_turn).is_not_null()

	var button_center := next_turn.get_global_rect().position + (next_turn.get_global_rect().size * 0.5)
	var blocker := _find_mouse_blocker_at_point(ui_root, button_center, next_turn)
	assert(blocker == null, "Expected NextTurnButton to be clickable; found mouse blocker: %s" % (blocker.name if blocker != null else "<none>"))

# ACC:T30.5
# Evidence: write a reproducible artifact under user://logs/** for traceability.
func test_artifact_written_to_logs_directory() -> void:
	var viewport_size := _get_default_viewport_size()
	var date_str := _date_yyyy_mm_dd()

	var base_dir := "user://logs/e2e/%s" % date_str
	var relative_dir := "logs/e2e/%s" % date_str

	var dir := DirAccess.open("user://")
	assert(dir != null, "Cannot open user:// for artifact output")
	var dir_err := dir.make_dir_recursive(relative_dir)
	assert(dir_err == OK or dir_err == ERR_ALREADY_EXISTS, "Cannot create artifact directory under user:// (error=%s)" % str(dir_err))

	var artifact_path := "%s/ui_responsive_clickability_smoke.json" % base_dir
	var payload := {
		"task_id": TASK_ID,
		"task_title": TASK_TITLE,
		"artifact_kind": "ui_responsive_clickability_smoke",
		"date": date_str,
		"viewport": {"width": viewport_size.x, "height": viewport_size.y}
	}

	var file := FileAccess.open(artifact_path, FileAccess.WRITE)
	assert(file != null, "Cannot open artifact for writing: %s" % artifact_path)
	file.store_string(JSON.stringify(payload, "  "))
	file.flush()
	file.close()

	print("ARTIFACT_PATH=", artifact_path)
	assert(FileAccess.file_exists(artifact_path), "Artifact not found after write: %s" % artifact_path)
