import socket
import struct
import threading
import time

import cv2


class PreviewClient(threading.Thread):
    """Sends only the latest camera preview frame to Unity over localhost TCP."""

    def __init__(
        self,
        host,
        port,
        width,
        height,
        jpeg_quality,
        max_fps,
    ):
        super().__init__(daemon=True)
        self.host = host
        self.port = port
        self.width = width
        self.height = height
        self.jpeg_quality = jpeg_quality
        self.minimum_submit_interval = 1.0 / max(1, max_fps)

        self._condition = threading.Condition()
        self._latest_frame = None
        self._last_submit_time = 0.0
        self._stop_requested = False
        self._socket = None
        self._last_connection_log_time = 0.0

    def submit_frame(self, frame):
        now = time.monotonic()
        if now - self._last_submit_time < self.minimum_submit_interval:
            return

        self._last_submit_time = now
        with self._condition:
            # The capture loop reuses image buffers, so the sender owns a copy.
            self._latest_frame = frame.copy()
            self._condition.notify()

    def stop(self):
        with self._condition:
            self._stop_requested = True
            self._condition.notify_all()
        self._disconnect()

    def run(self):
        while True:
            frame = self._take_latest_frame()
            if frame is None:
                if self._stop_requested:
                    break
                continue

            try:
                resized = cv2.resize(
                    frame,
                    (self.width, self.height),
                    interpolation=cv2.INTER_AREA,
                )
                encoded, jpeg = cv2.imencode(
                    ".jpg",
                    resized,
                    [cv2.IMWRITE_JPEG_QUALITY, self.jpeg_quality],
                )
                if not encoded:
                    continue

                self._ensure_connected()
                payload = jpeg.tobytes()
                self._socket.sendall(struct.pack("!I", len(payload)) + payload)
            except (ConnectionError, OSError):
                self._disconnect()
                time.sleep(0.25)

        self._disconnect()

    def _take_latest_frame(self):
        with self._condition:
            if self._latest_frame is None and not self._stop_requested:
                self._condition.wait(timeout=0.25)

            frame = self._latest_frame
            self._latest_frame = None
            return frame

    def _ensure_connected(self):
        if self._socket is not None:
            return

        try:
            self._socket = socket.create_connection(
                (self.host, self.port),
                timeout=1.0,
            )
            self._socket.settimeout(1.0)
            print(
                "Unity camera preview connected @ "
                f"{self.host}:{self.port}"
            )
        except OSError:
            now = time.monotonic()
            if now - self._last_connection_log_time >= 3.0:
                print(
                    "Waiting for Unity camera preview receiver @ "
                    f"{self.host}:{self.port}"
                )
                self._last_connection_log_time = now
            raise

    def _disconnect(self):
        current_socket = self._socket
        self._socket = None

        if current_socket is None:
            return

        try:
            current_socket.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass

        try:
            current_socket.close()
        except OSError:
            pass
