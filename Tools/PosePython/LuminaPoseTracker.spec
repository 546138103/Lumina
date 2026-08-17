from pathlib import Path

from PyInstaller.utils.hooks import get_package_paths


pose_python_root = Path(SPEC).resolve().parent
mediapipe_root = Path(get_package_paths("mediapipe")[1])
mediapipe_resources = [
    "modules/pose_detection/pose_detection.tflite",
    "modules/pose_landmark/pose_landmark_cpu.binarypb",
    "modules/pose_landmark/pose_landmark_full.tflite",
    "modules/pose_landmark/pose_landmark_heavy.tflite",
]

datas = []
for relative_path in mediapipe_resources:
    source = mediapipe_root / relative_path
    if not source.is_file():
        raise FileNotFoundError(
            f"Required MediaPipe resource is missing: {source}. "
            "Run build-runtime.cmd while connected to the internet."
        )

    destination = str(Path("mediapipe") / Path(relative_path).parent)
    datas.append((str(source), destination))

a = Analysis(
    [str(pose_python_root / "main.py")],
    pathex=[str(pose_python_root)],
    binaries=[],
    datas=datas,
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="LuminaPoseTracker",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="LuminaPoseTracker",
)
