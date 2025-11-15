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
    await get_tree().process_frame
    if not db.has_method("TryOpen"):
        await get_tree().process_frame
    return db

func _force_managed() -> void:
    var helper = preload("res://Game.Godot/Adapters/Db/DbTestHelper.cs").new()
    add_child(auto_free(helper))
    helper.ForceManaged()

func test_fk_on_delete_cascade_works() -> void:
    var path = "user://utdb_%s/fk.db" % Time.get_unix_time_from_system()
    var db = await _new_db("SqlDb")
    _force_managed()
    assert_bool(db.TryOpen(path)).is_true()
    db.Execute("PRAGMA foreign_keys=ON;")
    var helper = preload("res://Game.Godot/Adapters/Db/DbTestHelper.cs").new()
    add_child(auto_free(helper))
    helper.ExecSql("CREATE TABLE IF NOT EXISTS p(id TEXT PRIMARY KEY);")
    helper.ExecSql("CREATE TABLE IF NOT EXISTS c(id TEXT PRIMARY KEY, pid TEXT, FOREIGN KEY(pid) REFERENCES p(id) ON DELETE CASCADE);")
    helper.ExecSql("INSERT OR IGNORE INTO p(id) VALUES('A');")
    helper.ExecSql("INSERT OR REPLACE INTO c(id,pid) VALUES('C1','A');")
    var cnt := helper.QueryScalarInt("SELECT COUNT(1) AS cnt FROM c WHERE pid='A';")
    assert_int(cnt).is_equal(1)
    helper.ExecSql("DELETE FROM p WHERE id='A';")
    var cnt2 := helper.QueryScalarInt("SELECT COUNT(1) AS cnt FROM c;")
    assert_int(cnt2).is_equal(0)
