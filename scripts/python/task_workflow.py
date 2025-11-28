#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Task Master Workflow Orchestrator

自动化任务生命周期管理,集成 Git 工作流与 SuperClaude。

使用方法:
    py -3 scripts/python/task_workflow.py next
    py -3 scripts/python/task_workflow.py start 1.1
    py -3 scripts/python/task_workflow.py commit 1.1
    py -3 scripts/python/task_workflow.py finish 1.1
    py -3 scripts/python/task_workflow.py complete 1.1
    py -3 scripts/python/task_workflow.py block 1.1 "等待 ADR-0007 批准"
"""

import subprocess
import sys
import json
import os
from pathlib import Path
from datetime import datetime
from typing import Optional, Dict, List, Any

# 配置
# 默认使用 tasks_back.json 作为任务文件，可通过环境变量覆盖：
#   TASK_WORKFLOW_TASKS_FILE=.taskmaster/tasks/tasks_gameplay.json
_TASKS_FILE = os.environ.get("TASK_WORKFLOW_TASKS_FILE", ".taskmaster/tasks/tasks_back.json")
TASKS_JSON_PATH = Path(_TASKS_FILE)


def get_all_tasks() -> List[Dict[str, Any]]:
    """读取所有任务（newguild: 顶层为任务数组）。"""
    if not TASKS_JSON_PATH.exists():
        print(f"错误: 找不到任务文件 {TASKS_JSON_PATH}")
        sys.exit(1)

    with open(TASKS_JSON_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    # 兼容：newguild 顶层为数组；旧结构为 {"tasks": [...]}。
    if isinstance(data, list):
        return data
    if isinstance(data, dict) and "tasks" in data:
        tasks = data.get("tasks")
        if isinstance(tasks, list):
            return tasks
    print(f"错误: 任务文件 {TASKS_JSON_PATH} 结构不符合预期（需为任务数组或包含 tasks 字段的对象）")
    sys.exit(1)


def get_task(task_id: str) -> Optional[Dict[str, Any]]:
    """根据 ID 获取任务"""
    tasks = get_all_tasks()
    for task in tasks:
        if task.get("id") == task_id:
            return task
    return None


def save_tasks(tasks: List[Dict[str, Any]]) -> None:
    """保存任务列表到 JSON（newguild: 顶层为任务数组）。"""
    with open(TASKS_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(tasks, f, ensure_ascii=False, indent=2)
    print(f"✅ 已保存任务到 {TASKS_JSON_PATH}")


def _get_list(task: Dict[str, Any], primary: str, fallback: str) -> List[str]:
    """读取列表字段，兼容新旧命名。"""
    value = task.get(primary)
    if isinstance(value, list):
        return value
    value = task.get(fallback)
    if isinstance(value, list):
        return value
    return []


def can_start(task_id: str) -> bool:
    """检查任务的依赖是否全部完成。使用 depends_on，兼容旧 dependencies。"""
    task = get_task(task_id)
    if not task:
        print(f"错误: 找不到任务 {task_id}")
        return False

    dependencies = task.get("depends_on")
    if dependencies is None:
        dependencies = task.get("dependencies", [])
    if not dependencies:
        return True

    for dep_id in dependencies:
        dep_task = get_task(dep_id)
        if not dep_task:
            print(f"警告: 依赖任务 {dep_id} 不存在")
            continue
        if dep_task.get("status") != "completed":
            print(f"错误: 依赖任务 {dep_id} 状态为 {dep_task.get('status')},尚未完成")
            return False

    return True


def create_branch(task_id: str) -> str:
    """创建功能分支"""
    task = get_task(task_id)
    if not task:
        print(f"错误: 找不到任务 {task_id}")
        sys.exit(1)

    # 生成分支名
    title = task.get("title", "")
    slug = title.lower().replace(" ", "-").replace("_", "-")[:30]
    branch = f"feature/task-{task_id}-{slug}"

    # 创建分支
    try:
        subprocess.run(["git", "checkout", "-b", branch], check=True)
        print(f"✅ 已创建分支: {branch}")
        return branch
    except subprocess.CalledProcessError as e:
        print(f"❌ 创建分支失败: {e}")
        sys.exit(1)


def record_commit(task_id: str) -> None:
    """记录最新的 commit 到任务"""
    try:
        # 获取最新 commit SHA
        sha = subprocess.check_output(
            ["git", "rev-parse", "HEAD"],
            text=True
        ).strip()

        # 获取 commit message
        message = subprocess.check_output(
            ["git", "log", "-1", "--format=%B"],
            text=True
        ).strip()

        # 获取时间戳
        timestamp = datetime.now().isoformat()

        # 更新任务
        tasks = get_all_tasks()
        for task in tasks:
            if task.get("id") == task_id:
                if "commits" not in task:
                    task["commits"] = []
                task["commits"].append({
                    "sha": sha,
                    "message": message,
                    "timestamp": timestamp
                })
                break

        save_tasks(tasks)
        print(f"✅ 已记录 commit {sha[:7]} 到任务 {task_id}")

    except subprocess.CalledProcessError as e:
        print(f"❌ 记录 commit 失败: {e}")
        sys.exit(1)


def create_pr(task_id: str) -> str:
    """创建 Pull Request"""
    task = get_task(task_id)
    if not task:
        print(f"错误: 找不到任务 {task_id}")
        sys.exit(1)

    # 生成 PR 标题
    title = f"Task {task_id}: {task.get('title', '')}"

    # 生成 PR body（兼容新旧字段命名）
    description = task.get("description", "")
    adr_refs = ", ".join(_get_list(task, "adr_refs", "adrRefs"))
    arch_refs = ", ".join(_get_list(task, "chapter_refs", "archRefs"))
    test_refs = _get_list(task, "test_refs", "testRefs")

    body = f"""## 任务说明
{description}

