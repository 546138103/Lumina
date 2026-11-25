"""Preview transport regression test; does not open a camera."""
import socket
import struct
import threading
import unittest
from unittest.mock import patch

import cv2
import numpy as np
from preview_client import PreviewClient


class PreviewClientTests(unittest.TestCase):
    def test_sender_thread_delivers_a_decodable_jpeg(self):
        errors = []
        listener = socket.socket()
        listener.bind(('127.0.0.1', 0))
        listener.listen(1)
        listener.settimeout(2)
        sender = PreviewClient('127.0.0.1', listener.getsockname()[1], 70, 12)
        try:
            with patch.object(threading, 'excepthook', side_effect=lambda args: errors.append(args.exc_value)):
                sender.start()
                sender.submit_frame(np.zeros((24, 32, 3), dtype=np.uint8))
                try:
                    connection, _ = listener.accept()
                except socket.timeout:
                    self.fail(f'预览线程没有连接接收端；线程异常：{errors}')
                with connection:
                    connection.settimeout(2)
                    def read_exact(size):
                        data = bytearray()
                        while len(data) < size:
                            part = connection.recv(size - len(data))
                            if not part:
                                self.fail('JPEG 数据未发送完整')
                            data.extend(part)
                        return bytes(data)
                    length = struct.unpack('!I', read_exact(4))[0]
                    frame = cv2.imdecode(np.frombuffer(read_exact(length), dtype=np.uint8), cv2.IMREAD_COLOR)
                    self.assertIsNotNone(frame)
                    self.assertEqual(frame.shape, (24, 32, 3))
                    self.assertEqual(errors, [])
        finally:
            sender.stop()
            sender.join(timeout=2)
            listener.close()


if __name__ == '__main__':
    unittest.main()
