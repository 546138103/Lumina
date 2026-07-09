# Internally used, don't mind this.
KILL_THREADS = False

# Diagnostic statistics in the Python console.
DEBUG = True

# The old OpenCV window is disabled because Unity now displays the preview.
SHOW_OPENCV_WINDOW = False

# Unity preview settings. The preview uses a separate TCP channel so a slow
# image frame never blocks the landmark UDP stream.
SEND_UNITY_PREVIEW = True
DRAW_PREVIEW_LANDMARKS = True
PREVIEW_HOST = '127.0.0.1'
PREVIEW_PORT = 52734
PREVIEW_WIDTH = 480
PREVIEW_HEIGHT = 360
PREVIEW_FPS = 12
PREVIEW_JPEG_QUALITY = 70

# Change UDP connection settings (must match Unity side)
USE_LEGACY_PIPES = False # Only supported on Windows (if True, use NamedPipes rather than UDP sockets)
HOST = '127.0.0.1'
PORT = 52733

# Settings do not universally apply, not all WebCams support all frame rates and resolutions
CAM_INDEX = 0 # OpenCV2 webcam index, try changing for using another (ex: external) webcam.
USE_CUSTOM_CAM_SETTINGS = True
FPS = 30
CAPTURE_WIDTH = 640
CAPTURE_HEIGHT = 480

# MediaPipe and the preview window use the downscaled full frame.
PROCESS_WIDTH = 640
PROCESS_HEIGHT = 480

# [0, 2] Higher numbers are more precise, but also cost more performance. The demo video used 2 (good environment is more important).
MODEL_COMPLEXITY = 2
