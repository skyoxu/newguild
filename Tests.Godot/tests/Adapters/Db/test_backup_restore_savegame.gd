extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _new_db(name: String) -> Node:
    var db = null
    if ClassDB.class_exists("SqliteDataStore"):
        db = ClassDB.instantiate("SqliteDataStore")
    else:
        var s = load("res://Game.Godot/Adapters/SqliteDataStore.cs")
        db = Node.new()
        db.set_script(s)
    db.name = name
    get_tree().get_root().add_child(auto_free(db))
    return db

static func _mkdirs_user(abs_path: String) -> bool:
    var base := abs_path.get_base_dir()
    if not base.begins_with("user://"):
        return false
    var rel := base.substr("user://".length())
    var d := DirAccess.open("user://")
    if d == null:
        return false
    return d.make_dir_recursive(rel) == OK

static func _copy_file(src: String, dst: String) -> bool:
    if not _mkdirs_user(dst):
        return false
    var r := FileAccess.open(src, FileAccess.READ)
    if r == null:
        return false
    var data := r.get_buffer(r.get_length())
    var w := FileAccess.open(dst, FileAccess.WRITE)
    if w == null:
        return false
    w.store_buffer(data)
    return true

func test_backup_restore_savegame() -> void:
    var path = "user://utdb_%s/sg_bak.db" % Time.get_unix_time_from_system()
    var db = _new_db("SqlDb")
    var ok = db.TryOpen(path)
    assert_bool(ok).is_true()
    var helper = preload("res://Game.Godot/Adapters/Db/DbTestHelper.cs").new()
    add_child(auto_free(helper))
    helper.CreateSchema()
    helper.ClearAll()

    var bridge = preload("res://Game.Godot/Adapters/Db/RepositoryTestBridge.cs").new()
    add_child(auto_free(bridge))
    var username = "u_%s" % Time.get_unix_time_from_system()
    assert_bool(bridge.UpsertUser(username)).is_true()
    var uid = bridge.FindUserId(username)
    assert_that(uid).is_not_null()
    var json = '{"hp": 55, "ts": %d}' % Time.get_unix_time_from_system()
    assert_bool(bridge.UpsertSave(uid, 1, json)).is_true()

    # close and copy to backup
    db.Close()
    await get_tree().process_frame
    var backup_dir = "user://backup_%s" % Time.get_unix_time_from_system()
    var backup_path = "%s/%s" % [backup_dir, path.get_file()]
    assert_bool(_copy_file(path, backup_path)).is_true()

    # open from backup and verify
    var ok2 = db.TryOpen(backup_path)
    assert_bool(ok2).is_true()
    var bridge2 = preload("res://Game.Godot/Adapters/Db/RepositoryTestBridge.cs").new()
    add_child(auto_free(bridge2))
    var got = bridge2.GetSaveData(uid, 1)
    assert_str(str(got)).contains('"hp": 55')
