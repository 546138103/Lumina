
# Direct launch reserves the port before importing/loading the large TTS model.
if __name__ == "__main__":
    import socket
    import sys

    listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        if hasattr(socket, "SO_EXCLUSIVEADDRUSE"):
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        listener.bind(("127.0.0.1", 8001))
        listener.listen(128)
    except OSError as error:
        listener.close()
        print(f"[ERROR] 无法启动 TTS：127.0.0.1:8001 已占用或不可用。{error}", flush=True)
        sys.exit(3)

    try:
        import uvicorn

        uvicorn.Server(uvicorn.Config(
            "server_CustomVoice:app", host="127.0.0.1", port=8001,
            log_level="info", workers=1,
        )).run(sockets=[listener])
    finally:
        listener.close()
    sys.exit(0)

import os
import re
import shutil
from pathlib import Path

sox_dir = Path(__file__).resolve().parent / "sox-14-4-2"

os.environ["PATH"] = (
    str(sox_dir) + os.pathsep + os.environ["PATH"]
)

from threading import Lock
from uuid import uuid4

import soundfile as sf
import torch
from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from pydantic import BaseModel, Field
from qwen_tts import Qwen3TTSModel


# ==================================
# 1. 项目路径
# ==================================

# 当前文件所在目录：Lumina/Tools/TTS/
BASE_DIR = Path(__file__).resolve().parent

# Lumina 项目根目录
PROJECT_ROOT = BASE_DIR.parent.parent

# 直接保存到 Unity 的 Assets 目录
OUTPUT_DIR = (
    PROJECT_ROOT
    / "Assets"
    / "Musics"
    / "GeneratedTTS"
)

# 临时音频目录，避免 Unity 导入尚未写完的文件
TEMP_DIR = BASE_DIR / "temp"

OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
TEMP_DIR.mkdir(parents=True, exist_ok=True)


# ==================================
# 2. 创建 FastAPI 服务
# ==================================

app = FastAPI(title="Lumina TTS Service")


# ==================================
# 3. 加载 Qwen3-TTS 模型
# ==================================

MODEL_NAME = "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice"

print("正在加载 Qwen3-TTS...")

model = Qwen3TTSModel.from_pretrained(
    MODEL_NAME,
    device_map="cpu",
    dtype=torch.float32,
    attn_implementation="eager"
)

print("Qwen3-TTS 加载完成")

SUPPORTED_SPEAKERS = model.get_supported_speakers()
print("支持的音色：", SUPPORTED_SPEAKERS)

# 一次只执行一个生成任务，防止文件命名冲突
model_lock = Lock()


# ==================================
# 4. Unity 发送的数据结构
# ==================================

class TTSRequest(BaseModel):
    text: str = Field(min_length=1, max_length=500)
    speaker: str = "Serena"
    instruct: str = Field(default="", max_length=500)
    language: str = "Chinese"


# ==================================
# 5. 根据 text 生成合法文件名
# ==================================

def make_safe_filename(text: str) -> str:
    # 去除首尾空白
    filename = text.strip()

    # 替换 Windows 禁止的文件名字符及控制字符
    filename = re.sub(
        r'[<>:"/\\|?*\x00-\x1f]',
        "_",
        filename
    )

    # 将连续空白合并为一个空格
    filename = re.sub(r"\s+", " ", filename)

    # Windows 不允许文件名以空格或句点结束
    filename = filename.strip(" .")

    # 防止文件名过长
    filename = filename[:60].rstrip(" .")

    if not filename:
        filename = "未命名语音"

    # Windows 保留设备名称
#     reserved = {
#     "CON", "PRN", "AUX", "NUL",
#     "COM1", "COM2", "COM3",
#     "COM4", "COM5", "COM6",
#     "COM7", "COM8", "COM9",
#     "LPT1", "LPT2", "LPT3",
#     "LPT4", "LPT5", "LPT6",
#     "LPT7", "LPT8", "LPT9"
# }
    reserved = {
        "CON", "PRN", "AUX", "NUL",
        *(f"COM{i}" for i in range(1, 10)),
        *(f"LPT{i}" for i in range(1, 10))
    }

    if filename.split(".")[0].upper() in reserved:
        filename = "_" + filename

    return filename


def get_unique_output_path(text: str) -> Path:
    base_name = make_safe_filename(text)

    output_path = OUTPUT_DIR / f"{base_name}.wav"

    # 同名文件已存在时，添加序号
    counter = 2

    while output_path.exists():
        output_path = (
            OUTPUT_DIR / f"{base_name}_{counter}.wav"
        )
        counter += 1

    return output_path


# ==================================
# 6. 健康检查
# ==================================

@app.get("/health")
def health():
    return {
        "status": "ok",
        "model": MODEL_NAME,
        "project_root": str(PROJECT_ROOT),
        "pid": os.getpid(),
    }


# ==================================
# 7. 生成语音
# ==================================

@app.post("/tts")
def generate_tts(request: TTSRequest):

    text = request.text.strip()
    instruct = request.instruct.strip()

    if not text:
        raise HTTPException(
            status_code=400,
            detail="生成文字不能为空"
        )

    temp_path = TEMP_DIR / f"{uuid4().hex}.wav"

    try:
        with model_lock:

            # instruct 为空时，不传情绪指令
            kwargs = {
                "text": text,
                "speaker": request.speaker,
                "language": request.language
            }

            if instruct:
                kwargs["instruct"] = instruct

            print(f"开始生成：{text}")
            print(f"音色：{request.speaker}")
            print(f"情绪：{instruct or '默认'}")

            wavs, sample_rate = (
                model.generate_custom_voice(**kwargs)
            )
            
            output_path = get_unique_output_path(text)
            sf.write(
                str(output_path),
                wavs[0],
                sample_rate,
                subtype="PCM_16"
            )


        print(f"生成成功：{output_path}")

        return {
            "success": True,
            "filename": output_path.name,
            "asset_path": (
                "Assets/Musics/GeneratedTTS/"
                + output_path.name
            )
        }

    except Exception as error:
        print("生成失败：", repr(error))

        raise HTTPException(
            status_code=500,
            detail="TTS 生成失败，请查看 Python 日志"
        )

    finally:
        # 异常时清理未使用的临时文件
        if temp_path.exists():
            temp_path.unlink()


# ==================================
# 8. 提供音频下载接口
#    供其他 C# 脚本实时调用
# ==================================

@app.get("/audio/{filename}")
def get_audio(filename: str):

    # 不允许路径跳转
    if (
        Path(filename).name != filename
        or "\\" in filename
        or not filename.lower().endswith(".wav")
    ):
        raise HTTPException(
            status_code=400,
            detail="无效文件名"
        )

    audio_path = OUTPUT_DIR / filename

    if not audio_path.is_file():
        raise HTTPException(
            status_code=404,
            detail="音频不存在"
        )

    return FileResponse(
        path=audio_path,
        media_type="audio/wav",
        filename=filename
    )
