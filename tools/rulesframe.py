#!/usr/bin/env python3
import argparse
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RULES_DIR = os.path.join(ROOT, "Assets", "Scripts", "EntitySystem", "AI", "Rules")
GENERATED = os.path.join(RULES_DIR, "SimulationRules.Frame.g.cs")
DERIVED = os.path.join(RULES_DIR, "SimulationRules.Frame.cs")
ATTRIBUTE = "FrameField"

FIELD = re.compile(
    r"^(?P<indent>\s*)\[SerializeField(?P<attrs>[^\]]*)\]\s*(?:\[[^\]]*\]\s*)*"
    r"(?:private|public|internal)\s+(?P<type>[\w.<>\[\],]+)\s+(?P<name>\w+)\s*(?:=|;|$)"
)
MARK = re.compile(r"\bFrameField\b(?:\(\"(?P<frame>\w+)\"\))?")
DERIVED_FIELD = re.compile(r"^\s*public readonly (?P<type>[\w.<>\[\],\s]+?)\s+(?P<name>\w+);\s*$")
ASSIGN = re.compile(r"^\s*(?P<name>\w+)\s*=\s*(?P<expr>.*?);\s*$")

HEADER = "using UnityEngine;\nusing Unity.Collections;\nusing Unity.Mathematics;\n\n"


def pascal(name):
    return name[0].upper() + name[1:]


def norm(text):
    return re.sub(r"\s+", " ", text.strip())


def source_files():
    files = sorted(glob.glob(os.path.join(RULES_DIR, "SimulationRules*.cs")))
    return [f for f in files if not f.endswith(".g.cs")]


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def read_lines(path):
    with open(path, encoding="utf-8-sig") as handle:
        return handle.read().split("\n")


def collect_marked():
    marked = []
    errors = []
    for path in source_files():
        if path == DERIVED:
            continue
        for number, line in enumerate(read_lines(path), 1):
            if ATTRIBUTE not in line:
                continue
            match = FIELD.match(line)
            mark = MARK.search(match.group("attrs")) if match else None
            if not match or not mark:
                if re.search(r"\[[^\]]*\b" + ATTRIBUTE + r"\b", line):
                    errors.append(f"{rel(path)}:{number}: cannot read the [{ATTRIBUTE}] field declaration")
                continue
            frame = mark.group("frame") or pascal(match.group("name"))
            marked.append({
                "frame": frame,
                "type": match.group("type"),
                "source": match.group("name"),
                "where": f"{rel(path)}:{number}",
            })
    return marked, errors


def collect_derived():
    if not os.path.exists(DERIVED):
        return [], {}, ["missing " + rel(DERIVED)]
    lines = read_lines(DERIVED)
    start = next((i for i, l in enumerate(lines) if re.search(r"\bstruct FrameDerived\b", l)), None)
    if start is None:
        return [], {}, [f"{rel(DERIVED)}: no struct FrameDerived"]
    fields = []
    exprs = {}
    in_ctor = False
    depth = 0
    seen_open = False
    for number in range(start + 1, len(lines)):
        line = lines[number]
        if not in_ctor:
            match = DERIVED_FIELD.match(line)
            if match:
                fields.append({
                    "frame": match.group("name"),
                    "type": norm(match.group("type")),
                    "where": f"{rel(DERIVED)}:{number + 1}",
                })
            if re.search(r"\bFrameDerived\s*\(\s*SimulationRules\s+r\s*\)", line):
                in_ctor = True
                depth = 0
                seen_open = False
            continue
        depth += line.count("{") - line.count("}")
        if "{" in line:
            seen_open = True
        match = ASSIGN.match(line)
        if match and seen_open and depth >= 1:
            exprs[match.group("name")] = norm(match.group("expr"))
        if seen_open and depth <= 0:
            break
    return fields, exprs, []


def render(marked, derived):
    out = [HEADER.rstrip("\n"), "", "public sealed partial class SimulationRules", "{",
           '    [System.CodeDom.Compiler.GeneratedCode("tools/rulesframe.py", "1")]',
           "    public readonly partial struct RulesFrame", "    {"]
    for item in marked + derived:
        out.append(f"        public readonly {item['type']} {item['frame']};")
    out += ["", "        public RulesFrame(SimulationRules r)", "        {"]
    if derived:
        out.append("            var d = new FrameDerived(r);")
    for item in marked:
        out.append(f"            {item['frame']} = r.{item['source']};")
    for item in derived:
        out.append(f"            {item['frame']} = d.{item['frame']};")
    out += ["        }", "    }", "}", ""]
    return "\n".join(out)


