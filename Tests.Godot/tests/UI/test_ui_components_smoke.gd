extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const EXPECTED_UI_COMPONENT_SCENES := [
	"res://Game.Godot/Scenes/UI/Components/StatusPanel.tscn",
	"res://Game.Godot/Scenes/UI/Components/ErrorPanel.tscn",
	"res://Game.Godot/Scenes/UI/Components/ListPanel.tscn",
	"res://Game.Godot/Scenes/UI/Components/ConfirmDialog.tscn",
]


# ACC:T31.1
func test_ui_smoke_builtin_ui_types_exist() -> void:
	assert_bool(ClassDB.class_exists("Control")).is_true()
	assert_bool(ClassDB.class_exists("PanelContainer")).is_true()
	assert_bool(ClassDB.is_parent_class("PanelContainer", "Control")).is_true()
	assert_bool(ClassDB.class_exists("ItemList")).is_true()
	assert_bool(ClassDB.is_parent_class("ItemList", "Control")).is_true()


# ACC:T31.6
func test_ui_smoke_expected_component_scenes_exist() -> void:
	# Ensure the reusable UI component library scenes exist.
	for scene_path in EXPECTED_UI_COMPONENT_SCENES:
		assert_bool(ResourceLoader.exists(scene_path)).is_true()


# ACC:T31.3
func test_ui_smoke_expected_component_scenes_can_instantiate() -> void:
	# Ensure scenes can be loaded and instantiated.
	for scene_path in EXPECTED_UI_COMPONENT_SCENES:
		var exists := ResourceLoader.exists(scene_path)
		assert_bool(exists).is_true()
		if not exists:
			continue

		var res := load(scene_path)
		assert_bool(res != null).is_true()
		if res == null:
			continue

		assert_bool(res is PackedScene).is_true()
		if not (res is PackedScene):
			continue

		assert_bool((res as PackedScene).can_instantiate()).is_true()

		var instance := (res as PackedScene).instantiate()
		assert_bool(instance != null).is_true()
		assert_bool(instance is Control).is_true()

		add_child(auto_free(instance))
		await get_tree().process_frame

		if scene_path.ends_with("StatusPanel.tscn"):
			instance.call("SetStatus", "Sample Status", "Sample Message")
			await get_tree().process_frame
			assert_str((instance.get_node("Root/Title") as Label).text).is_equal("Sample Status")
			assert_str((instance.get_node("Root/Message") as Label).text).is_equal("Sample Message")

		if scene_path.ends_with("ErrorPanel.tscn"):
			var retry_count := [0]
			var close_count := [0]

			assert_bool(instance.has_signal("RetryRequested")).is_true()
			assert_bool(instance.has_signal("CloseRequested")).is_true()
			if instance.has_signal("RetryRequested"):
				instance.connect("RetryRequested", func() -> void: retry_count[0] += 1)
			if instance.has_signal("CloseRequested"):
				instance.connect("CloseRequested", func() -> void: close_count[0] += 1)

			instance.set("RetryVisible", true)
			instance.set("CloseVisible", true)
			instance.call("SetError", "Sample Error", "Something failed")
			await get_tree().process_frame
			assert_str((instance.get_node("Root/Title") as Label).text).is_equal("Sample Error")
			assert_str((instance.get_node("Root/Message") as RichTextLabel).text).is_equal("Something failed")

			(instance.get_node("Root/Buttons/RetryButton") as Button).emit_signal("pressed")
			(instance.get_node("Root/Buttons/CloseButton") as Button).emit_signal("pressed")
			await get_tree().process_frame
			assert_int(retry_count[0]).is_equal(1)
			assert_int(close_count[0]).is_equal(1)

			instance.set("RetryVisible", false)
			instance.call("SetError", "Sample Error", "Something failed")
			await get_tree().process_frame
			assert_bool((instance.get_node("Root/Buttons/RetryButton") as Button).visible).is_false()

		if scene_path.ends_with("ListPanel.tscn"):
			instance.call("SetTitle", "Sample List")
			await get_tree().process_frame
			assert_str((instance.get_node("Root/Title") as Label).text).is_equal("Sample List")

			instance.call("SetItems", ["a", "b", "c"])
			await get_tree().process_frame
			assert_int((instance.get_node("Root/Items") as ItemList).item_count).is_equal(3)

			instance.call("ClearItems")
			await get_tree().process_frame
			assert_int((instance.get_node("Root/Items") as ItemList).item_count).is_equal(0)

		if scene_path.ends_with("ConfirmDialog.tscn"):
			var confirmed_count := [0]
			var cancelled_count := [0]

			assert_bool(instance.has_signal("Confirmed")).is_true()
			assert_bool(instance.has_signal("Cancelled")).is_true()
			if instance.has_signal("Confirmed"):
				instance.connect("Confirmed", func() -> void: confirmed_count[0] += 1)
			if instance.has_signal("Cancelled"):
				instance.connect("Cancelled", func() -> void: cancelled_count[0] += 1)

			instance.set("ConfirmText", "Yes")
			instance.set("CancelText", "No")
			instance.set("CancelVisible", true)
			instance.call("SetPrompt", "Sample Confirm", "Proceed?")
			await get_tree().process_frame
			assert_str((instance.get_node("Root/Title") as Label).text).is_equal("Sample Confirm")
			assert_str((instance.get_node("Root/Message") as Label).text).is_equal("Proceed?")
			assert_str((instance.get_node("Root/Buttons/ConfirmButton") as Button).text).is_equal("Yes")
			assert_str((instance.get_node("Root/Buttons/CancelButton") as Button).text).is_equal("No")

			(instance.get_node("Root/Buttons/ConfirmButton") as Button).emit_signal("pressed")
			(instance.get_node("Root/Buttons/CancelButton") as Button).emit_signal("pressed")
			await get_tree().process_frame
			assert_int(confirmed_count[0]).is_equal(1)
			assert_int(cancelled_count[0]).is_equal(1)

			instance.set("CancelVisible", false)
			instance.call("SetPrompt", "Sample Confirm", "Proceed?")
			await get_tree().process_frame
			assert_bool((instance.get_node("Root/Buttons/CancelButton") as Button).visible).is_false()
