"""Port preflight tests: do not import or download the TTS model."""
import contextlib
import io
from pathlib import Path
import runpy
import socket
import subprocess
import sys
import types
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).with_name("server_CustomVoice.py")


class StartupTests(unittest.TestCase):
    def test_occupied_port_exits_before_model_loading(self):
        owner = socket.socket()
        try:
            # If the user's TTS already owns the port, leave it untouched.
            try:
                owner.bind(("127.0.0.1", 8001))
                owner.listen()
            except OSError:
                owner.close()
            result = subprocess.run(
                [sys.executable, "-B", "-u", str(SCRIPT)],
                capture_output=True, timeout=10,
            )
            self.assertEqual(result.returncode, 3)
            self.assertIn(b"8001", result.stdout)
            self.assertNotIn(b"flash-attn", result.stdout)
        finally:
            owner.close()

    def test_reserved_socket_is_passed_to_uvicorn(self):
        events = []

        class Listener:
            def setsockopt(self, *args): pass
            def bind(self, address): events.append(("bind", address))
            def listen(self, backlog): events.append(("listen", backlog))
            def close(self): events.append(("close",))

        listener = Listener()

        def config(app, **kwargs):
            self.assertEqual(app, "server_CustomVoice:app")
            return kwargs

        class Server:
            def __init__(self, config): pass
            def run(self, *, sockets):
                self_socket = sockets[0]
                if self_socket is not listener or events[:2] != [
                    ("bind", ("127.0.0.1", 8001)), ("listen", 128)
                ]:
                    raise AssertionError("Uvicorn did not receive the reserved socket")
                events.append(("run",))

        fake = types.SimpleNamespace(Config=config, Server=Server)
        with patch("socket.socket", return_value=listener), \
             patch.dict(sys.modules, {"uvicorn": fake}), \
             contextlib.redirect_stdout(io.StringIO()), \
             self.assertRaises(SystemExit) as result:
            runpy.run_path(str(SCRIPT), run_name="__main__")
        self.assertEqual(result.exception.code, 0)
        self.assertEqual(events[-2:], [("run",), ("close",)])


if __name__ == "__main__":
    unittest.main()
