#!/usr/bin/env -S godot -s
extends SceneTree


var _cli_runner: GdUnitTestCIRunner


func _initialize() -> void:
	# Only set window mode if not running headless (ADR-0019 CI compatibility)
	# DisplayServer may be unavailable or blocking in --headless mode
	if not OS.has_feature("dedicated_server") and DisplayServer.get_name() != "headless":
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MINIMIZED)
	_cli_runner = GdUnitTestCIRunner.new()
	root.add_child(_cli_runner)


# do not use print statements on _finalize it results in random crashes
func _finalize() -> void:
	queue_delete(_cli_runner)
	if OS.is_stdout_verbose():
		prints("Finallize ..")
		prints("-Orphan nodes report-----------------------")
		Window.print_orphan_nodes()
		prints("Finallize .. done")
