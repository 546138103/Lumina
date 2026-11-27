"""One local child process per lesson. Only fixed status tokens cross stdout."""
import json
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
    # Read the Unity pipe without holding a Windows CRT descriptor lock during
    # a blocking read. Native audio/ML imports may also inspect the standard fds.
    if os.name == "nt":
        import _winapi
        import msvcrt
        import time
        handle = msvcrt.get_osfhandle(sys.stdin.fileno())
        pending = b""
        try:
            while True:
                available, _ = _winapi.PeekNamedPipe(handle)
                if not available:
                    time.sleep(0.1)
                    continue
                data, _ = _winapi.ReadFile(handle, min(available, 4096))
                if not data:
                    break
                lines = (pending + data).split(b"\n")
                pending = lines.pop()
                for line in lines:
                    commands.put(line.decode("utf-8").strip())
        except BrokenPipeError:
            pass
    else:
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


CREDENTIAL_TARGET = "Lumina/Ark/APIKey"


def get_key(replace=False):
    import pywintypes
    import win32cred

    if not replace:
        try:
            credential = win32cred.CredRead(CREDENTIAL_TARGET, win32cred.CRED_TYPE_GENERIC)
            return credential["CredentialBlob"].decode("utf-16-le")
        except pywintypes.error as exc:
            if exc.winerror != 1168:  # ERROR_NOT_FOUND
                raise

    signal("KEY_REQUIRED")
    key = ask_key()
    if key:
        win32cred.CredWrite({
            "Type": win32cred.CRED_TYPE_GENERIC,
            "TargetName": CREDENTIAL_TARGET,
            "CredentialBlob": key,
            "Persist": win32cred.CRED_PERSIST_LOCAL_MACHINE,
        })
    return key


def main():
    commands = Queue()
    threading.Thread(target=read_commands, args=(commands,), daemon=True).start()
    key = get_key()
    if not key:
        signal("CANCELLED")
        return
    signal("LOADING")
    from ASR.realtime_asr_test import load_model, recognize, record_sentence
    from Semantic.task_common import evaluate_text
    from openai import AuthenticationError
    model = load_model(lambda: signal("LOADING_WEIGHTS"))
    signal("READY")
    while True:
        try:
            request = json.loads(commands.get())
            audio = record_sentence(request["silence_timeout"])
            if audio is None:
                signal("SILENCE")
                continue
            text = recognize(audio, model)
            try:
                assessment = evaluate_text(text or "", request["manually_issue"], key)
            except AuthenticationError:
                key = get_key(replace=True)
                if not key:
                    signal("CANCELLED")
                    return
                assessment = evaluate_text(text or "", request["manually_issue"], key)
            if text and text.strip():
                signal("ARK " + assessment.model_dump_json(include={"result"}))
            signal(assessment.result.upper())
        except Exception as exc:
            code = getattr(exc, "code", None)
            code = code if isinstance(code, str) and code.isascii() and code.isidentifier() else ""
            signal("FAULT " + type(exc).__name__ + " " + code)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        signal("FAULT " + type(exc).__name__)
