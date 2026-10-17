"""Helper antrean Docs/Autopilot/tasks.json untuk agent.sh.

    python3 tasks.py next   <tasks.json> [assignee]       -> id tugas berikutnya (exit 1 kalau tidak ada)
    python3 tasks.py prompt <tasks.json> <id> <template>  -> prompt agent untuk tugas itu
    python3 tasks.py set    <tasks.json> <id> <status> [catatan]
    python3 tasks.py bump   <tasks.json> <id>              -> tambah attempts, cetak jumlahnya
"""
import json
import sys

DONE = {"done", "needs_unity_check"}           # tugas yang dianggap selesai untuk dependency
PRIORITY = {"P0": 0, "P1": 1, "P2": 2, "P3": 3}


def load(path):
    return json.load(open(path, encoding="utf-8"))


def save(path, doc):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
        f.write("\n")


def find(doc, tid):
    return next(t for t in doc["tasks"] if t["id"] == tid)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    cmd, path = sys.argv[1], sys.argv[2]
    doc = load(path)
    status = {t["id"]: t["status"] for t in doc["tasks"]}
    if cmd == "next":
        who = sys.argv[3] if len(sys.argv) > 3 else None
        ready = [t for t in doc["tasks"] if t["lane"] == "agent" and t["status"] in ("todo", "in_progress")
                 and (who is None or t.get("assignee") == who)
                 and all(status.get(d) in DONE for d in t["depends_on"])]
        if not ready:
            sys.exit(1)
        ready.sort(key=lambda t: (t["status"] != "in_progress", PRIORITY.get(t["priority"], 9), t["id"]))
        print(ready[0]["id"])
    elif cmd == "prompt":
        t = find(doc, sys.argv[3])
        template = open(sys.argv[4], encoding="utf-8").read()
        task = {k: t.get(k, "") for k in ("id", "title", "priority", "description", "acceptance_criteria", "files", "notes")}
        text = template.replace("{{TASK_ID}}", t["id"]).replace("{{AGENT}}", t.get("assignee", "agent"))
        print(text.replace("{{TASK_JSON}}", json.dumps(task, ensure_ascii=False, indent=1)))
    elif cmd == "set":
        t = find(doc, sys.argv[3])
        t["status"] = sys.argv[4]
        if len(sys.argv) > 5:
            t["notes"] = (t.get("notes", "") + "\n" + sys.argv[5]).strip()
        save(path, doc)
    elif cmd == "bump":
        t = find(doc, sys.argv[3])
        t["attempts"] = t.get("attempts", 0) + 1
        save(path, doc)
        print(t["attempts"])


if __name__ == "__main__":
    main()
