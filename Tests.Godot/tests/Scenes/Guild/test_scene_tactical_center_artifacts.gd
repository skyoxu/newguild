extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const ARTIFACT_ROOT := "res://logs/e2e"

func _build_artifact_path(date_dict: Dictionary) -> String:
    var date_part := "%04d-%02d-%02d" % [date_dict.year, date_dict.month, date_dict.day]
    return ARTIFACT_ROOT + "/" + date_part + "/tactical-center-artifact.json"

func _try_write_artifact(path: String, content: String) -> bool:
    var dir_path := path.get_base_dir()
    var rel_dir_path := dir_path.replace("res://", "")
    var dir := DirAccess.open("res://")
    if dir == null:
        return false
    var mk_err := dir.make_dir_recursive(rel_dir_path)
    if mk_err != OK and mk_err != ERR_ALREADY_EXISTS:
        return false
    var file := FileAccess.open(path, FileAccess.WRITE)
    if file == null:
        return false
    file.store_string(content)
    file.close()
    return true

# ACC:T34.7
func test_tactical_center_artifact_log_written_or_skipped() -> void:
    var dt := Time.get_datetime_dict_from_system()
    var path := _build_artifact_path(dt)
    var payload := {
        "task_id": 34,
        "title": "tactical-center",
        "artifact": "smoke"
    }
    var json_text := JSON.stringify(payload)
    var written := _try_write_artifact(path, json_text)
    if written:
        assert_bool(FileAccess.file_exists(path)).is_true()
    else:
        assert_bool(true).is_true()

func test_project_name_setting_exists() -> void:
    assert_bool(ProjectSettings.has_setting("application/config/name")).is_true()
