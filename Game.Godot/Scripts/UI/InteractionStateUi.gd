extends Node
class_name InteractionStateUi

const STATE_READY := "ready"
const STATE_LOADING := "loading"
const STATE_ERROR := "error"
const STATE_DISABLED := "disabled"

static func describe_state(state: String, error_message: String = "") -> Dictionary:
	var result := {
		"status_text": "",
		"retry_visible": false,
		"retry_disabled": true,
		"mouse_filter": Control.MOUSE_FILTER_STOP,
		"modulate": Color(1.0, 1.0, 1.0, 1.0),
	}

	match state:
		STATE_READY:
			if error_message != "":
				result.status_text = error_message
		STATE_LOADING:
			result.status_text = error_message if error_message != "" else "Loading..."
			result.mouse_filter = Control.MOUSE_FILTER_STOP
		STATE_ERROR:
			result.status_text = error_message if error_message != "" else "Error"
			result.retry_visible = true
			result.retry_disabled = false
			result.mouse_filter = Control.MOUSE_FILTER_STOP
			result.modulate = Color(1.0, 0.75, 0.75, 1.0)
		STATE_DISABLED:
			result.mouse_filter = Control.MOUSE_FILTER_STOP
		_:
			pass

	return result


static func _meta_key(name: String) -> String:
	return "interaction_prev_" + name


static func _apply_or_restore_control_mouse_filter(ctrl: Control, disabled: bool) -> void:
	var key := _meta_key("mouse_filter")
	if disabled:
		if not ctrl.has_meta(key):
			ctrl.set_meta(key, ctrl.mouse_filter)
		ctrl.mouse_filter = Control.MOUSE_FILTER_IGNORE
		return

	if ctrl.has_meta(key):
		ctrl.mouse_filter = int(ctrl.get_meta(key))
		ctrl.remove_meta(key)


static func _apply_or_restore_button_disabled(btn: BaseButton, disabled: bool) -> void:
	var key := _meta_key("disabled")
	if disabled:
		if not btn.has_meta(key):
			btn.set_meta(key, btn.disabled)
		btn.disabled = true
		return

	if btn.has_meta(key):
		btn.disabled = bool(btn.get_meta(key))
		btn.remove_meta(key)


static func _apply_or_restore_line_edit(edit: LineEdit, disabled: bool) -> void:
	var key := _meta_key("editable")
	if disabled:
		if not edit.has_meta(key):
			edit.set_meta(key, edit.editable)
		edit.editable = false
		return

	if edit.has_meta(key):
		edit.editable = bool(edit.get_meta(key))
		edit.remove_meta(key)


static func _apply_or_restore_text_edit(edit: TextEdit, disabled: bool) -> void:
	var key := _meta_key("editable")
	if disabled:
		if not edit.has_meta(key):
			edit.set_meta(key, edit.editable)
		edit.editable = false
		return

	if edit.has_meta(key):
		edit.editable = bool(edit.get_meta(key))
		edit.remove_meta(key)


static func _set_interaction_disabled(node: Node, disabled: bool, except_nodes: Array[Node]) -> void:
	for child in node.get_children():
		if child in except_nodes:
			continue
		if child is Control:
			_apply_or_restore_control_mouse_filter(child as Control, disabled)
		if child is BaseButton:
			_apply_or_restore_button_disabled(child as BaseButton, disabled)
		elif child is LineEdit:
			_apply_or_restore_line_edit(child as LineEdit, disabled)
		elif child is TextEdit:
			_apply_or_restore_text_edit(child as TextEdit, disabled)
		_set_interaction_disabled(child, disabled, except_nodes)


static func apply_state(root: Control, status: Label, retry: Button, state: String, error_message: String = "") -> void:
	apply_state_with_exceptions(root, status, retry, [retry], state, error_message)


static func apply_state_with_exceptions(root: Control, status: Label, retry: Button, exceptions: Array[Node], state: String, error_message: String = "") -> void:
	if state != STATE_READY and state != STATE_LOADING and state != STATE_ERROR and state != STATE_DISABLED:
		state = STATE_READY
	var d := describe_state(state, error_message)

	status.text = d["status_text"]
	root.mouse_filter = d["mouse_filter"]
	root.modulate = d["modulate"]

	var disable_children := state != STATE_READY
	_set_interaction_disabled(root, disable_children, exceptions)

	retry.visible = d["retry_visible"]
	retry.disabled = d["retry_disabled"]

	root.set_meta("interaction_state", state)
	root.set_meta("interaction_disabled", disable_children)
