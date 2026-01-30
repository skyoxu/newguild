extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE := "res://Game.Godot/Scenes/UI/HUD.tscn"
const ACHIEVEMENT_ENTITY := "res://Game.Core/Domain/Entities/Achievement.cs"
const ACHIEVEMENT_REPOSITORY := "res://Game.Core/Repositories/IAchievementRepository.cs"
const ACHIEVEMENT_ADAPTER := "res://Game.Godot/Adapters/Db/AchievementRepository.cs"
const ACHIEVEMENT_TRACKER := "res://Game.Core/Domain/Achievements/AchievementTracker.cs"

# ACC:T36.3
func test_achievements_smoke_hud_has_label() -> void:
	assert_bool(FileAccess.file_exists(ACHIEVEMENT_ENTITY)).is_true()
	assert_bool(FileAccess.file_exists(ACHIEVEMENT_REPOSITORY)).is_true()
	assert_bool(FileAccess.file_exists(ACHIEVEMENT_ADAPTER)).is_true()
	assert_bool(FileAccess.file_exists(ACHIEVEMENT_TRACKER)).is_true()
	var scene := preload(HUD_SCENE).instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame
	var label := scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(label).is_not_null()
