extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node

func before() -> void:
	_bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	_bus.name = "EventBus"
	get_tree().get_root().add_child(auto_free(_bus))


# ACC:T18.3
func test_hud_shows_relationship_value_change_after_advancing_turn() -> void:
	var hud := preload("res://Game.Godot/Scenes/UI/HUD.tscn").instantiate()
	add_child_autofree(hud)
	await get_tree().process_frame

	var label: Label = hud.get_node("IntimacyPanel/IntimacyValueLabel")
	assert_str(label.text).contains("-")

	# StartNewWeek() uses Resolution phase; advance twice to hit Player phase where
	# the demo core pipeline publishes core.social.relationship.changed.
	hud.AdvanceTurnFromGd()
	await get_tree().process_frame
	await get_tree().process_frame
	hud.AdvanceTurnFromGd()
	await get_tree().process_frame
	await get_tree().process_frame

	assert_str(label.text).contains("1")