## ADR/CH 引用
{adr_refs} | {arch_refs}

## 测试引用
"""

    for ref in test_refs:
        body += f"- [ ] {ref}\n"

    body += f"\nRefs: #{task_id}\n"

    try:
        # 创建 PR
        result = subprocess.run(
            ["gh", "pr", "create", "--title", title, "--body", body],
            capture_output=True,
            text=True,
            check=True
        )

        # 解析 PR URL (gh 输出的最后一行)
        pr_url = result.stdout.strip().split("\n")[-1]

        # 更新任务
        tasks = get_all_tasks()
        for t in tasks:
            if t.get("id") == task_id:
                t["prUrl"] = pr_url
                break

        save_tasks(tasks)
        print(f"✅ 已创建 PR: {pr_url}")
        return pr_url

    except subprocess.CalledProcessError as e:
        print(f"❌ 创建 PR 失败: {e}")
        print(f"stderr: {e.stderr}")
        sys.exit(1)


def cmd_next() -> None:
    """查看下一个待办任务"""
    tasks = get_all_tasks()

    # 查找第一个 pending 且依赖满足的任务
    for task in tasks:
        task_id = task.get("id")
        status = task.get("status")

        if status == "pending" and can_start(task_id):
            print(f"\n下一个任务:")
            print(f"  ID: {task_id}")
            print(f"  Title: {task.get('title')}")
            print(f"  Priority: {task.get('priority', 'medium')}")
            deps = task.get("depends_on")
            if deps is None:
                deps = task.get("dependencies", [])
            print(f"  Dependencies: {deps} (all satisfied)")

            adr_list = _get_list(task, "adr_refs", "adrRefs")
            chap_list = _get_list(task, "chapter_refs", "archRefs")
            print(f"  ADRs: {', '.join(adr_list)}")
            print(f"  Chapters: {', '.join(chap_list)}")

            overlay = None
            overlay_refs = task.get("overlay_refs")
            if isinstance(overlay_refs, list) and overlay_refs:
                overlay = overlay_refs[0]
            else:
                overlay = task.get("overlay")
            if overlay:
                print(f"  Overlay: {overlay}")
            return

    print("✅ 没有待办任务(所有任务已完成或被阻塞)")


def cmd_start(task_id: str) -> None:
    """开始任务(创建分支 + 更新状态)"""
    if not can_start(task_id):
        print(f"❌ 任务 {task_id} 的依赖尚未完成,无法开始")
        sys.exit(1)

    # 创建分支
    branch = create_branch(task_id)

    # 更新任务状态
    tasks = get_all_tasks()
    for task in tasks:
        if task.get("id") == task_id:
            task["status"] = "in-progress"
            task["gitBranch"] = branch
            break

    save_tasks(tasks)
    print(f"✅ 任务 {task_id} 已开始")


def cmd_commit(task_id: str) -> None:
    """提交代码(记录 commit SHA)"""
    record_commit(task_id)


def cmd_finish(task_id: str) -> None:
    """完成任务(创建 PR + 更新状态为 review)"""
    pr_url = create_pr(task_id)

    # 更新任务状态
    tasks = get_all_tasks()
    for task in tasks:
        if task.get("id") == task_id:
            task["status"] = "review"
            break

    save_tasks(tasks)
    print(f"✅ 任务 {task_id} 已提交审查,PR: {pr_url}")


def cmd_complete(task_id: str) -> None:
    """标记任务完成(PR merged 后)"""
    tasks = get_all_tasks()
    for task in tasks:
        if task.get("id") == task_id:
            task["status"] = "completed"
            break

    save_tasks(tasks)
    print(f"✅ 任务 {task_id} 已完成")


def cmd_block(task_id: str, reason: str) -> None:
    """标记任务为 blocked"""
    tasks = get_all_tasks()
    for task in tasks:
        if task.get("id") == task_id:
            task["status"] = "blocked"
            if "blockers" not in task:
                task["blockers"] = []
            task["blockers"].append(reason)
            break

    save_tasks(tasks)
    print(f"警告: 任务 {task_id} 已标记为 blocked")
    print(f"原因: {reason}")


def main():
    """主函数"""
    if len(sys.argv) < 2:
        print("使用方法:")
        print("  py -3 scripts/python/task_workflow.py next")
        print("  py -3 scripts/python/task_workflow.py start <task-id>")
        print("  py -3 scripts/python/task_workflow.py commit <task-id>")
        print("  py -3 scripts/python/task_workflow.py finish <task-id>")
        print("  py -3 scripts/python/task_workflow.py complete <task-id>")
        print('  py -3 scripts/python/task_workflow.py block <task-id> "原因"')
        sys.exit(1)

    command = sys.argv[1]

    if command == "next":
        cmd_next()
    elif command == "start":
        if len(sys.argv) < 3:
            print("错误: 缺少 task-id 参数")
            sys.exit(1)
        cmd_start(sys.argv[2])
    elif command == "commit":
        if len(sys.argv) < 3:
            print("错误: 缺少 task-id 参数")
            sys.exit(1)
        cmd_commit(sys.argv[2])
    elif command == "finish":
        if len(sys.argv) < 3:
            print("错误: 缺少 task-id 参数")
            sys.exit(1)
        cmd_finish(sys.argv[2])
    elif command == "complete":
        if len(sys.argv) < 3:
            print("错误: 缺少 task-id 参数")
            sys.exit(1)
        cmd_complete(sys.argv[2])
    elif command == "block":
        if len(sys.argv) < 4:
            print("错误: 缺少 task-id 或原因参数")
            sys.exit(1)
        cmd_block(sys.argv[2], sys.argv[3])
    else:
        print(f"错误: 未知命令 '{command}'")
        sys.exit(1)


if __name__ == "__main__":
    main()
