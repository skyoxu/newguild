extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _make_ui() -> Dictionary:
	var root := Control.new()
	root.name = "Root"
	root.mouse_filter = Control.MOUSE_FILTER_STOP

	var status := Label.new()
	status.name = "StatusLabel"
	root.add_child(status)

	var retry := Button.new()
	retry.name = "RetryButton"
	retry.text = "Retry"
	retry.visible = false
	retry.disabled = true
	root.add_child(retry)

	var other := Button.new()
	other.name = "OtherButton"
	other.text = "Other"
	other.disabled = false
	root.add_child(other)

	var other_input := LineEdit.new()
	other_input.name = "OtherInput"
	other_input.text = "x"
	other_input.editable = true
	root.add_child(other_input)

	return {"root": root, "status": status, "retry": retry, "other": other, "other_input": other_input}

# ACC:T32.2
func test_interaction_state_mapping_smoke() -> void:
	var mapper := preload("res://Game.Godot/Scripts/UI/InteractionStateUi.gd")
	var ui := _make_ui()
	var root: Control = ui["root"]
	var status: Label = ui["status"]
	var retry: Button = ui["retry"]
	var other: Button = ui["other"]
	var other_input: LineEdit = ui["other_input"]

	mapper.apply_state(root, status, retry, mapper.STATE_READY)
	assert_str(status.text).is_equal("")
	assert_bool(retry.visible).is_false()
	assert_int(root.mouse_filter).is_equal(Control.MOUSE_FILTER_STOP)
	assert_bool(bool(root.get_meta("interaction_disabled"))).is_false()
	assert_bool(other.disabled).is_false()
	assert_bool(other_input.editable).is_true()

	mapper.apply_state(root, status, retry, mapper.STATE_LOADING)
	assert_str(status.text).is_equal("Loading...")
	assert_bool(retry.visible).is_false()
	assert_int(root.mouse_filter).is_equal(Control.MOUSE_FILTER_STOP)
	assert_bool(bool(root.get_meta("interaction_disabled"))).is_true()
	assert_bool(other.disabled).is_true()
	assert_bool(other_input.editable).is_false()

	mapper.apply_state(root, status, retry, mapper.STATE_ERROR, "Network error")
	assert_str(status.text).is_equal("Network error")
	assert_bool(retry.visible).is_true()
	assert_bool(retry.disabled).is_false()
	assert_int(root.mouse_filter).is_equal(Control.MOUSE_FILTER_STOP)
	assert_bool(root.modulate.is_equal_approx(Color(1.0, 0.75, 0.75, 1.0))).is_true()
	assert_bool(other.disabled).is_true()
	assert_bool(other_input.editable).is_false()

	var d1 := mapper.describe_state(mapper.STATE_LOADING)
	var d2 := mapper.describe_state(mapper.STATE_LOADING)
	assert_str(d1["status_text"]).is_equal(d2["status_text"])
	assert_int(d1["mouse_filter"]).is_equal(d2["mouse_filter"])

	mapper.apply_state(root, status, retry, "unknown-state")
	assert_str(status.text).is_equal("")
	assert_bool(bool(root.get_meta("interaction_disabled"))).is_false()
	assert_bool(other_input.editable).is_true()

	root.free()
