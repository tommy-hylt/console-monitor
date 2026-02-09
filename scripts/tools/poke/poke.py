import sys
from typing import Any, Dict, List

import pyautogui

# poke.py
# Flag style only (order-preserving):
#   python poke.py --text "Abc" --text "def" --keys "{ENTER}" --text "Bcd"
#
# Foreground/focus is handled by Foreground.exe (caller responsibility).

KEY_MAP = {
    "{ENTER}": "enter",
    "{TAB}": "tab",
    "{ESC}": "esc",
    "{ESCAPE}": "esc",
    "{UP}": "up",
    "{DOWN}": "down",
    "{LEFT}": "left",
    "{RIGHT}": "right",
}

KEY_ALIAS = {
    "enter": "enter",
    "tab": "tab",
    "esc": "esc",
    "escape": "esc",
    "up": "up",
    "down": "down",
    "left": "left",
    "right": "right",
}


def die(msg: str, code: int = 2):
    sys.stderr.write(msg + "\n")
    raise SystemExit(code)


def parse_flag_actions(argv: List[str]) -> List[Dict[str, Any]]:
    actions: List[Dict[str, Any]] = []

    i = 0
    while i < len(argv):
        a = argv[i]

        # Some environments may inject this token; treat as ENTER.
        if a.lower() == "-encodedcommand":
            actions.append({"type": "key", "key": "enter"})
            i += 1
            continue

        if a == "--text":
            if i + 1 >= len(argv):
                die("--text expects an argument", 2)
            actions.append({"type": "text", "text": argv[i + 1]})
            i += 2
            continue

        if a in ("--keys", "--key"):
            if i + 1 >= len(argv):
                die("--keys expects an argument", 2)
            raw = argv[i + 1]
            k = (raw or "").strip()

            # Some environments rewrite "{ENTER}" into "-encodedCommand".
            if k.lower() == "-encodedcommand":
                actions.append({"type": "key", "key": "enter"})
            elif k.upper() in KEY_MAP:
                actions.append({"type": "key", "key": KEY_MAP[k.upper()]})
            else:
                kk = k.lower()
                if kk in KEY_ALIAS:
                    actions.append({"type": "key", "key": KEY_ALIAS[kk]})
                else:
                    die(f"Unsupported key: {k}", 3)

            i += 2
            continue

        # ignore unknown tokens
        i += 1

    return actions


def run_actions(actions: List[Dict[str, Any]]):
    pyautogui.PAUSE = 0.02

    for a in actions:
        t = (a.get("type") or "").lower().strip()
        if t == "text":
            text = a.get("text")
            if isinstance(text, str) and text:
                pyautogui.write(text, interval=0.0)
        elif t == "key":
            key = (a.get("key") or "").lower().strip()
            k = KEY_ALIAS.get(key)
            if not k:
                die(f"Unsupported key: {key}", 3)
            pyautogui.press(k)


def main():
    argv = sys.argv[1:]

    if not ("--text" in argv or "--keys" in argv or "--key" in argv):
        die("No actions provided. Use --text/--keys.", 2)

    actions = parse_flag_actions(argv)
    if not actions:
        die("No actions parsed.", 2)

    run_actions(actions)
    sys.stdout.write("OK\n")


if __name__ == "__main__":
    main()