def build():
    marked, errors = collect_marked()
    derived, exprs, derived_errors = collect_derived()
    errors += derived_errors
    for item in derived:
        if item["frame"] not in exprs:
            errors.append(f"{item['where']}: FrameDerived.{item['frame']} is not assigned in its constructor")
    for name in exprs:
        if not any(item["frame"] == name for item in derived):
            errors.append(f"{rel(DERIVED)}: FrameDerived constructor assigns {name}, which is not declared")
    seen = {}
    for item in marked + derived:
        if item["frame"] in seen:
            errors.append(f"{item['where']}: frame member {item['frame']} is also defined at {seen[item['frame']]}")
        seen[item["frame"]] = item["where"]
    return marked, derived, exprs, errors


def read_generated():
    if not os.path.exists(GENERATED):
        return None, {}, {}
    with open(GENERATED, encoding="utf-8") as handle:
        text = handle.read()
    fields = {}
    assigns = {}
    for line in text.split("\n"):
        match = re.match(r"^\s+public readonly (?P<type>[\w.<>\[\],\s]+?)\s+(?P<name>\w+);\s*$", line)
        if match:
            fields[match.group("name")] = norm(match.group("type"))
            continue
        match = ASSIGN.match(line)
        if match and match.group("name") in fields:
            assigns[match.group("name")] = norm(match.group("expr"))
    return text, fields, assigns


def cmd_gen():
    marked, derived, _, errors = build()
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    with open(GENERATED, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(render(marked, derived))
    print(f"written {rel(GENERATED)}: {len(marked)} marked fields, {len(derived)} derived")
    return 0


def cmd_check():
    marked, derived, _, errors = build()
    text, fields, _ = read_generated()
    if text is None:
        errors.append(f"missing {rel(GENERATED)}: run python3 tools/rulesframe.py gen")
    else:
        for item in marked:
            if item["frame"] not in fields:
                errors.append(f"{item['where']}: [{ATTRIBUTE}] field {item['source']} has no generated member {item['frame']}")
        sources = {item["frame"] for item in marked + derived}
        for name in fields:
            if name not in sources:
                errors.append(f"{rel(GENERATED)}: frame member {name} has no source")
        if not errors and text != render(marked, derived):
            errors.append(f"{rel(GENERATED)} is stale: run python3 tools/rulesframe.py gen")
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1
    print(f"rulesframe ok: {len(marked)} marked fields, {len(derived)} derived")
    return 0


def read_old(path):
    lines = read_lines(path)
    start = next(i for i, l in enumerate(lines) if re.search(r"\bstruct RulesFrame\b", l))
    ctor = next(i for i, l in enumerate(lines) if re.search(r"\bpublic RulesFrame\(SimulationRules r\)", l))
    fields = {}
    for line in lines[start:ctor]:
        match = re.match(r"^\s+public readonly (?P<type>[\w.<>\[\],\s]+?)\s+(?P<name>\w+);\s*$", line)
        if match:
            fields[match.group("name")] = norm(match.group("type"))
    triples = set()
    depth = 0
    for line in lines[ctor:]:
        depth += line.count("{") - line.count("}")
        match = ASSIGN.match(line)
        if match and match.group("name") in fields:
            triples.add((match.group("name"), fields[match.group("name")], norm(match.group("expr"))))
        if depth <= 0 and line.strip() == "}":
            break
    return triples


def read_new():
    _, exprs, errors = collect_derived()
    if errors:
        raise SystemExit("\n".join(errors))
    _, fields, assigns = read_generated()
    triples = set()
    for name, kind in fields.items():
        expr = assigns[name]
        if expr == f"d.{name}":
            expr = exprs[name]
        triples.add((name, kind, expr))
    return triples


def cmd_compare(old_path):
    old = read_old(old_path)
    new = read_new()
    print(f"old: {len(old)} assignments, new: {len(new)} assignments")
    only_old = sorted(old - new)
    only_new = sorted(new - old)
    for item in only_old:
        print("only in old:", item)
    for item in only_new:
        print("only in new:", item)
    if only_old or only_new:
        return 1
    print("equal: every (frame name, type, assignment expression) is the same")
    return 0


def main():
    parser = argparse.ArgumentParser(description="Generate SimulationRules.RulesFrame from [FrameField] fields")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("gen", help="write SimulationRules.Frame.g.cs")
    sub.add_parser("check", help="fail on a stale generated file, a field without a member or a member without a source")
    compare = sub.add_parser("compare", help="compare the assignments of an older hand-written RulesFrame with the generated ones")
    compare.add_argument("--old", required=True, help="a SimulationRules.cs that still holds the hand-written struct")
    args = parser.parse_args()
    if args.command == "gen":
        return cmd_gen()
    if args.command == "check":
        return cmd_check()
    return cmd_compare(args.old)


if __name__ == "__main__":
    sys.exit(main())
