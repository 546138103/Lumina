"""One local child process per lesson. Only fixed status tokens cross stdout."""
import os
import sys
import threading
from pathlib import Path
from queue import Queue
from tkinter import Tk, simpledialog

protocol = sys.stdout
sys.stdout = sys.stderr
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


def signal(status):
    print("LUMINA " + status, file=protocol, flush=True)


def read_commands(commands):
    for line in sys.stdin:
        commands.put(line.strip())
    # Parent closed its pipe, including while blocked in audio, HTTP or a dialog.
    os._exit(0)


def ask_key():
    root = Tk()
    root.withdraw()
    root.attributes("-topmost", True)
    try:
        key = simpledialog.askstring("Lumina", "请输入方舟 API Key", show="*", parent=root)
        return (key or "").strip()
    finally:
        root.destroy()


def main():
    commands = Queue()
    threading.Thread(target=read_commands, args=(commands,), daemon=True).start()
    key = ask_key()
    if not key:
        signal("CANCELLED")
        return
    from ASR.realtime_asr_test import load_model, recognize, record_sentence
    from Semantic.introduction import matches_question
    model = load_model()
    signal("READY")
    while True:
        index, timeout = commands.get().split()
        try:
            audio = record_sentence(float(timeout))
            if audio is None:
                signal("SILENCE")
                continue
            text = recognize(audio, model)
            signal("YES" if text and matches_question(int(index), text, key) else "NO")
        except Exception as exc:
            code = getattr(exc, "code", None)
            code = code if isinstance(code, str) and code.isascii() and code.isidentifier() else ""
            signal("FAULT " + type(exc).__name__ + " " + code)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        signal("FAULT " + type(exc).__name__)
